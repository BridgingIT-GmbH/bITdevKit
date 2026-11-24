// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

/// <summary>Checks provider-owned tracked invalidation admission, checkpoints, cancellation, resume, and bounded history.</summary>
public class CacheOperationTests
{
    /// <summary>Acceptance is only a request; confirmed continuation units count their incremental effects once across an aggregate checkpoint.</summary>
    [Fact]
    public async Task TrackedDiagnostics_SeparateAcceptanceFromCommittedUnitsAndDoNotDoubleCountEffects()
    {
        await using var fixture = new CacheBackgroundFixture(b => b.WithCollection<string>("cards"));
        for (var index = 0; index < 260; index++) { (await fixture.Collection().SetAsync(new("key:" + index), "old")).IsSuccess.ShouldBeTrue(); }

        var observations = fixture.Services.GetRequiredService<CacheDiagnostics>().ForCollection("cards");
        var operations = fixture.Services.GetRequiredService<CacheOperationService>();
        var accepted = (await operations.SubmitAsync(new(["cards"], CacheSelection.None, Clear: true, Background: true))).Value.Single().Value;
        observations.Snapshot().Counters[CacheObservationKind.InvalidationRequested].ShouldBe(1);
        observations.Snapshot().Counters[CacheObservationKind.InvalidationCompleted].ShouldBe(0);
        observations.Snapshot().Counters[CacheObservationKind.InvalidatedEntries].ShouldBe(0);
        var runner = fixture.Services.GetRequiredService<CacheOperationRunner>();
        var binding = fixture.Catalog.Collections["cards"];
        var first = await runner.RunUnitAsync(binding, accepted.Handle.Id);
        first.IsSuccess.ShouldBeTrue();
        first.Value.Outcome.Completed.ShouldBeFalse();
        observations.Snapshot().Counters[CacheObservationKind.InvalidationCompleted].ShouldBe(1);
        observations.Snapshot().Counters[CacheObservationKind.InvalidatedEntries].ShouldBe(256);
        var second = await runner.RunUnitAsync(binding, accepted.Handle.Id);
        second.Value.State.ShouldBe(CacheOperationState.Completed);
        (await runner.RunUnitAsync(binding, accepted.Handle.Id)).Value.State.ShouldBe(CacheOperationState.Completed);
        observations.Snapshot().Counters[CacheObservationKind.InvalidationCompleted].ShouldBe(2);
        observations.Snapshot().Counters[CacheObservationKind.InvalidatedEntries].ShouldBe(260);
    }

    /// <summary>Background acceptance reserves a pending record before any entries are invalidated.</summary>
    [Fact]
    public async Task BackgroundSubmission_ReservesStatusWithoutMutatingEntries()
    {
        await using var fixture = new CacheBackgroundFixture(b => b.WithCollection<string>("cards"));
        await fixture.Collection().SetAsync(new("key"), "value");
        var sut = fixture.Services.GetRequiredService<CacheOperationService>();
        var accepted = (await sut.SubmitAsync(new(["cards"], CacheSelection.None, Clear: true, Background: true))).Value.Single().Value;

        accepted.Accepted.ShouldBeTrue();
        accepted.Status.State.ShouldBe(CacheOperationState.Pending);
        (await fixture.Collection().GetAsync("key")).Value.Found.ShouldBeTrue();
        (await sut.GetStatusAsync(accepted.Handle)).Value.Id.ShouldBe(accepted.Handle.Id);
    }

