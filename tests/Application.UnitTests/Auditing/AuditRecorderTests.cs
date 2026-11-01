// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Auditing;

using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Auditing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

public class AuditRecorderTests
{
    [Fact]
    public async Task DisposeAsync_NestedManualScopes_PersistsOneRootAndRestoresCurrent()
    {
        using var host = CreateHost();
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = sut.BeginScope(s => s.Key("customers.rename"));
        var id = root.AuditId;
        using (var child = sut.BeginScope(s => s.Key("customers.rename.apply")))
        {
            child.AddResource<Customer>("C-1");
            child.SetDetail("reason", AuditValue.From("Correction"));
            child.Complete();
        }

        sut.Current.ShouldBeSameAs(root);
        root.Complete();
        await root.DisposeAsync();

        sut.Current.ShouldBeNull();
        var record = (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(id)).Value;
        record.Outcome.ShouldBe(AuditOutcome.Succeeded);
        record.Completeness.ShouldBe(AuditCompleteness.Complete);
        record.Actions.Single().Resources.Single().Id.ShouldBe("C-1");
        record.Actions.Single().Details["reason"].Data.GetString().ShouldBe("Correction");
        record.ContentHash.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task RunAsync_ParallelBranches_PreserveDistinctParentsAndOccurrenceIdentities()
    {
        using var host = CreateHost();
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = sut.BeginScope(s => s.Key("parallel"));
        await Task.WhenAll(Enumerable.Range(0, 20).Select(i => Task.Run(() => sut.RunAsync(s => s.Key($"branch.{i}"), async (branch, ct) =>
        {
            await Task.Yield();
            using var nested = sut.BeginScope(s => s.Key($"nested.{i}"));
            nested.Complete();
            return branch.ActionId;
        }))));
        sut.Current.ShouldBeSameAs(root);
        root.Complete();
        await root.DisposeAsync();

        var record = (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).Value;
        record.Actions.Length.ShouldBe(40);
        record.Actions.Select(a => a.Id).Distinct().Count().ShouldBe(40);
        foreach (var child in record.Actions.Where(a => a.Key.StartsWith("nested.", StringComparison.Ordinal)))
            record.Actions.Single(a => a.Id == child.ParentActionId).Key.ShouldBe(child.Key.Replace("nested.", "branch.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_RepeatedCalls_DoNotLeaveClosedCurrentScopes()
    {
        using var host = CreateHost();
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        for (var i = 0; i < 25; i++)
        {
            await sut.RunAsync(s => s.Key("repeated"), (scope, ct) => Task.FromResult(1));
            sut.Current.ShouldBeNull();
        }
    }

    [Fact]
    public async Task DisposeAsync_ParentBeforeChild_FreezesIncompleteAndRejectsLateCapture()
    {
        using var host = CreateHost();
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = sut.BeginScope(s => s.Key("parent"));
        var child = sut.BeginScope(s => s.Key("child"));
        root.Complete();
        await root.DisposeAsync();
        child.IsRecording.ShouldBeFalse();
        child.SetDetail("late", AuditValue.From("must not persist"));
        using (var replacement = sut.BeginScope(s => s.Key("late.replacement"))) replacement.IsRecording.ShouldBeFalse();
        child.Dispose();
        sut.Current.ShouldBeNull();

        var record = (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).Value;
        record.Completeness.ShouldBe(AuditCompleteness.Incomplete);
        record.Actions.Single().ExecutionStatus.ShouldBe("Incomplete");
        record.Actions.Single().Details.ShouldBeEmpty();
    }

    [Fact]
    public async Task Dispose_SynchronousRecordingRoot_DoesNotWriteOrLeaveAmbientState()
    {
        using var host = CreateHost();
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = sut.BeginScope(s => s.Key("sync.root"));
        root.Complete();
        root.Dispose();
        await root.DisposeAsync();

        sut.Current.ShouldBeNull();
        (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddAuditing_FallbackInEitherOrder_ReplacesOnlyBuiltInFallback(bool fallbackFirst)
    {
        var services = new ServiceCollection();
        if (fallbackFirst) services.AddNoOpAuditContext();
        services.AddAuditing();
        services.AddNoOpAuditContext();
        using var host = services.BuildServiceProvider();
        using var scope = host.CreateScope();
        scope.ServiceProvider.GetRequiredService<IAuditContext>().ShouldBeOfType<AuditContext>();
        services.Count(d => d.ServiceType == typeof(IAuditContext)).ShouldBe(1);

        var custom = Substitute.For<IAuditContext>();
        var customServices = new ServiceCollection().AddSingleton(custom);
        customServices.AddAuditing();
        using var customHost = customServices.BuildServiceProvider();
        customHost.GetRequiredService<IAuditContext>().ShouldBeSameAs(custom);
    }

    [Fact]
    public async Task Suppress_RequiresNewAndManualDetails_SkipsAllCaptureCallbacks()
    {
        using var host = CreateHost();
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = sut.BeginScope(s => s.Key("selected"));
        var callbacks = 0;
        using (sut.Suppress())
        {
            root.SetDetail("hidden", AuditValue.From("secret"));
            using var independent = sut.BeginScope(s => { callbacks++; s.Key("hidden").Mode(AuditScopeMode.RequiresNew); });
            independent.IsRecording.ShouldBeFalse();
            (await sut.RunAsync(s => { callbacks++; s.Key("hidden"); }, (_, _) => Task.FromResult(42))).ShouldBe(42);
        }

        root.Complete();
        await root.DisposeAsync();
        callbacks.ShouldBe(0);
        var record = (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).Value;
        record.Actions.ShouldBeEmpty();
        record.Details.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_RejectedResultAndThrownFailure_PreserveOriginalBusinessSemantics()
    {
        using var host = CreateHost();
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var rejected = Result<string>.Failure(new ValidationError("private business payload"));
        Guid id = default;
        var result = await sut.RunAsync(s => s.Key("rejection"), (scope, ct) => { id = scope.AuditId; return Task.FromResult(rejected); });
        result.ShouldBe(rejected);
        (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(id)).Value.Outcome.ShouldBe(AuditOutcome.Rejected);

        var original = new InvalidOperationException("credential-that-must-never-be-stored");
        var error = await Should.ThrowAsync<InvalidOperationException>(() => sut.RunAsync<int>(s => s.Key("failure"), (scope, ct) =>
        { id = scope.AuditId; return Task.FromException<int>(original); }));
        error.ShouldBeSameAs(original);
        var failed = (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(id)).Value;
        failed.Outcome.ShouldBe(AuditOutcome.Failed);
        Encoding.UTF8.GetString(AuditRecordCodec.Encode(failed)).ShouldNotContain(original.Message);
    }

    [Fact]
    public async Task RunAsync_RequestedCancellation_FinalizesWithFreshAdmissionBudget()
    {
        using var host = CreateHost();
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Guid id = default;
        var error = await Should.ThrowAsync<OperationCanceledException>(() => sut.RunAsync<int>(s => s.Key("canceled"), (scope, ct) =>
        { id = scope.AuditId; return Task.FromCanceled<int>(ct); }, canceled.Token));
        error.CancellationToken.ShouldBe(canceled.Token);
        (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(id)).Value.Outcome.ShouldBe(AuditOutcome.Canceled);
    }

    [Fact]
    public async Task BeginScope_ActorAndModule_AreCapturedBeforeBusinessChanges()
    {
        var user = Substitute.For<ICurrentUserAccessor>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("user-1");
        user.UserName.Returns("Alex");
        var module = Substitute.For<IAuditModuleContextAccessor>();
        module.Find(typeof(Customer)).Returns("Customers");
        using var host = CreateHost(s => { s.AddSingleton(user); s.AddSingleton(module); });
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = sut.BeginScope(s => s.For<Customer>("Rename"));
        user.UserId.Returns("user-2");
        root.Complete();
        await root.DisposeAsync();

        var record = (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).Value;
        record.Actor.UserId.ShouldBe("user-1");
        record.ModuleName.ShouldBe("Customers");
    }

    [Fact]
    public async Task Correlation_ExplicitChildOverride_PreservesRootAndRestoresAmbient()
    {
        using var host = CreateHost();
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        using var original = CorrelationId.BeginScope("flow-original");
        var root = sut.BeginScope(s => s.Key("root"));
        using (CorrelationId.BeginScope("flow-child"))
        using (var child = sut.BeginScope(s => s.Key("child"))) child.Complete();
        root.Complete();
        await root.DisposeAsync();

        CorrelationId.Current.ShouldBe("flow-original");
        var record = (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).Value;
        record.CorrelationId.ShouldBe("flow-original");
        record.Actions.Single().CorrelationId.ShouldBe("flow-child");
    }

    [Fact]
    public async Task DetachExecution_InheritedRootAndSuppression_OwnsAnIndependentServiceExecution()
    {
        using var host = CreateHost();
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var boundary = services.ServiceProvider.GetRequiredService<IAuditExecutionBoundary>();
        var root = sut.BeginScope(s => s.Key("origin"));
        Guid workerId = default;
        using (sut.Suppress())
        using (boundary.DetachExecution())
        {
            await sut.RunAsync(s => s.Key("worker"), (scope, ct) => { workerId = scope.AuditId; return Task.FromResult(1); });
        }

        sut.Current.ShouldBeSameAs(root);
        root.Complete();
        await root.DisposeAsync();

        workerId.ShouldNotBe(root.AuditId);
        var worker = (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(workerId)).Value;
        worker.Actor.Kind.ShouldBe("Service");
        worker.OriginAuditId.ShouldBe(workerId);
        worker.CausationAuditId.ShouldBeNull();
    }

    [Fact]
    public async Task Expiry_OpenExecution_StopsLateCaptureWithoutCancelingBusinessWork()
    {
        var time = new FakeTimeProvider();
        using var host = CreateHost(s => s.AddSingleton<TimeProvider>(time));
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = sut.BeginScope(s => s.Key("long-running"));
        time.Advance(TimeSpan.FromMinutes(31));
        root.IsRecording.ShouldBeFalse();
        var callbacks = 0;
        using (var late = sut.BeginScope(s => { callbacks++; s.Key("late"); })) late.IsRecording.ShouldBeFalse();
        callbacks.ShouldBe(0);
        await root.DisposeAsync();

        var record = (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).Value;
        record.Completeness.ShouldBe(AuditCompleteness.Incomplete);
        record.Notes.ShouldContain(n => n.Code == "AuditCaptureExpired");
    }

    [Fact]
    public async Task Limits_SecretValueAndActionOverflow_KeepSafeMarkersAndOmissionCounts()
    {
        using var host = CreateHost(configure: o => o.Limits(l => l with { MaxActions = 2, MaxDetailValueBytes = 8 }));
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = sut.BeginScope(s => s.Key("bounded"));
        root.SetDetail("password", AuditValue.From("never-store-this"));
        root.SetDetail("large", AuditValue.From("日本語"));
        for (var i = 0; i < 3; i++) { using var child = sut.BeginScope(s => s.Key("repeat")); child.Complete(); }

        root.Complete();
        await root.DisposeAsync();

        var record = (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).Value;
        record.Actions.Length.ShouldBe(2);
        record.TotalActionCount.ShouldBe(3);
        record.OmittedActionCount.ShouldBe(1);
        record.Details["password"].Marker.ShouldBe("Excluded");
        record.Details["large"].Marker.ShouldBe("Oversized");
        Encoding.UTF8.GetString(AuditRecordCodec.Encode(record)).ShouldNotContain("never-store-this");
    }

    [Fact]
    public async Task DisposeAsync_AsyncSealer_BlocksAdmissionUntilOwnedFinalizationCompletes()
    {
        var sealer = new GatedSealer();
        using var host = CreateHost(s => s.AddSingleton<IAuditRecordSealer>(sealer));
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = sut.BeginScope(s => s.Key("sealed"));
        root.Complete();
        var finalization = root.DisposeAsync();
        sut.Current.ShouldBeNull();
        finalization.IsCompleted.ShouldBeFalse();
        (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).IsFailure.ShouldBeTrue();
        sealer.Release.SetResult();
        await finalization;
        (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_DisabledOrInvalidMetadata_DoesNotRepeatOrReplaceBusinessWork(bool disabled)
    {
        using var host = CreateHost(configure: o => o.Enabled(!disabled));
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var calls = 0;
        var callbacks = 0;
        var result = await sut.RunAsync<int>(_ => { callbacks++; throw new InvalidOperationException("private metadata"); }, (_, _) => Task.FromResult(++calls));
        result.ShouldBe(1);
        calls.ShouldBe(1);
        callbacks.ShouldBe(disabled ? 0 : 1);
        sut.Current.ShouldBeNull();
    }

    [Fact]
    public void Canonicalizer_OrdinalNestedKeys_PreservesArrayAndNumericTokens()
    {
        using var json = JsonDocument.Parse("{\"z\":[2,1.00],\"a\":{\"b\":2,\"A\":1}}");
        Encoding.UTF8.GetString(AuditCanonicalizerV1.Encode(json.RootElement)).ShouldBe("{\"a\":{\"A\":1,\"b\":2},\"z\":[2,1.00]}");
        using var duplicate = JsonDocument.Parse("{\"a\":1,\"a\":2}");
        Should.Throw<JsonException>(() => AuditCanonicalizerV1.Encode(duplicate.RootElement));
    }

    [Fact]
    public void Codec_UnknownNestedAdditions_PreservesOriginalCompleteBytesAndHash()
    {
        var id = Guid.NewGuid();
        var frozen = AuditRecordCodec.Freeze(new AuditRecord { AuditId = id, OriginAuditId = id, Key = "retained", StartedUtc = DateTimeOffset.UtcNow }).Value;
        var json = JsonNode.Parse(AuditRecordCodec.Encode(frozen)).AsObject();
        json["futureRoot"] = "retained";
        json["actor"]["futureActorField"] = 42;
        json.Remove("contentHash");
        json.Remove("integrityProtection");
        using var body = JsonDocument.Parse(json.ToJsonString());
        json["contentHash"] = AuditCanonicalizerV1.Hash(AuditCanonicalizerV1.Encode(body.RootElement));
        json["integrityProtection"] = null;
        using var original = JsonDocument.Parse(json.ToJsonString());
        var bytes = AuditCanonicalizerV1.Encode(original.RootElement);

        var sut = AuditRecordCodec.Decode(bytes);

        sut.IsSuccess.ShouldBeTrue();
        AuditRecordCodec.Encode(sut.Value).ShouldBe(bytes);
        AuditRecordCodec.Freeze(sut.Value with { Key = "tampered" }).IsFailure.ShouldBeTrue();
        AuditRecordCodec.Decode(Encoding.UTF8.GetBytes("{\"schemaVersion\":2}")).Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditSchemaUnsupported");
    }

    [Fact]
    public async Task BeginScopeFor_ObjectAndRegisteredProvider_ApplyOrderedProjectionsUnderSharedPolicy()
    {
        var provider = new CustomerDetailsProvider();
        using var host = CreateHost(s => s.AddSingleton<IAuditDetailsProvider<CustomerDetails>>(provider),
            o => o.Configure(value => value with
            {
                CapturePolicy = new AuditCapturePolicyOptions
                {
                    RedactNames = System.Collections.Immutable.ImmutableHashSet.Create("privateNote"),
                    HashNames = System.Collections.Immutable.ImmutableHashSet.Create("externalReference")
                }
            }));
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = sut.BeginScopeFor(new CustomerDetails(), services.ServiceProvider);
        root.SetDetail("privateNote", AuditValue.From("private-value"));
        root.SetDetail("externalReference", AuditValue.From("external-value"));
        root.Complete();
        await root.DisposeAsync();

        var record = (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).Value;
        record.Details["source"].Data.GetString().ShouldBe("registered");
        record.Details["privateNote"].Marker.ShouldBe("Redacted");
        record.Details["externalReference"].Marker.ShouldBe("Hashed");
        record.Details["externalReference"].Data.GetString().Length.ShouldBe(64);
        record.Details["password"].Marker.ShouldBe("Excluded");
        provider.Calls.ShouldBe(1);
        Encoding.UTF8.GetString(AuditRecordCodec.Encode(record)).ShouldNotContain("private-value");
        Encoding.UTF8.GetString(AuditRecordCodec.Encode(record)).ShouldNotContain("external-value");
        using (sut.Suppress())
        using (sut.BeginScopeFor(new CustomerDetails(), services.ServiceProvider)) { }

        provider.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Contributors_MetadataOnlyOwnershipAndFailure_PreserveBusinessOutcomeAndCloseOnce()
    {
        var contributor = new CompletionContributor();
        using var host = CreateHost(s => s.AddSingleton<IAuditContributor>(contributor));
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = sut.BeginScope(s => s.Key("contributed"));
        root.Complete(AuditOutcome.Rejected);
        await root.DisposeAsync();
        await root.DisposeAsync();

        contributor.Starts.ShouldBe(1);
        contributor.Completions.ShouldBe(1);
        var record = (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).Value;
        record.Outcome.ShouldBe(AuditOutcome.Rejected);
        record.Details["completion"].Data.GetString().ShouldBe("observed");
        record.Notes.ShouldContain(n => n.Code == "AuditContributorFailed");
        Encoding.UTF8.GetString(AuditRecordCodec.Encode(record)).ShouldNotContain("contributor-private-message");
    }

    [Fact]
    public async Task Naming_ConfiguredConventions_ReplaceDerivedNamesButPreserveExplicitOverrides()
    {
        using var host = CreateHost(configure: o => o.Configure(value => value with { NamingConvention = new CustomerNaming() }));
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = sut.BeginScope(s => s.For<Customer>("Rename"));
        root.AddResource<Customer>("C-1");
        using (var child = sut.BeginScope(s => s.For<Customer>().Key("explicit.key").DisplayName("Explicit label"))) child.Complete();
        root.Complete();
        await root.DisposeAsync();

        var record = (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).Value;
        record.Key.ShouldBe("customer.alias.Rename");
        record.DisplayName.ShouldBe("Customer operation");
        record.Resources.Single().Type.ShouldBe("customer-resource");
        record.Resources.Single().ContextType.ShouldBeNull();
        record.Actions.Single().Key.ShouldBe("explicit.key");
        record.Actions.Single().DisplayName.ShouldBe("Explicit label");
    }

    [Fact]
    public async Task RunAsync_ProviderThrows_PreservesSuccessfulResultAndOriginalBusinessException()
    {
        var storage = new ThrowingProvider();
        using var host = CreateHost(s => s.AddSingleton<IAuditingStorageProvider>(storage));
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        (await sut.RunAsync(s => s.Key("success"), (_, _) => Task.FromResult(42))).ShouldBe(42);
        var original = new InvalidOperationException("original business exception");
        var observed = await Should.ThrowAsync<InvalidOperationException>(() => sut.RunAsync<int>(s => s.Key("failure"), (_, _) => Task.FromException<int>(original)));
        observed.ShouldBeSameAs(original);
        storage.Calls.ShouldBe(2);
        sut.Current.ShouldBeNull();
    }

    [Fact]
    public async Task Detail_RepeatedReplacement_AccountsForRetainedBytesRatherThanCumulativeWrites()
    {
        using var host = CreateHost();
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = sut.BeginScope(s => s.Key("replacements"));
        for (var i = 0; i < 20_000; i++) root.SetDetail("count", AuditValue.From((long)i));

        root.Complete();
        await root.DisposeAsync();
        var record = (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).Value;
        record.Details["count"].Data.GetInt64().ShouldBe(19_999);
        record.OmittedDetailCount.ShouldBe(0);
    }

    [Fact]
    public async Task ActiveRootLimit_IndependentAdmission_RejectsNewRootsWithoutMetadataCallbacks()
    {
        using var host = CreateHost(configure: o => o.Limits(l => l with { MaxActiveRoots = 1 }));
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var boundary = services.ServiceProvider.GetRequiredService<IAuditExecutionBoundary>();
        var root = sut.BeginScope(s => s.Key("root"));
        var callbacks = 0;
        using (boundary.DetachExecution())
        using (var rejected = sut.BeginScope(s => { callbacks++; s.Key("second"); })) rejected.IsRecording.ShouldBeFalse();

        callbacks.ShouldBe(0);
        root.Complete();
        await root.DisposeAsync();
        sut.Current.ShouldBeNull();
    }

    [Fact]
    public void Architecture_CoreReferences_DoNotRequireWebOrModuleHosting()
    {
        var references = typeof(AuditContext).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        references.ShouldNotContain("BridgingIT.DevKit.Common.Modules");
        references.ShouldNotContain(name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) &&
            name != "Microsoft.AspNetCore.DataProtection" && name != "Microsoft.AspNetCore.DataProtection.Abstractions");
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "bITdevKit.slnx"))) repository = repository.Parent;
        repository.ShouldNotBeNull();
        var project = System.Xml.Linq.XDocument.Load(Path.Combine(repository.FullName, "src", "Application.Auditing", "Application.Auditing.csproj"));
        project.Descendants("FrameworkReference").ShouldNotContain(reference => (string)reference.Attribute("Include") == "Microsoft.AspNetCore.App");
    }

    [Fact]
    public async Task Expiry_FinalizingRoot_KeepsItsCapacityUntilSealingFinishes()
    {
        var time = new FakeTimeProvider();
        var sealer = new GatedSealer();
        using var host = CreateHost(s => { s.AddSingleton<TimeProvider>(time); s.AddSingleton<IAuditRecordSealer>(sealer); },
            o => o.Configure(value => value with { FinalizationTimeout = TimeSpan.FromMinutes(1) }).Limits(l => l with { MaxActiveRoots = 1, MaxLifetime = TimeSpan.FromSeconds(1) }));
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = sut.BeginScope(s => s.Key("sealing"));
        root.Complete();
        var finalization = root.DisposeAsync();
        time.Advance(TimeSpan.FromSeconds(2));
        var callbacks = 0;
        using (var rejected = sut.BeginScope(s => { callbacks++; s.Key("overflow"); })) rejected.IsRecording.ShouldBeFalse();

        callbacks.ShouldBe(0);
        finalization.IsCompleted.ShouldBeFalse();
        sealer.Release.SetResult();
        await finalization;
        (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Expiry_BeforeMaintenanceTick_StopsCaptureAtTheConfiguredLifetime()
    {
        var time = new FakeTimeProvider();
        using var host = CreateHost(s => s.AddSingleton<TimeProvider>(time), o => o.Limits(l => l with { MaxLifetime = TimeSpan.FromMilliseconds(100) }));
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = sut.BeginScope(s => s.Key("short-lived"));
        time.Advance(TimeSpan.FromMilliseconds(200));
        root.IsRecording.ShouldBeFalse();
        root.SetDetail("late", AuditValue.From("unobserved"));
        await root.DisposeAsync();
        var record = (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).Value;
        record.Details.ShouldBeEmpty();
        record.Notes.ShouldContain(n => n.Code == "AuditCaptureExpired");
    }

    [Fact]
    public async Task DisposeAsync_OutOfOrderRequiresNew_PreservesInnerCorrelationAndRestoresOuterAmbient()
    {
        using var cleared = CorrelationId.BeginScope(null);
        using var host = CreateHost();
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var outer = sut.BeginScope(s => s.Key("outer"));
        var correlation = CorrelationId.Current;
        var inner = sut.BeginScope(s => s.Key("inner").Mode(AuditScopeMode.RequiresNew));
        outer.Complete();
        await outer.DisposeAsync();
        CorrelationId.Current.ShouldBe(correlation);
        using (var action = sut.BeginScope(s => s.Key("after.outer"))) action.Complete();
        inner.Complete();
        await inner.DisposeAsync();
        CorrelationId.Current.ShouldBeNull();
        var record = (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(inner.AuditId)).Value;
        record.Actions.Single().CorrelationId.ShouldBe(correlation);
    }

    [Fact]
    public async Task Detail_DuplicateJsonAndNestedSecret_DoNotPoisonOtherwiseValidRoot()
    {
        using var host = CreateHost();
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = sut.BeginScope(s => s.Key("projection"));
        root.SetDetail("valid", AuditValue.From("retained"));
        using var duplicate = JsonDocument.Parse("{\"x\":1,\"x\":2}");
        root.SetDetail("duplicate", AuditValue.Json(duplicate.RootElement));
        using var nested = JsonDocument.Parse("{\"nested\":{\"password\":\"nested-private-secret\"}}");
        root.SetDetail("json", AuditValue.Json(nested.RootElement));
        root.Complete();
        await root.DisposeAsync();
        var result = await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId);
        result.IsSuccess.ShouldBeTrue();
        result.Value.Details["valid"].Data.GetString().ShouldBe("retained");
        result.Value.Details.ContainsKey("duplicate").ShouldBeFalse();
        result.Value.Details["json"].Marker.ShouldBe("Excluded");
        result.Value.Notes.ShouldContain(n => n.Code == "AuditDetailProjectionFailed");
        Encoding.UTF8.GetString(AuditRecordCodec.Encode(result.Value)).ShouldNotContain("nested-private-secret");
    }

    [Fact]
    public async Task DisposeAsync_OpenChild_ReleasesItsContributionClosureBeforeChildDisposal()
    {
        using var host = CreateHost(s => s.AddSingleton<IAuditContributor>(new CompletionContributor()));
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = sut.BeginScope(s => s.Key("root"));
        var child = sut.BeginScope(s => s.Key("open-child"));
        root.Complete();
        await root.DisposeAsync();
        // This checks the private lifetime invariant that no public projection exposes.
        var callback = child.GetType().GetField("completing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        callback.GetValue(child).ShouldBeNull();
        child.Dispose();
    }

    [Fact]
    public async Task Detail_DeepJson_IsMarkedOversizedWithoutDroppingTheRoot()
    {
        using var host = CreateHost();
        using var services = host.CreateScope();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = sut.BeginScope(s => s.Key("deep-detail"));
        using var json = JsonDocument.Parse(new string('[', 62) + "0" + new string(']', 62));
        root.SetDetail("deep", AuditValue.Json(json.RootElement));
        root.SetDetail("valid", AuditValue.From(42L));
        root.Complete();
        await root.DisposeAsync();
        var result = await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId);
        result.IsSuccess.ShouldBeTrue();
        result.Value.Details["deep"].Marker.ShouldBe("Oversized");
        result.Value.Details["valid"].Data.GetInt64().ShouldBe(42);
        result.Value.Completeness.ShouldBe(AuditCompleteness.Truncated);
    }

    [Fact]
    public async Task RequiresNewInsideLinkedWorker_UsesTheImmediateWorkerActionAsCause()
    {
        using var host = CreateHost();
        using var services = host.CreateScope();
        var context = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var origin = context.BeginScope(builder => builder.Key("customers.initiate"));
        var link = ((IAuditSourceScope)origin).CaptureLink();
        Guid workerId;
        Guid independentId;
        Guid? immediateAction;
        using (services.ServiceProvider.GetRequiredService<IAuditExecutionBoundary>().DetachExecution(link))
        {
            var worker = context.BeginScope(builder => builder.Key("customers.worker"));
            workerId = worker.AuditId;
            using (var action = context.BeginScope(builder => builder.Key("customers.parent-action")))
            {
                immediateAction = action.ActionId;
                var independent = context.BeginScope(builder => builder.Key("customers.independent").Mode(AuditScopeMode.RequiresNew));
                independentId = independent.AuditId;
                independent.Complete();
                await independent.DisposeAsync();
                action.Complete();
            }

            worker.Complete();
            await worker.DisposeAsync();
        }

        context.Current.ShouldBeSameAs(origin);
        origin.Complete();
        await origin.DisposeAsync();
        var record = (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(independentId)).Value;
        record.OriginAuditId.ShouldBe(origin.AuditId);
        record.CausationAuditId.ShouldBe(workerId);
        record.CausationActionId.ShouldBe(immediateAction);
        record.Actor.Kind.ShouldBe("Service");
        record.InitiatingActor.Kind.ShouldBe("Anonymous");
    }

    [Fact]
    public async Task RunAsync_InvalidHashInMetadata_PreservesBusinessExecutionAndReportsCaptureFailure()
    {
        using var host = CreateHost();
        using var services = host.CreateScope();
        var audit = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var calls = 0;

        var result = await audit.RunAsync(builder => builder.Describe(AuditDescriptorBuilder.For<Customer>()
            .Detail("reference", AuditValue.Policy("Hashed", "raw")).Build()), (_, _) =>
        {
            calls++;
            return Task.FromResult(42);
        });

        result.ShouldBe(42);
        calls.ShouldBe(1);
        host.GetRequiredService<AuditRuntime>().SnapshotFailures()["AuditConfigurationFailed"].ShouldBe(1);
    }

    private static ServiceProvider CreateHost(Action<IServiceCollection> services = null, Action<AuditingOptionsBuilder> configure = null)
    {
        var collection = new ServiceCollection();
        services?.Invoke(collection);
        collection.AddAuditing(configure);
        return collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private sealed class Customer;
    private sealed class CustomerDetails : IAuditDetails
    {
        public AuditDescriptor DescribeAudit() => AuditDescriptorBuilder.For<CustomerDetails>().Detail("source", AuditValue.From("object")).Detail("password", AuditValue.From("password-private-value")).Build();
    }
    private sealed class CustomerDetailsProvider : IAuditDetailsProvider<CustomerDetails>
    {
        internal int Calls { get; private set; }
        public AuditDescriptor DescribeAudit(CustomerDetails value)
        {
            this.Calls++;
            return AuditDescriptorBuilder.For<CustomerDetails>().Detail("source", AuditValue.From("registered")).Build();
        }
    }
    private sealed class CompletionContributor : IAuditContributor
    {
        internal int Starts { get; private set; }
        internal int Completions { get; private set; }
        public void OnStarting(IAuditScope scope, IServiceProvider services)
        {
            this.Starts++;
            scope.Dispose();
            scope.Complete();
        }
        public void OnCompleted(IAuditScope scope, IServiceProvider services)
        {
            this.Completions++;
            scope.SetDetail("completion", AuditValue.From("observed"));
            throw new InvalidOperationException("contributor-private-message");
        }
    }
    private sealed class CustomerNaming : AuditNamingConvention
    {
        public override string GetKey(Type type, string operation = null) => "customer.alias" + (operation is null ? "" : "." + operation);
        public override string GetDisplayName(Type type, string operation = null) => "Customer operation";
        public override string GetResourceType(Type type) => "customer-resource";
    }
    private sealed class ThrowingProvider : IAuditingStorageProvider
    {
        internal int Calls { get; private set; }
        public Task<Result<AuditAppendReceipt>> AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
        {
            this.Calls++;
            return Task.FromException<Result<AuditAppendReceipt>>(new IOException("private storage failure"));
        }
        public Task<Result<AuditRecord>> FindAsync(Guid auditId, CancellationToken cancellationToken = default) => Task.FromResult(Result<AuditRecord>.Failure());
        public Task<Result<AuditFlushResult>> FlushAsync(CancellationToken cancellationToken = default) => Task.FromResult(Result<AuditFlushResult>.Success(new()));
    }
    private sealed class GatedSealer : IAuditRecordSealer
    {
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<Result<AuditRecord>> SealAsync(AuditRecord record, CancellationToken cancellationToken = default)
        {
            await this.Release.Task.WaitAsync(cancellationToken);
            return Result<AuditRecord>.Success(record);
        }
    }
}
