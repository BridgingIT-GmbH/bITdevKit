// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Auditing;

using System.Text;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Presentation.Web.Auditing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

public sealed class AuditDownloadTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(250)]
    [InlineData(500)]
    public async Task DownloadAsync_ValidTicket_StreamsAttachmentAndRejectsReplay(int pageSize)
    {
        await using var services = AuditWebTestServices.Create();
        await using var scope = services.CreateAsyncScope();
        await AuditWebTestServices.CaptureAsync(scope.ServiceProvider);
        var runtime = AuditWebRuntime.Get(services);
        var ticket = await AuditDownloadHandlers.IssueAsync(AuditWebTestServices.Context(scope.ServiceProvider));
        var http = Form(scope.ServiceProvider, ticket.Ticket, filter: "{\"pageSize\":" + pageSize + "}");

        await (await AuditDownloadHandlers.DownloadAsync(http, runtime)).ExecuteAsync(http);

        http.Response.ContentType.ShouldBe("text/csv");
        http.Response.Headers["X-Audit-Export-Id"].ToString().ShouldBe(ticket.ExportId.ToString("D"));
        Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray()).ShouldContain("Rename customer");
        var replay = await AuditDownloadHandlers.DownloadAsync(Form(scope.ServiceProvider, ticket.Ticket), runtime);
        ((IStatusCodeHttpResult)replay).StatusCode.ShouldBe(429);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("principal")]
    [InlineData("scope")]
    [InlineData("tampered")]
    public async Task DownloadAsync_InvalidTicketBinding_RejectsWithoutOutput(string mode)
    {
        var clock = new AuditExportContractTests.TestClock(DateTimeOffset.UtcNow);
        var decision = AuditReadScope.Unrestricted("scope-v1");
        await using var services = AuditWebTestServices.Create(builder => builder
            .WithExports(options => options with { MetadataLifetime = TimeSpan.FromSeconds(1) })
            .WithReadScope((_, _) => Task.FromResult(Result<AuditReadScope>.Success(decision))),
            collection => collection.AddSingleton<TimeProvider>(clock));
        await using var scope = services.CreateAsyncScope();
        var ticket = await AuditDownloadHandlers.IssueAsync(AuditWebTestServices.Context(scope.ServiceProvider));
        if (mode == "expired") clock.Now = clock.Now.AddSeconds(2);
        if (mode == "scope") decision = AuditReadScope.Unrestricted("scope-v2");
        var http = Form(scope.ServiceProvider, mode == "tampered" ? "invalid" : ticket.Ticket, mode == "principal" ? "another-reviewer" : "reviewer");

        var result = await AuditDownloadHandlers.DownloadAsync(http, AuditWebRuntime.Get(services));

        ((IStatusCodeHttpResult)result).StatusCode.ShouldBe(400);
        http.Response.Body.Length.ShouldBe(0);
    }

    [Fact]
    public async Task DownloadAsync_DifferentRuntimeWithSameProtectionKey_RejectsOldTicket()
    {
        var protection = new EphemeralDataProtectionProvider();
        await using var first = AuditWebTestServices.Create(services: services => services.AddSingleton<IDataProtectionProvider>(protection));
        await using var firstScope = first.CreateAsyncScope();
        var ticket = await AuditDownloadHandlers.IssueAsync(AuditWebTestServices.Context(firstScope.ServiceProvider));
        await using var second = AuditWebTestServices.Create(services: services => services.AddSingleton<IDataProtectionProvider>(protection));
        await using var secondScope = second.CreateAsyncScope();

        var result = await AuditDownloadHandlers.DownloadAsync(Form(secondScope.ServiceProvider, ticket.Ticket), AuditWebRuntime.Get(second));

        ((IStatusCodeHttpResult)result).StatusCode.ShouldBe(400);
    }

    private static DefaultHttpContext Form(IServiceProvider services, string ticket, string user = "reviewer", string filter = "{}")
    {
        var http = AuditWebTestServices.Context(services, user: user);
        var body = Encoding.UTF8.GetBytes("ticket=" + Uri.EscapeDataString(ticket) + "&filter=" + Uri.EscapeDataString(filter) + "&format=csv");
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.ContentLength = body.Length;
        http.Request.Body = new MemoryStream(body);
        return http;
    }
}