    /// <summary>Cancelled work resumes its fixed checkpoint and does not remove a newer replacement.</summary>
    [Fact]
    public async Task CancelAndResume_ContinuesCapturedCutoffWithoutReplayingCompletedEffects()
    {
        await using var fixture = new CacheBackgroundFixture(b => b.WithCollection<string>("cards"));
        for (var index = 0; index < 270; index++) { (await fixture.Collection().SetAsync(new("key:" + index), "old")).IsSuccess.ShouldBeTrue(); }

        var sut = fixture.Services.GetRequiredService<CacheOperationService>();
        var accepted = (await sut.SubmitAsync(new(["cards"], CacheSelection.None, Clear: true, Background: true))).Value.Single().Value;
        var binding = fixture.Catalog.Collections["CARDS"];
        var runner = fixture.Services.GetRequiredService<CacheOperationRunner>();
        var first = (await runner.RunUnitAsync(binding, accepted.Handle.Id)).Value;
        first.State.ShouldBe(CacheOperationState.Pending);
        first.Outcome.AffectedCount.ShouldBe(256);
        (await sut.CancelAsync(accepted.Handle)).Value.State.ShouldBe(CacheOperationState.Cancelled);
        (await sut.CancelAsync(accepted.Handle)).Value.State.ShouldBe(CacheOperationState.Cancelled);
        var remaining = (await fixture.Provider.QueryAsync(new("workers", "cards", CacheSelection.Pattern("*"), fixture.Time.GetUtcNow()))).Value.Entries;
        var replaced = remaining[0].DisplayKey;
        await fixture.Collection().SetAsync(new(replaced), "newer");
        var resumed = (await sut.ResumeAsync(accepted.Handle)).Value;
        resumed.Outcome.Continuation.ShouldBe(first.Outcome.Continuation);
        resumed.CancellationRequested.ShouldBeFalse();
        var finished = (await runner.RunUnitAsync(binding, accepted.Handle.Id)).Value;

        finished.State.ShouldBe(CacheOperationState.Completed);
        finished.Outcome.AffectedCount.ShouldBe(269);
        (await fixture.Collection().GetAsync(replaced)).Value.Value.ShouldBe("newer");
        (await sut.ResumeAsync(accepted.Handle)).Value.State.ShouldBe(CacheOperationState.Completed);
    }

    /// <summary>Active history exhaustion rejects new work before mutation and completed records can be reclaimed.</summary>
    [Fact]
    public async Task OperationCapacity_ProtectsActiveRecordsAndReclaimsTerminalHistory()
    {
        await using var fixture = new CacheBackgroundFixture(b => b.WithCollection<string>("cards")
            .WithOptions(o => o.With(x => x.Diagnostics.MaximumOperationRecords, 1)));
        await fixture.Collection().SetAsync(new("key"), "value");
        var sut = fixture.Services.GetRequiredService<CacheOperationService>();
        var first = (await sut.SubmitAsync(new(["cards"], CacheSelection.None, Clear: true, Background: true))).Value.Single().Value;
        var rejected = await sut.SubmitAsync(new(["cards"], CacheSelection.None, Clear: true, Background: true));
        rejected.Value.Single().IsFailure.ShouldBeTrue();
        (await fixture.Collection().GetAsync("key")).Value.Found.ShouldBeTrue();
        await fixture.Services.GetRequiredService<CacheOperationRunner>().RunUnitAsync(fixture.Catalog.Collections["CARDS"], first.Handle.Id);
        (await sut.SubmitAsync(new(["cards"], CacheSelection.None, Clear: true, Background: true))).Value.Single().IsSuccess.ShouldBeTrue();
        (await sut.GetStatusAsync(first.Handle)).IsFailure.ShouldBeTrue();
    }

