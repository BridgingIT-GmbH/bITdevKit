// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Resolves trusted read scope once, validates all client input, and suppresses its own audit activity.</summary>
/// <example><code>var page = await queries.SearchAsync(filter, cancellationToken);</code></example>
public sealed class AuditQueryService(IServiceProvider services, IAuditContext context, IAuditingStorageProvider provider,
    AuditSearchPlanBuilder builder, AuditFieldCatalog catalog, AuditReadProjection projection, AuditQueryOptions options, AuditingOptions capture, AuditIntegrityReadPolicy integrity) : IAuditQueryService
{
    /// <inheritdoc />
    public Task<ResultPaged<AuditRecordSummary>> SearchAsync(FilterModel filter = null, CancellationToken cancellationToken = default) => this.RunAsync(async token =>
    {
        var plan = await this.BuildAsync(filter, token).ConfigureAwait(false);
        return plan.IsSuccess ? await provider.SearchAsync(plan.Value, token).ConfigureAwait(false) : ResultPaged<AuditRecordSummary>.Failure(plan.Messages, plan.Errors);
    }, error => ResultPaged<AuditRecordSummary>.Failure(error), cancellationToken);

    /// <inheritdoc />
    public Task<Result<AuditRecordDetails>> FindAsync(Guid auditId, CancellationToken cancellationToken = default) => this.RunAsync(async token =>
    {
        var scope = await this.ResolveAsync(token).ConfigureAwait(false);
        if (scope.IsFailure) return Result<AuditRecordDetails>.Failure(scope);
        var stored = await provider.FindStoredAsync(auditId, scope.Value.Predicate, token).ConfigureAwait(false);
        if (stored.IsFailure) return Result<AuditRecordDetails>.Failure(stored);
        var verification = await integrity.VerifyOriginalAsync(stored.Value.Record, token).ConfigureAwait(false);
        if (verification.IsFailure) return Result<AuditRecordDetails>.Failure(verification);
        if (verification.Value is not ("Verified" or "NotProtected")) return Result<AuditRecordDetails>.Failure(new AuditError("AuditIntegrityInvalid"));
        return Result<AuditRecordDetails>.Success(projection.Apply(stored.Value, verification.Value));
    }, error => Result<AuditRecordDetails>.Failure(error), cancellationToken);

    /// <inheritdoc />
    public Task<ResultPaged<AuditRecordSummary>> FindRelatedAsync(Guid auditId, FilterModel filter = null, CancellationToken cancellationToken = default) => this.RunAsync(async token =>
    {
        var scope = await this.ResolveAsync(token).ConfigureAwait(false);
        if (scope.IsFailure) return ResultPaged<AuditRecordSummary>.Failure(scope.Messages, scope.Errors);
        var anchor = await provider.FindStoredAsync(auditId, scope.Value.Predicate, token).ConfigureAwait(false);
        if (anchor.IsFailure) return ResultPaged<AuditRecordSummary>.Failure(anchor.Messages, anchor.Errors);
        var plan = builder.Build(filter, scope.Value.Predicate, withRange: false, scopeFingerprint: scope.Value.Fingerprint);
        if (plan.IsFailure) return ResultPaged<AuditRecordSummary>.Failure(plan.Messages, plan.Errors);
        return await provider.FindRelatedAsync(new AuditRelatedExecutionsQuery
        {
            Selection = plan.Value, AnchorAuditId = auditId, OriginAuditId = anchor.Value.Record.OriginAuditId,
            ParentIds = anchor.Value.Record.CausationAuditId is Guid parent ? [parent] : []
        }, token).ConfigureAwait(false);
    }, error => ResultPaged<AuditRecordSummary>.Failure(error), cancellationToken);

    /// <inheritdoc />
    public Task<Result<ImmutableArray<AuditAggregateItem>>> AggregateAsync(FilterModel filter, string field, int limit = 100, CancellationToken cancellationToken = default) => this.RunAsync(async token =>
    {
        if (!this.ApprovedGrouping(field, false)) return Result<ImmutableArray<AuditAggregateItem>>.Failure(new AuditError("AuditFieldUnsupported"));
        var plan = await this.BuildAsync(filter, token).ConfigureAwait(false);
        return plan.IsSuccess ? await provider.AggregateAsync(new() { Selection = plan.Value, Field = field, Limit = limit }, token).ConfigureAwait(false) : Result<ImmutableArray<AuditAggregateItem>>.Failure(plan);
    }, error => Result<ImmutableArray<AuditAggregateItem>>.Failure(error), cancellationToken);

    /// <inheritdoc />
    public Task<Result<ImmutableArray<string>>> FindFacetsAsync(FilterModel filter, string field, string prefix = null, int limit = 20, CancellationToken cancellationToken = default) => this.RunAsync(async token =>
    {
        if (!this.ApprovedGrouping(field, true)) return Result<ImmutableArray<string>>.Failure(new AuditError("AuditFieldUnsupported"));
        var plan = await this.BuildAsync(filter, token).ConfigureAwait(false);
        return plan.IsSuccess ? await provider.FindFacetsAsync(new() { Selection = plan.Value, Field = field, Prefix = prefix, Limit = limit }, token).ConfigureAwait(false) : Result<ImmutableArray<string>>.Failure(plan);
    }, error => Result<ImmutableArray<string>>.Failure(error), cancellationToken);

    /// <inheritdoc />
    public Task<ResultPaged<AuditResourceHistoryItem>> GetResourceHistoryAsync(AuditResourceReference resource, FilterModel filter = null, CancellationToken cancellationToken = default) => this.RunAsync(async token =>
    {
        if (resource is null) return ResultPaged<AuditResourceHistoryItem>.Failure(new AuditError("AuditResourceInvalid"));
        var plan = await this.BuildAsync(filter, token).ConfigureAwait(false);
        return plan.IsSuccess ? await provider.GetResourceHistoryAsync(plan.Value, resource, token).ConfigureAwait(false) : ResultPaged<AuditResourceHistoryItem>.Failure(plan.Messages, plan.Errors);
    }, error => ResultPaged<AuditResourceHistoryItem>.Failure(error), cancellationToken);

    /// <inheritdoc />
    public Task<Result<AuditSearchPlan>> PrepareAsync(FilterModel filter = null, CancellationToken cancellationToken = default) => this.RunAsync(
        token => this.BuildAsync(filter, token), error => Result<AuditSearchPlan>.Failure(error), cancellationToken);

    /// <inheritdoc />
    public Task<Result<AuditSearchPlan>> PrepareAtAsync(FilterModel filter, DateTimeOffset asOfUtc, CancellationToken cancellationToken = default) => this.RunAsync(
        token => this.BuildAsync(filter, token, asOfUtc), error => Result<AuditSearchPlan>.Failure(error), cancellationToken);

    private async Task<Result<AuditSearchPlan>> BuildAsync(FilterModel filter, CancellationToken token, DateTimeOffset? asOfUtc = null)
    {
        var scope = await this.ResolveAsync(token).ConfigureAwait(false);
        return scope.IsSuccess ? builder.Build(filter, scope.Value.Predicate, scopeFingerprint: scope.Value.Fingerprint, asOfUtc: asOfUtc, unrestrictedScope: scope.Value.IsUnrestricted) : Result<AuditSearchPlan>.Failure(scope);
    }

    private async Task<Result<ResolvedScope>> ResolveAsync(CancellationToken token)
    {
        var resolver = services.GetService<AuditReadScopeResolver>();
        Result<AuditReadScope> resolved;
        try
        {
            resolved = resolver is null ? Result<AuditReadScope>.Success(AuditReadScope.Unrestricted())
                : await resolver.ResolveAsync(services, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return Result<ResolvedScope>.Failure(new AuditError("AuditReadScopeUnavailable")); }

        if (resolved.IsFailure || resolved.Value is null) return Result<ResolvedScope>.Failure(new AuditError("AuditReadScopeUnavailable"));
        var scope = resolved.Value;
        var constraints = scope.IsUnrestricted ? Result<AuditPredicate>.Success(new()) : builder.CompileConstraints(scope.Constraints);
        if (constraints.IsFailure || !scope.IsUnrestricted && constraints.Value.Kind == "True") return Result<ResolvedScope>.Failure(new AuditError("AuditReadScopeInvalid"));
        var fingerprint = AuditCanonicalizerV1.Hash(AuditCanonicalizerV1.Encode(JsonSerializer.SerializeToElement(new { scope.Revision, scope.IsUnrestricted, constraints.Value })));
        return Result<ResolvedScope>.Success(new(constraints.Value, fingerprint, scope.IsUnrestricted));
    }

    private bool ApprovedGrouping(string name, bool stringOnly)
    {
        var field = catalog.Find("Record", name);
        return field.IsSuccess && field.Value.Kind != "Collection" && (!stringOnly || field.Value.Kind == "String") &&
            (field.Value.Detail is null || field.Value.IsMarker || field.Value.Detail.ExposeValue &&
                !AuditCapturePolicy.MatchesName(capture.CapturePolicy.ExcludeNames, field.Value.Detail.Name) && !AuditCapturePolicy.MatchesName(capture.CapturePolicy.RedactNames, field.Value.Detail.Name) && !AuditCapturePolicy.MatchesName(capture.CapturePolicy.HashNames, field.Value.Detail.Name));
    }

    private async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation, Func<AuditError, T> failure, CancellationToken token)
    {
        using var suppression = context.Suppress();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(options.Timeout);
        try { return await operation(deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return failure(new AuditError("AuditQueryTimeout")); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return failure(new AuditError("AuditReadUnavailable")); }
    }

    private sealed record ResolvedScope(AuditPredicate Predicate, string Fingerprint, bool IsUnrestricted);
}
