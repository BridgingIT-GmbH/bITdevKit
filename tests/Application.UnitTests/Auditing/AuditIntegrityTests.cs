// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Auditing;

using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Auditing;
using Microsoft.Extensions.DependencyInjection;

public class AuditIntegrityTests
{
    [Fact]
    public void AuthenticationPrimitive_MatchesRfc4231Case6()
    {
        // https://www.rfc-editor.org/rfc/rfc4231#section-4.7
        var key = Enumerable.Repeat((byte)0xaa, 131).ToArray();
        var tag = HmacSha256AuditIntegrityService.ComputeAuthenticationTag(key, Encoding.ASCII.GetBytes("Test Using Larger Than Block-Size Key - Hash Key First"));
        Convert.ToHexStringLower(tag).ShouldBe("60e431591ee0b67f0d8a26aacbf5b77f8e0bc6213728c5140546040f0ee37f54");
    }

    [Fact]
    public async Task SealVerify_RetainsUnknownAdditiveFieldsAndNestedHashNames()
    {
        using var host = Host();
        var integrity = host.GetRequiredService<HmacSha256AuditIntegrityService>();
        var json = JsonNode.Parse(AuditRecordCodec.Encode(Record()));
        json["futureField"] = new JsonObject { ["contentHash"] = "nested-retained", ["items"] = new JsonArray(1, 2) };
        json["contentHash"] = null;
        var original = AuditRecordCodec.Decode(Encoding.UTF8.GetBytes(json.ToJsonString())).Value;
        var sealedRecord = (await integrity.SealAsync(original)).Value;
        (await integrity.VerifyAsync(sealedRecord)).Value.ShouldBe("Verified");
        var encoded = AuditRecordCodec.Encode(sealedRecord);
        Encoding.UTF8.GetString(encoded).ShouldContain("nested-retained");
        var restored = AuditRecordCodec.Decode(encoded).Value;
        (await integrity.VerifyAsync(restored)).Value.ShouldBe("Verified");
        var tampered = JsonNode.Parse(encoded);
        tampered["futureField"]["contentHash"] = "changed";
        tampered["contentHash"] = null;
        var forgedHash = AuditRecordCodec.Decode(Encoding.UTF8.GetBytes(tampered.ToJsonString())).Value;
        (await integrity.VerifyAsync(forgedHash)).IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("action")]
    [InlineData("detail")]
    [InlineData("outcome")]
    [InlineData("key-id")]
    [InlineData("tag")]
    [InlineData("tag-whitespace")]
    [InlineData("short-tag")]
    [InlineData("algorithm")]
    [InlineData("profile")]
    public async Task Verify_RejectsChangedContentHeadersAndNonCanonicalTags(string mutation)
    {
        using var host = Host();
        var integrity = host.GetRequiredService<HmacSha256AuditIntegrityService>();
        var record = (await integrity.SealAsync(Record())).Value;
        var changed = mutation switch
        {
            "actor" => record with { Actor = record.Actor with { UserId = "forged" }, ContentHash = null },
            "action" => record with { Actions = [record.Actions[0] with { Key = "forged" }], ContentHash = null },
            "detail" => record with { Details = record.Details.SetItem("count", AuditValue.From(9L)), ContentHash = null },
            "outcome" => record with { Outcome = AuditOutcome.Failed, ContentHash = null },
            "key-id" => record with { IntegrityProtection = record.IntegrityProtection with { KeyId = "second" } },
            "tag" => record with { IntegrityProtection = record.IntegrityProtection with { Tag = Convert.ToBase64String(new byte[32]) } },
            "tag-whitespace" => record with { IntegrityProtection = record.IntegrityProtection with { Tag = " " + record.IntegrityProtection.Tag } },
            "short-tag" => record with { IntegrityProtection = record.IntegrityProtection with { Tag = Convert.ToBase64String(new byte[16]) } },
            "algorithm" => record with { IntegrityProtection = record.IntegrityProtection with { Algorithm = "SHA-256" } },
            _ => record with { IntegrityProtection = record.IntegrityProtection with { CanonicalizationVersion = 2 } }
        };
        (await integrity.VerifyAsync(changed)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task Rotation_SealsOnceAndDuplicateAcknowledgmentVerifiesExistingOriginal()
    {
        using var host = Host();
        var keys = host.GetRequiredService<KeyRing>();
        var integrity = host.GetRequiredService<HmacSha256AuditIntegrityService>();
        var provider = host.GetRequiredService<IAuditingStorageProvider>();
        var original = Record();
        var first = (await integrity.SealAsync(original)).Value;
        (await provider.AppendAsync(first)).IsSuccess.ShouldBeTrue();
        keys.Active = "second";
        var unchanged = (await integrity.SealAsync(first)).Value;
        AuditRecordCodec.Encode(unchanged).ShouldBe(AuditRecordCodec.Encode(first));
        var second = (await integrity.SealAsync(original)).Value;
        second.IntegrityProtection.KeyId.ShouldBe("second");
        second.ContentHash.ShouldBe(first.ContentHash);
        (await provider.AppendAsync(second)).Value.State.ShouldBe("AlreadyPresent");
        keys.Keys.Remove("first");
        (await integrity.VerifyAsync(first)).Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditIntegrityUnavailable");
        (await integrity.SealAsync(first)).Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditIntegrityUnavailable");
        (await provider.AppendAsync(second)).IsFailure.ShouldBeTrue();
        (await provider.FindAsync(original.AuditId)).Value.IntegrityProtection.KeyId.ShouldBe("first");
    }

    [Fact]
    public async Task RequiredProtection_MissingEnvelopeFailsUnlessHostExplicitlyAllowsUnsignedHistory()
    {
        using var required = Host();
        var record = Record();
        var missing = await required.GetRequiredService<HmacSha256AuditIntegrityService>().VerifyAsync(record);
        missing.Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditIntegrityMissingProtection");
        (await required.GetRequiredService<IAuditingStorageProvider>().AppendAsync(record)).IsFailure.ShouldBeTrue();
        using var allowed = Host(new() { AllowUnsignedHistory = true });
        (await allowed.GetRequiredService<HmacSha256AuditIntegrityService>().VerifyAsync(record)).Value.ShouldBe("NotProtected");
        (await allowed.GetRequiredService<IAuditingStorageProvider>().AppendAsync(record)).Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditIntegrityMissingProtection");
    }

    [Fact]
    public async Task KeyFailure_PreservesBusinessResultAndNeverAdmitsUnsignedFallback()
    {
        using var host = Host();
        host.GetRequiredService<KeyRing>().Fail = true;
        using var scope = host.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IAuditContext>();
        Guid id = default;
        var expected = new object();
        var result = await context.RunAsync(o => o.Key("customer.rename"), (audit, ct) => { id = audit.AuditId; return Task.FromResult(expected); });
        result.ShouldBeSameAs(expected);
        (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(id)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task RequiredDetailRead_VerifiesOriginalBeforeApplyingCurrentExposureAndArchiveKeepsTag()
    {
        using var host = Host();
        var original = Record() with { Details = ImmutableDictionary<string, AuditValue>.Empty.Add("tenantId", AuditValue.From("A")) };
        var sealedRecord = (await host.GetRequiredService<HmacSha256AuditIntegrityService>().SealAsync(original)).Value;
        var provider = host.GetRequiredService<IAuditingStorageProvider>();
        await provider.AppendAsync(sealedRecord);
        await provider.ArchiveBeforeAsync(DateTimeOffset.UtcNow.AddDays(1));
        using var scope = host.CreateScope();
        var details = (await scope.ServiceProvider.GetRequiredService<IAuditQueryService>().FindAsync(original.AuditId)).Value;
        details.StorageState.ShouldBe("Archived");
        details.IntegrityStatus.ShouldBe("Verified");
        details.IsTransformed.ShouldBeTrue();
        details.Record.Details["tenantId"].Marker.ShouldBe("Redacted");
        AuditRecordCodec.Encode((await provider.FindAsync(original.AuditId)).Value).ShouldBe(AuditRecordCodec.Encode(sealedRecord));
    }

    [Fact]
    public async Task KeyResolution_TimeoutClearsLateReturnedOwnedMaterial()
    {
        var pending = new TaskCompletionSource<Result<AuditIntegrityKey>>();
        var services = new ServiceCollection();
        services.AddAuditing().WithHmacSha256<SlowKeys>(new() { KeyResolutionTimeout = TimeSpan.FromMilliseconds(30) });
        services.AddSingleton(pending);
        using var host = services.BuildServiceProvider();
        var result = await host.GetRequiredService<HmacSha256AuditIntegrityService>().SealAsync(Record());
        result.IsFailure.ShouldBeTrue();
        var late = new AuditIntegrityKey("late", new byte[32]);
        pending.SetResult(Result<AuditIntegrityKey>.Success(late));
        Should.Throw<ObjectDisposedException>(() => late.GetMaterial());
    }

    [Fact]
    public void KeyMaterial_IsCopiedBoundedAndClearedOnDisposal()
    {
        var source = Enumerable.Repeat((byte)7, 32).ToArray();
        var key = new AuditIntegrityKey("external", source);
        var retained = key.GetMaterial();
        Should.Throw<NotSupportedException>(() => JsonSerializer.Serialize(key));
        source[0] = 99;
        retained.Span[0].ShouldBe((byte)7);
        key.Dispose();
        retained.ToArray().ShouldAllBe(value => value == 0);
        Should.Throw<ObjectDisposedException>(() => key.GetMaterial());
        Should.Throw<ArgumentException>(() => new AuditIntegrityKey("weak", new byte[31]));
        Should.Throw<ArgumentException>(() => new AuditIntegrityKey("line\nbreak", new byte[32]));
        Should.Throw<ArgumentException>(() => new AuditIntegrityKey(new string('ä', 65), new byte[32]));
    }

    [Fact]
    public void StructuredFrame_IsPinnedToVersionOneGoldenBytes()
    {
        const string json = """
            {"auditId":"00000000-0000-0000-0000-000000000001","originAuditId":"00000000-0000-0000-0000-000000000001","key":"customers.rename","schemaVersion":1,"startedUtc":"2026-10-30T12:00:00.0000000Z","completedUtc":"2026-10-30T12:01:00.0000000Z"}
            """;
        var original = AuditRecordCodec.Decode(Encoding.UTF8.GetBytes(json)).Value;
        var frame = HmacSha256AuditIntegrityService.CreateFrame(original, new() { KeyId = "golden" });
        Encoding.UTF8.GetString(frame).ShouldBe("""
            {"algorithm":"HMAC-SHA-256","canonicalizationVersion":1,"document":{"auditId":"00000000-0000-0000-0000-000000000001","completedUtc":"2026-10-30T12:01:00.0000000Z","key":"customers.rename","originAuditId":"00000000-0000-0000-0000-000000000001","schemaVersion":1,"startedUtc":"2026-10-30T12:00:00.0000000Z"},"keyId":"golden","protectionVersion":1,"purpose":"bdk.auditing.record"}
            """);
    }

    [Fact]
    public async Task PermittedResponse_DropsUnknownFieldsAndReportsTransformationAfterOriginalVerification()
    {
        using var host = Host();
        var json = JsonNode.Parse(AuditRecordCodec.Encode(Record()));
        json["futureSensitiveField"] = "source-only-value";
        json["contentHash"] = null;
        var original = AuditRecordCodec.Decode(Encoding.UTF8.GetBytes(json.ToJsonString())).Value;
        var sealedRecord = (await host.GetRequiredService<HmacSha256AuditIntegrityService>().SealAsync(original)).Value;
        var provider = host.GetRequiredService<IAuditingStorageProvider>();
        (await provider.AppendAsync(sealedRecord)).IsSuccess.ShouldBeTrue();
        using var scope = host.CreateScope();
        var details = (await scope.ServiceProvider.GetRequiredService<IAuditQueryService>().FindAsync(original.AuditId)).Value;
        details.IntegrityStatus.ShouldBe("Verified");
        details.IsTransformed.ShouldBeTrue();
        Encoding.UTF8.GetString(AuditRecordCodec.Encode(details.Record)).ShouldNotContain("source-only-value");
        Encoding.UTF8.GetString(AuditRecordCodec.Encode((await provider.FindAsync(original.AuditId)).Value)).ShouldContain("source-only-value");
    }

    private static ServiceProvider Host(AuditIntegrityOptions options = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<KeyRing>();
        services.AddAuditing().WithHmacSha256<RotatingKeys>(options).WithSearchableDetail(new() { Name = "tenantId", ExposeValue = false });
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
    private static AuditRecord Record()
    {
        var id = Guid.NewGuid();
        return new()
        {
            AuditId = id, OriginAuditId = id, Key = "customers.rename", StartedUtc = DateTimeOffset.UtcNow.AddMinutes(-1), CompletedUtc = DateTimeOffset.UtcNow,
            Actor = new() { UserId = "U-42", Kind = "Authenticated", IsAuthenticated = true },
            Actions = [new() { Id = Guid.NewGuid(), Key = "customers.save", Outcome = AuditOutcome.Succeeded }],
            Details = ImmutableDictionary<string, AuditValue>.Empty.Add("count", AuditValue.From(1L))
        };
    }
    public sealed class KeyRing
    {
        public Dictionary<string, byte[]> Keys { get; } = new(StringComparer.Ordinal) { ["first"] = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray(), ["second"] = Enumerable.Repeat((byte)0x55, 32).ToArray() };
        public string Active { get; set; } = "first";
        public bool Fail { get; set; }
    }
    public sealed class RotatingKeys(KeyRing ring) : IAuditIntegrityKeyProvider
    {
        public Task<Result<AuditIntegrityKey>> ResolveActiveAsync(CancellationToken cancellationToken = default) => this.ResolveAsync(ring.Active, cancellationToken);
        public Task<Result<AuditIntegrityKey>> ResolveAsync(string keyId, CancellationToken cancellationToken = default) => Task.FromResult(!ring.Fail && ring.Keys.TryGetValue(keyId, out var material)
            ? Result<AuditIntegrityKey>.Success(new(keyId, material)) : Result<AuditIntegrityKey>.Failure());
    }
    public sealed class SlowKeys(TaskCompletionSource<Result<AuditIntegrityKey>> pending) : IAuditIntegrityKeyProvider
    {
        public Task<Result<AuditIntegrityKey>> ResolveActiveAsync(CancellationToken cancellationToken = default) => pending.Task;
        public Task<Result<AuditIntegrityKey>> ResolveAsync(string id, CancellationToken cancellationToken = default) => pending.Task;
    }
}
