// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Text;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Validates bounded host metadata before any management provider, stream, or mutation access.</summary>
/// <example><code>var validation = new CacheAccessContextValidator().Validate(access);</code></example>
public sealed class CacheAccessContextValidator : AbstractValidator<CacheAccessContext>
{
    /// <summary>Defines finite host identity, origin, and action contracts.</summary>
    public CacheAccessContextValidator()
    {
        this.RuleFor(value => value.Namespace).NotEmpty().MaximumLength(256);
        this.RuleFor(value => value.SubjectId).NotEmpty().MaximumLength(512);
        this.RuleFor(value => value.Origin).NotEmpty().MaximumLength(128);
        this.RuleFor(value => value.Actions).Must(value => (value & ~(CacheAccessActions.Read | CacheAccessActions.Write | CacheAccessActions.Invalidate)) == 0);
    }
}

/// <summary>Handles the common bounded cache requests used by independent HTTP, Dashboard, Console, and MCP adapters.</summary>
/// <example><code>var response = await requester.SendAsync(new CacheCollectionsQuery(access));</code></example>
public sealed class CacheManagementHandler(CacheCatalog catalog, CacheCollectionAccessRegistry registry,
    IServiceProvider services, TimeProvider time) :
    IRequestHandler<CacheCollectionsQuery, IReadOnlyList<CacheCollectionInfo>>,
    IRequestHandler<CacheEntriesQuery, CacheEntriesPage>,
    IRequestHandler<CacheEntryMetadataQuery, CacheLookup<CacheEntryMetadata>>,
    IRequestHandler<CacheEntryReadQuery, CacheLookup<ICacheReadHandle>>,
    IRequestHandler<CacheEntryWriteCommand, CacheWriteOutcome>,
    IRequestHandler<CacheEntryRemoveCommand, bool>,
    IRequestHandler<CacheInvalidationPreviewQuery, CacheInvalidationPreview>,
    IRequestHandler<CacheInvalidateCommand, IReadOnlyList<Result<CacheOperationSubmission>>>,
    IRequestHandler<CacheOperationCommand, CacheProviderOperation>,
    IRequestHandler<CacheBackgroundCommand, CacheBackgroundState>,
    IRequestHandler<CacheDiagnosticsQuery, CacheDiagnosticsReport>,
    IRequestHandler<CacheMetricHistoryQueryRequest, CacheMetricHistoryResult>,
    IRequestHandler<CacheMetricSeriesQuery, IReadOnlyList<CacheMetricSeriesDescriptor>>
{
    /// <inheritdoc />
    public Task<Result<IReadOnlyList<CacheCollectionInfo>>> HandleAsync(CacheCollectionsQuery request, SendOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var authorized = this.Authorize(request.Access, null, CacheAccessActions.Read, allowGlobal: true);
        if (authorized.IsFailure) { return Task.FromResult(Result<IReadOnlyList<CacheCollectionInfo>>.Failure(authorized)); }

        IReadOnlyList<CacheCollectionInfo> result = catalog.Collections.Values.Where(binding => Allowed(request.Access, binding.Name))
            .OrderBy(binding => binding.Name, StringComparer.OrdinalIgnoreCase).Select(binding => new CacheCollectionInfo(binding.Name,
                binding.Contract.Alias, binding.Provider.Name, binding.Provider.Kind.Alias, binding.Contract.IsBinary,
                RedactPolicy(binding.Options, request.Access), binding.Provider.Capabilities)).ToArray();
        return Task.FromResult(Result<IReadOnlyList<CacheCollectionInfo>>.Success(result));
    }

    /// <inheritdoc />
    public async Task<Result<CacheEntriesPage>> HandleAsync(CacheEntriesQuery request, SendOptions options, CancellationToken cancellationToken)
    {
        var authorized = this.Authorize(request.Access, request.Collection, CacheAccessActions.Read);
        if (authorized.IsFailure) { return Result<CacheEntriesPage>.Failure(authorized); }

        var observedAt = time.GetUtcNow();
        var cutoff = request.Cutoff ?? observedAt;
        if ((request.Continuation is not null && !request.Cutoff.HasValue) || cutoff > observedAt)
        {
            return Result<CacheEntriesPage>.Failure(new CacheError(CacheErrorCode.Validation, "A continuation requires its original publication cutoff."));
        }

        var result = await registry.Get(request.Collection).QueryAsync(request.Selection, request.Take, request.Continuation, cancellationToken, cutoff).ConfigureAwait(false);
        return result.IsFailure ? Result<CacheEntriesPage>.Failure(result) : Result<CacheEntriesPage>.Success(new(
            result.Value.Entries.Select(entry => Redact(entry, request.Access)).ToArray(), result.Value.Count,
            result.Value.CountIsExact, result.Value.Completed, result.Value.Continuation, observedAt, cutoff));
    }

    /// <inheritdoc />
    public async Task<Result<CacheLookup<CacheEntryMetadata>>> HandleAsync(CacheEntryMetadataQuery request, SendOptions options, CancellationToken cancellationToken)
    {
        if (request.RequiredAction is not (CacheAccessActions.Read or CacheAccessActions.Write or CacheAccessActions.Invalidate))
        {
            return Result<CacheLookup<CacheEntryMetadata>>.Failure(new CacheError(CacheErrorCode.Validation, "The metadata action is invalid."));
        }

        var authorized = this.Authorize(request.Access, request.Collection, request.RequiredAction, request.Key, requireKey: true);
        if (authorized.IsFailure) { return Result<CacheLookup<CacheEntryMetadata>>.Failure(authorized); }

        var result = await registry.Get(request.Collection).ReadMetadataAsync(request.Key, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure || result.Value.Value is null) { return result; }

        return Result<CacheLookup<CacheEntryMetadata>>.Success(new(result.Value.Found, Redact(result.Value.Value, request.Access),
            result.Value.LookupOutcome, result.Value.StorageOutcome, isNotFound: result.Value.IsNotFound, version: result.Value.Version));
    }

    /// <inheritdoc />
    public async Task<Result<CacheLookup<ICacheReadHandle>>> HandleAsync(CacheEntryReadQuery request, SendOptions options, CancellationToken cancellationToken)
    {
        var authorized = this.Authorize(request.Access, request.Collection, CacheAccessActions.Read, request.Key, requireKey: true);
        return authorized.IsFailure ? Result<CacheLookup<ICacheReadHandle>>.Failure(authorized) :
            await registry.Get(request.Collection).OpenReadAsync(request.Key, request.Options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<CacheWriteOutcome>> HandleAsync(CacheEntryWriteCommand request, SendOptions options, CancellationToken cancellationToken)
    {
        var authorized = this.Authorize(request.Access, request.Collection, CacheAccessActions.Write, request.Item?.Key, requireKey: true);
        if (authorized.IsFailure) { return Result<CacheWriteOutcome>.Failure(authorized); }

        if (request.Item is null || (!request.Typed && request.Input is null))
        {
            return Result<CacheWriteOutcome>.Failure(new CacheError(CacheErrorCode.Validation, "A declared key and payload representation are required."));
        }

        var collection = registry.Get(request.Collection);
        var written = request.Typed ? await collection.SetValueAsync(request.Item, request.Value, request.Descriptor, request.Condition, cancellationToken).ConfigureAwait(false) :
            await collection.SetStreamAsync(request.Item, request.Input, request.Descriptor, request.Condition, cancellationToken).ConfigureAwait(false);
        return written.IsFailure || written.Value.Metadata is null ? written : Result<CacheWriteOutcome>.Success(written.Value with
        {
            Metadata = Redact(written.Value.Metadata, request.Access)
        });
    }

    /// <inheritdoc />
    public async Task<Result<bool>> HandleAsync(CacheEntryRemoveCommand request, SendOptions options, CancellationToken cancellationToken)
    {
        var authorized = this.Authorize(request.Access, request.Collection, CacheAccessActions.Invalidate, request.Key, requireKey: true);
        if (authorized.IsFailure) { return Result<bool>.Failure().WithErrors(authorized.Errors); }

        var removed = await registry.Get(request.Collection).RemoveAsync(request.Key, request.Condition, cancellationToken).ConfigureAwait(false);
        return removed.IsSuccess ? Result<bool>.Success(true) : Result<bool>.Failure().WithErrors(removed.Errors);
    }

    /// <inheritdoc />
    public async Task<Result<CacheInvalidationPreview>> HandleAsync(CacheInvalidationPreviewQuery request, SendOptions options, CancellationToken cancellationToken)
    {
        var authorized = this.Authorize(request.Access, request.Collection, CacheAccessActions.Read);
        if (authorized.IsFailure) { return Result<CacheInvalidationPreview>.Failure(authorized); }

        var result = await registry.Get(request.Collection).PreviewAsync(request.Selection, cancellationToken).ConfigureAwait(false);
        return result.IsFailure ? result : Result<CacheInvalidationPreview>.Success(result.Value with
        {
            Sample = result.Value.Sample.Select(entry => Redact(entry, request.Access)).ToArray()
        });
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<Result<CacheOperationSubmission>>>> HandleAsync(CacheInvalidateCommand request, SendOptions options, CancellationToken cancellationToken)
    {
        var authorized = this.Authorize(request.Access, null, CacheAccessActions.Invalidate, allowGlobal: true);
        if (authorized.IsFailure) { return Result<IReadOnlyList<Result<CacheOperationSubmission>>>.Failure(authorized); }

        foreach (var target in request.Collections)
        {
            var child = this.Authorize(request.Access, target, CacheAccessActions.Invalidate);
            if (child.IsFailure) { return Result<IReadOnlyList<Result<CacheOperationSubmission>>>.Failure(child); }
        }

        var submitted = await services.GetRequiredService<CacheOperationService>().SubmitAsync(new(request.Collections, request.Selection,
            request.Clear, request.Background, request.Access.Origin), cancellationToken).ConfigureAwait(false);
        return submitted.IsFailure ? submitted : Result<IReadOnlyList<Result<CacheOperationSubmission>>>.Success(submitted.Value.Select(child =>
            child.IsFailure ? child : Result<CacheOperationSubmission>.Success(child.Value with { Status = Redact(child.Value.Status, request.Access) })).ToArray());
    }

    /// <inheritdoc />
    public async Task<Result<CacheProviderOperation>> HandleAsync(CacheOperationCommand request, SendOptions options, CancellationToken cancellationToken)
    {
        var authorized = this.Authorize(request.Access, request.Collection,
            request.Action == CacheOperationAction.Status ? CacheAccessActions.Read : CacheAccessActions.Invalidate);
        if (authorized.IsFailure) { return Result<CacheProviderOperation>.Failure(authorized); }

        if (request.Handle is null || !Enum.IsDefined(request.Action) || request.Handle.Provider != catalog.Collections[CacheIdentityNormalizer.Name(request.Collection)].Provider.Name)
        {
            return Result<CacheProviderOperation>.Failure(new CacheError(CacheErrorCode.Validation, "The operation scope is invalid."));
        }

        var operations = services.GetRequiredService<CacheOperationService>();
        var result = request.Action switch
        {
            CacheOperationAction.Resume => await operations.ResumeAsync(request.Handle, cancellationToken).ConfigureAwait(false),
            CacheOperationAction.Cancel => await operations.CancelAsync(request.Handle, cancellationToken).ConfigureAwait(false),
            _ => await operations.GetStatusAsync(request.Handle, cancellationToken).ConfigureAwait(false)
        };
        return result.IsFailure ? result : Result<CacheProviderOperation>.Success(Redact(result.Value, request.Access));
    }

    /// <inheritdoc />
    public Task<Result<CacheBackgroundState>> HandleAsync(CacheBackgroundCommand request, SendOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var authorized = this.Authorize(request.Access, request.Collection,
            request.Action == CacheBackgroundAction.Status ? CacheAccessActions.Read : CacheAccessActions.Invalidate);
        if (authorized.IsFailure) { return Task.FromResult(Result<CacheBackgroundState>.Failure(authorized)); }

        if (!Enum.IsDefined(request.Action)) { return Task.FromResult(Result<CacheBackgroundState>.Failure(new CacheError(CacheErrorCode.Validation, "The background action is invalid."))); }

        var controls = services.GetRequiredService<CacheBackgroundControl>();
        return Task.FromResult(request.Action switch
        {
            CacheBackgroundAction.Pause => controls.Pause(request.Collection, request.Access.Origin),
            CacheBackgroundAction.Resume => controls.Resume(request.Collection, request.Access.Origin),
            _ => Result<CacheBackgroundState>.Success(controls.GetState(request.Collection))
        });
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<CacheMetricSeriesDescriptor>>> HandleAsync(CacheMetricSeriesQuery request, SendOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var authorized = this.Authorize(request.Access, request.Collection, CacheAccessActions.Read, allowGlobal: true);
        if (authorized.IsFailure) { return Task.FromResult(Result<IReadOnlyList<CacheMetricSeriesDescriptor>>.Failure(authorized)); }

        if (request.Collection is null && request.Access.Collections is not null)
        {
            return Task.FromResult(Result<IReadOnlyList<CacheMetricSeriesDescriptor>>.Failure(new UnauthorizedError("Select one authorized collection for scoped history.")));
        }

        return Task.FromResult(Result<IReadOnlyList<CacheMetricSeriesDescriptor>>.Success(services.GetRequiredService<CacheMetricHistory>().GetSeries(request.Collection)));
    }

    /// <inheritdoc />
    public async Task<Result<CacheDiagnosticsReport>> HandleAsync(CacheDiagnosticsQuery request, SendOptions options, CancellationToken cancellationToken)
    {
        var authorized = this.Authorize(request.Access, request.Collection, CacheAccessActions.Read, allowGlobal: true);
        if (authorized.IsFailure) { return Result<CacheDiagnosticsReport>.Failure(authorized); }

        if (request.Collection is null && request.Access.Collections is not null)
        {
            return Result<CacheDiagnosticsReport>.Failure(new UnauthorizedError("Select one authorized collection for scoped diagnostics."));
        }

        return await services.GetRequiredService<CacheDiagnosticsQueryService>().GetSnapshotAsync(request.Collection, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<CacheMetricHistoryResult>> HandleAsync(CacheMetricHistoryQueryRequest request, SendOptions options, CancellationToken cancellationToken)
    {
        var authorized = this.Authorize(request.Access, request.Collection, CacheAccessActions.Read, allowGlobal: true);
        if (authorized.IsFailure) { return Result<CacheMetricHistoryResult>.Failure(authorized); }

        if (request.Query is null || (request.Collection is null && request.Access.Collections is not null))
        {
            return Result<CacheMetricHistoryResult>.Failure(new CacheError(CacheErrorCode.Validation, "Select one authorized scope and a bounded history query."));
        }

        return await services.GetRequiredService<CacheMetricHistory>().QueryAsync(request.Query, cancellationToken).ConfigureAwait(false);
    }

    private static bool Allowed(CacheAccessContext access, string collection) => access.Collections is null ||
        access.Collections.Contains(collection, StringComparer.OrdinalIgnoreCase);

    private Result Authorize(CacheAccessContext access, string collection, CacheAccessActions action, string key = null,
        bool allowGlobal = false, bool requireKey = false)
    {
        var authorized = services.GetRequiredService<CacheManagementAccessValidator>().Validate(access, collection, action, allowGlobal);
        if (authorized.IsFailure || collection is null) { return authorized; }

        try
        {
            if (!catalog.Collections.TryGetValue(CacheIdentityNormalizer.Name(collection), out var binding))
            {
                return Result.Failure(new CacheError(CacheErrorCode.Contract, "The cache collection is not declared."));
            }

            if (key is not null || requireKey)
            {
                _ = new CacheEntryIdentity(catalog.Options.Namespace, binding.Name, binding.Options.ContractVersion, key);
                if (Encoding.UTF8.GetByteCount(key) > binding.Options.Limits.MaxKeyBytes)
                {
                    return Result.Failure(new CacheError(CacheErrorCode.Validation, "The key exceeds its collection bound."));
                }
            }

            return Result.Success();
        }
        catch (ArgumentException)
        {
            return Result.Failure(new CacheError(CacheErrorCode.Validation, "The cache identity is invalid."));
        }
    }

    private static CacheEntryMetadata Redact(CacheEntryMetadata entry, CacheAccessContext access) => access.RevealSensitiveMetadata ? entry :
        entry with { Identity = new(entry.Identity.Namespace, entry.Identity.Collection, entry.Identity.ContractVersion, "[redacted]"), DisplayKey = "[redacted]", Dimensions = [] };

    private static CacheProviderOperation Redact(CacheProviderOperation operation, CacheAccessContext access) => access.RevealSensitiveMetadata || operation.Request is null ? operation :
        operation with { Request = operation.Request with { Selection = CacheSelection.None } };

    private static CacheCollectionOptions RedactPolicy(CacheCollectionOptions options, CacheAccessContext access) => options with
    {
        Warmup = options.Warmup with { Items = access.RevealSensitiveMetadata ? options.Warmup.Items : [] },
        Extensions = options.Extensions.ToDictionary(integration => integration.Key, integration =>
            (IReadOnlyDictionary<string, string>)integration.Value.ToDictionary(setting => setting.Key, setting =>
                setting.Key.Equals("Enabled", StringComparison.OrdinalIgnoreCase) && bool.TryParse(setting.Value, out _) ? setting.Value : "[redacted]",
                StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase)
    };
}