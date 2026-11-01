// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Auditing;

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Auditing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

public sealed class AuditExportContractTests
{
    [Fact]
    public async Task ExportAsync_RawResult_ConfirmsExactDigestAndPrincipalBoundContinuation()
    {
        await using var services = AuditWebTestServices.Create(builder => builder.WithExports(options => options with { RecordLimit = 1 }));
        await using var scope = services.CreateAsyncScope();
        await AuditWebTestServices.CaptureAsync(scope.ServiceProvider);
        await AuditWebTestServices.CaptureAsync(scope.ServiceProvider);
        var runtime = AuditWebRuntime.Get(services);
        var http = AuditWebTestServices.Context(scope.ServiceProvider);

        await (await AuditHttpHandlers.ExportAsync(http, new(), runtime)).ExecuteAsync(http);

        http.Response.ContentType.ShouldStartWith("application/x-ndjson");
        http.Response.Headers.ContentDisposition.ToString().ShouldStartWith("attachment;");
        http.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
        http.Response.Headers.ContentEncoding.ShouldBeEmpty();
        var frames = Split(((MemoryStream)http.Response.Body).ToArray());
        frames.Count.ShouldBe(3);
        using var record = JsonDocument.Parse(frames[1]);
        record.RootElement.GetProperty("kind").GetString().ShouldBe("record");
        var completion = await CompletionAsync(http, runtime);
        completion.OutputConfirmed.ShouldBeTrue();
        completion.ClientConfirmed.ShouldBeFalse();
        completion.Status.ShouldBe("Partial");
        completion.Sha256.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(frames[1])));
        completion.SourceRecordCount.ShouldBe(1);
        completion.CheckpointToken.ShouldBeNull();
        completion.ContinuationToken.ShouldNotBeNullOrEmpty();

        var continued = AuditWebTestServices.Context(scope.ServiceProvider, "?continuationToken=" + Uri.EscapeDataString(completion.ContinuationToken));
        await (await AuditHttpHandlers.ExportAsync(continued, new(), runtime)).ExecuteAsync(continued);
        var finished = await CompletionAsync(continued, runtime);
        finished.Status.ShouldBe("Completed");
        finished.CheckpointToken.ShouldNotBeNullOrEmpty();
        finished.ContinuationToken.ShouldBeNull();

        var foreign = AuditWebTestServices.Context(scope.ServiceProvider, "?continuationToken=" + Uri.EscapeDataString(completion.ContinuationToken), "another-reviewer");
        ((IStatusCodeHttpResult)await AuditHttpHandlers.ExportAsync(foreign, new(), runtime)).StatusCode.ShouldBe(400);
        ((IStatusCodeHttpResult)await AuditHttpHandlers.StatusAsync(foreign, completion.Metadata.ExportId, runtime)).StatusCode.ShouldBe(404);
    }

    [Theory]
    [InlineData("csv", "text/csv")]
    [InlineData("cef", "text/plain")]
    public async Task ExecuteAsync_OptionalProfiles_ConfirmExactOutputBytes(string format, string mediaType)
    {
        await using var services = AuditWebTestServices.Create(builder => builder.WithExportFormatter(_ => new CefAuditExportFormatter(new() { Vendor = "Northwind", Product = "CustomerPortal", Version = "1" })));
        await using var scope = services.CreateAsyncScope();
        await AuditWebTestServices.CaptureAsync(scope.ServiceProvider);
        var runtime = AuditWebRuntime.Get(services);
        var http = AuditWebTestServices.Context(scope.ServiceProvider, "?format=" + format);

        await (await AuditHttpHandlers.ExportAsync(http, new(), runtime)).ExecuteAsync(http);

        http.Response.ContentType.Split(';')[0].ShouldBe(mediaType);
        var bytes = ((MemoryStream)http.Response.Body).ToArray();
        var completion = await CompletionAsync(http, runtime);
        completion.OutputConfirmed.ShouldBeTrue();
        completion.Sha256.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var text = Encoding.UTF8.GetString(bytes);
        if (format == "csv")
        {
            text.ShouldStartWith("SourceStoreId,AuditId,SchemaVersion");
            text.ShouldEndWith("\r\n");
            completion.OutputItemCount.ShouldBe(1);
        }
        else
        {
            text.ShouldStartWith("CEF:0|Northwind|CustomerPortal|1|");
            completion.OutputItemCount.ShouldBe(2);
        }
    }

    [Theory]
    [InlineData("?format=unregistered", 0)]
    [InlineData("?checkpoint=tampered", 0)]
    [InlineData("?checkpoint=x&continuationToken=y", 0)]
    [InlineData("", 2)]
    public async Task ExportAsync_InvalidFormatProgressOrPaging_ReturnsSafeBadRequest(string query, int page)
    {
        await using var services = AuditWebTestServices.Create();
        await using var scope = services.CreateAsyncScope();
        var http = AuditWebTestServices.Context(scope.ServiceProvider, query);

        var result = await AuditHttpHandlers.ExportAsync(http, new() { Page = page }, AuditWebRuntime.Get(services));

        ((IStatusCodeHttpResult)result).StatusCode.ShouldBe(400);
        http.Response.Body.Length.ShouldBe(0);
    }

    [Theory]
    [InlineData("{", "AuditFilterInvalid")]
    [InlineData("oversized", "AuditFilterTooLarge")]
    public async Task ReadFilterAsync_InvalidOrUnboundedBody_RejectsBeforeBinding(string input, string code)
    {
        var http = new DefaultHttpContext();
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(input == "oversized" ? new string(' ', 32_769) : input));
        var original = http.Request.Body;

        var error = await Should.ThrowAsync<AuditExportException>(() => AuditHttpHandlers.ReadFilterAsync(http));

        error.Code.ShouldBe(code);
        http.Request.Body.ShouldBeSameAs(original);
    }

    [Fact]
    public async Task ExportAsync_ChangedTrustedScope_HidesStatusAndRejectsOldProgress()
    {
        var decision = AuditReadScope.Unrestricted("reviewer-v1");
        await using var services = AuditWebTestServices.Create(builder => builder
            .WithExports(options => options with { RecordLimit = 1 })
            .WithReadScope((_, _) => Task.FromResult(Result<AuditReadScope>.Success(decision))));
        await using var scope = services.CreateAsyncScope();
        await AuditWebTestServices.CaptureAsync(scope.ServiceProvider);
        await AuditWebTestServices.CaptureAsync(scope.ServiceProvider);
        var runtime = AuditWebRuntime.Get(services);
        var http = AuditWebTestServices.Context(scope.ServiceProvider);
        await (await AuditHttpHandlers.ExportAsync(http, new(), runtime)).ExecuteAsync(http);
        var completion = await CompletionAsync(http, runtime);
        decision = new(new() { Filters = [new("ModuleName", FilterOperator.Equal, "Customers")] }, "customers-v1");
        var changed = AuditWebTestServices.Context(scope.ServiceProvider, "?continuationToken=" + Uri.EscapeDataString(completion.ContinuationToken));

        ((IStatusCodeHttpResult)await AuditHttpHandlers.StatusAsync(changed, completion.Metadata.ExportId, runtime)).StatusCode.ShouldBe(404);
        ((IStatusCodeHttpResult)await AuditHttpHandlers.ExportAsync(changed, new(), runtime)).StatusCode.ShouldBe(400);
    }

    private static async Task<AuditExportCompletion> CompletionAsync(HttpContext context, AuditWebRuntime runtime)
    {
        var id = Guid.Parse(context.Response.Headers["X-Audit-Export-Id"].ToString());
        var result = await AuditHttpHandlers.StatusAsync(context, id, runtime);
        return ((IValueHttpResult)result).Value.ShouldBeOfType<AuditExportCompletion>();
    }

    private static List<byte[]> Split(byte[] bytes)
    {
        var frames = new List<byte[]>();
        var start = 0;
        for (var index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != (byte)'\n') continue;
            frames.Add(bytes[start..(index + 1)]);
            start = index + 1;
        }

        start.ShouldBe(bytes.Length);
        return frames;
    }

    [Fact]
    public async Task Cef_EscapesInjectionAndReportsUnicodeFieldLossWithoutSplittingScalars()
    {
        typeof(CefAuditExportFormatter).Assembly.ShouldBe(typeof(IAuditExportFormatter).Assembly);
        var formatter = new CefAuditExportFormatter(new() { Vendor = "Northwind", Product = "CustomerPortal", Version = "1" });
        var source = new AuditExportRecord { SourceStoreId = Guid.NewGuid(), Details = new() { IntegrityStatus = "NotProtected", Record = new() { AuditId = Guid.NewGuid(), Key = "customers|rename", DisplayName = "Rename\r\nforged=1\\", StartedUtc = DateTimeOffset.UtcNow, Actor = new() { UserId = new string('€', 1024) } } } };
        using var frame = await formatter.RecordAsync(source, new() { GraphByteLimit = 16384 });
        var text = Encoding.UTF8.GetString(frame.Bytes.Span);
        text.ShouldContain("customers\\|rename"); text.ShouldContain("Rename  forged=1\\\\"); text.Count(character => character == '\n').ShouldBe(1);
        frame.Transformations["CefFieldLoss"].ShouldBeGreaterThan(0);
        text.ShouldNotContain('\uFFFD'); text.ShouldContain("|0|");
    }

    [Fact]
    public void ProtectedProgress_RejectsExpiredForeignKeyAndDifferentIdentityMappings()
    {
        var now = DateTimeOffset.UtcNow;
        var time = new TestClock(now);
        var options = new AuditExportOptions();
        var protector = new AuditExportTokenProtector(new EphemeralDataProtectionProvider(), options, time);
        var identity = new ClaimsIdentity([new Claim("name", "reviewer"), new Claim("role", "Editor")], "fixture", "name", "role");
        var principal = AuditExportTokenProtector.PrincipalFingerprint(new(identity));
        var changed = AuditExportTokenProtector.PrincipalFingerprint(new(new ClaimsIdentity(identity.Claims, "fixture", "different-name", "different-role")));
        principal.ShouldNotBe(changed);
        var completion = new AuditExportCompletion { OutputConfirmed = true, Progress = new() { StoreId = Guid.NewGuid(), StoreEpoch = Guid.NewGuid(), ExpiresUtc = now.AddMinutes(1), IsCheckpoint = true } };
        var token = protector.Protect(completion, principal);
        protector.Unprotect(token, principal).IsSuccess.ShouldBeTrue();
        protector.Unprotect(token, changed).IsFailure.ShouldBeTrue();
        new AuditExportTokenProtector(new EphemeralDataProtectionProvider(), options, time).Unprotect(token, principal).IsFailure.ShouldBeTrue();
        time.Now = now.AddMinutes(2); protector.Unprotect(token, principal).IsFailure.ShouldBeTrue();
        Should.Throw<AuditExportException>(() => AuditExportTokenProtector.PrincipalFingerprint(new(new ClaimsIdentity(Enumerable.Range(0, 129).Select(index => new Claim("id", index.ToString()))))));
    }

    [Fact]
    public async Task CleanupFailureAndObserverFailure_DoNotPublishConfirmedCompanionProgress()
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddAuditing();
        await using var root = services.BuildServiceProvider();
        await using var scope = root.CreateAsyncScope();
        var session = (await scope.ServiceProvider.GetRequiredService<IAuditExportService>().OpenAsync()).Value;
        long frames = 0, bytes = 0;
        await foreach (var frame in session.ReadFramesAsync()) { using (frame) { frames++; bytes += frame.Bytes.Length; } }

        var runtime = AuditWebRuntime.Get(root); var principal = AuditExportTokenProtector.PrincipalFingerprint(new());
        runtime.Attempts.TryStart(session.Metadata, principal).ShouldBeTrue();
        await new AuditExportHttpObserver(session, runtime, principal).OnFinishedAsync(new(NdjsonStreamStatus.Succeeded, true, false, frames, bytes, frames, bytes, false));
        var failed = runtime.Attempts.Find(session.Metadata.ExportId, principal, session.Metadata.ScopeFingerprint);
        failed.OutputConfirmed.ShouldBeFalse(); failed.CheckpointToken.ShouldBeNull(); failed.Status.ShouldBe("Failed");
        var next = (await scope.ServiceProvider.GetRequiredService<IAuditExportService>().OpenAsync()).Value;
        frames = 0; bytes = 0;
        await foreach (var frame in next.ReadFramesAsync()) { using (frame) { frames++; bytes += frame.Bytes.Length; } }

        runtime.Attempts.TryStart(next.Metadata, principal).ShouldBeTrue();
        await Should.ThrowAsync<AuditExportException>(() => new AuditExportHttpObserver(next, runtime, "invalid-principal-binding").OnFinishedAsync(new(NdjsonStreamStatus.Succeeded, true, true, frames, bytes, frames, bytes, false)));
        var unavailable = runtime.Attempts.Find(next.Metadata.ExportId, principal, next.Metadata.ScopeFingerprint);
        unavailable.Status.ShouldBe("Streaming"); unavailable.OutputConfirmed.ShouldBeFalse(); unavailable.CheckpointToken.ShouldBeNull();
    }

    public sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => this.Now;
    }

}