    /// <summary>Controlled unit failures retain a non-sensitive classification and resume clears it without replacing the handle.</summary>
    [Fact]
    public async Task FailedUnit_RetainsFailureCodeAndResumeClearsIt()
    {
        var native = Substitute.For<ICacheStoreProvider>();
        native.Name.Returns("FAILING");
        native.Capabilities.Returns(InMemoryCacheStoreProvider.DefaultCapabilities);
        native.InitializeAsync(Arg.Any<IReadOnlyList<CacheProviderCollection>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(Result.Success()));
        CacheProviderOperation stored = null;
        native.SaveOperationAsync(Arg.Any<CacheProviderOperation>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            stored = call.ArgAt<CacheProviderOperation>(0) with { Revision = Guid.NewGuid().ToString("N") };
            stored = stored with { Request = stored.Request with { Namespace = CacheIdentityNormalizer.Namespace(stored.Request.Namespace) } };
            return ValueTask.FromResult(Result<CacheProviderOperation>.Success(stored));
        });
        native.GetOperationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(Result<CacheProviderOperation>.Success(stored)));
        native.InvalidateAsync(Arg.Any<CacheProviderInvalidation>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(Result<CacheInvalidationOutcome>.Failure(new CacheError(CacheErrorCode.Validation, "The bounded unit failed validation."))));
        await using var fixture = new CacheBackgroundFixture(b => b.WithProviderKind("Faulting", native.Capabilities, (_, _, _) => native)
            .WithProvider("failing", p => p.With(x => x.Kind, "Faulting"))
            .WithCollection<string>("cards", c => c.With(x => x.Provider, "failing")));
        var sut = fixture.Services.GetRequiredService<CacheOperationService>();
        var submitted = (await sut.SubmitAsync(new(["cards"], CacheSelection.None, Clear: true))).Value.Single().Value;

        submitted.Status.State.ShouldBe(CacheOperationState.Failed);
        submitted.Status.FailureCode.ShouldBe(CacheErrorCode.Validation);
        var observations = fixture.Services.GetRequiredService<CacheDiagnostics>().ForCollection("cards").Snapshot();
        observations.Counters[CacheObservationKind.InvalidationRequested].ShouldBe(1);
        observations.Counters[CacheObservationKind.InvalidationCompleted].ShouldBe(0);
        observations.Counters[CacheObservationKind.InvalidationFailed].ShouldBe(1);
        observations.Counters[CacheObservationKind.InvalidatedEntries].ShouldBe(0);
        var resumed = (await sut.ResumeAsync(submitted.Handle)).Value;
        resumed.Id.ShouldBe(submitted.Handle.Id);
        resumed.State.ShouldBe(CacheOperationState.Pending);
        resumed.FailureCode.ShouldBeNull();
    }

    /// <summary>Unknown handles remain unavailable and never create implicit work.</summary>
    [Fact]
    public async Task UnknownHandle_IsUnavailableForStatusAndControls()
    {
        await using var fixture = new CacheBackgroundFixture(b => b.WithCollection<string>("cards"));
        var sut = fixture.Services.GetRequiredService<CacheOperationService>();
        var handle = new CacheOperationHandle("memory", "cards", Guid.NewGuid().ToString("N"));
        (await sut.GetStatusAsync(handle)).IsFailure.ShouldBeTrue();
        (await sut.ResumeAsync(handle)).IsFailure.ShouldBeTrue();
        (await sut.CancelAsync(handle)).IsFailure.ShouldBeTrue();
    }

    /// <summary>Create-only permits replacing confirmed absence but treats positive null and empty values as present.</summary>
    [Fact]
    public async Task CreateOnly_ReplacesAbsenceButCannotReplacePositiveNullOrEmpty()
    {
        await using var fixture = new CacheBackgroundFixture(b => b.WithCollection<string>("cards", c => c.With(x => x.NegativeCaching.Enabled, true)));
        var sut = fixture.Collection();
        await sut.GetOrLoadAsync(new("key"), (_, _) => ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(CacheLoadResult<string>.NotFound())));
        (await sut.SetAsync(new("key"), null, new CacheWriteCondition(CreateOnly: true))).IsSuccess.ShouldBeTrue();
        (await sut.SetAsync(new("key"), "replacement", new CacheWriteCondition(CreateOnly: true))).IsFailure.ShouldBeTrue();
        await sut.SetAsync(new("empty"), "");
        (await sut.SetAsync(new("empty"), "replacement", new CacheWriteCondition(CreateOnly: true))).IsFailure.ShouldBeTrue();
    }
}