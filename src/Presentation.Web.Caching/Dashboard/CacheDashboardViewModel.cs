// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching.Dashboard;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;

/// <summary>Contains only bounded authorized metadata and observations for one Dashboard content fragment.</summary>
/// <example><code>var view = await builder.BuildAsync(context);</code></example>
public sealed record CacheDashboardViewModel
{
    /// <summary>Gets the current declared view key.</summary>
    public string View { get; init; }

    /// <summary>Gets the directly routed collection, or null for cache-level tabs.</summary>
    public string Collection { get; init; }

    /// <summary>Gets the selected observation scope, which may be narrowed on global Diagnostics.</summary>
    public string ScopeCollection { get; init; }

    /// <summary>Gets the authorized declared collection list.</summary>
    public IReadOnlyList<CacheCollectionInfo> Collections { get; init; } = [];

    /// <summary>Gets bounded retained-metadata counts by collection, with missing observations left unavailable.</summary>
    /// <example><code>var exact = model.CollectionCounts[collection].CountIsExact;</code></example>
    public IReadOnlyDictionary<string, CacheInvalidationPreview> CollectionCounts { get; init; } =
        new Dictionary<string, CacheInvalidationPreview>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the directly selected collection policy.</summary>
    public CacheCollectionInfo Selected { get; init; }

    /// <summary>Gets one bounded redacted metadata page, or null outside Entries.</summary>
    public CacheEntriesPage Entries { get; init; }

    /// <summary>Gets real node/provider observations, or null when unavailable/outside observation views.</summary>
    public CacheDiagnosticsReport Diagnostics { get; init; }

    /// <summary>Gets configured versus paused node state for collection Operations.</summary>
    public CacheBackgroundState Background { get; init; }

    /// <summary>Gets one explicitly selected tracked operation, or null when no identifier was requested.</summary>
    public CacheProviderOperation Operation { get; init; }

    /// <summary>Gets a controlled query failure, with unavailable values kept distinct from zero.</summary>
    public string Error { get; init; }

    /// <summary>Gets the bounded Entries page size.</summary>
    public int Take { get; init; } = 25;

    /// <summary>Gets whether the selected collection is effectively exposed through the independently enabled external API.</summary>
    public bool HttpExposed { get; init; }

    /// <summary>Gets the effective global caching switch for status labels.</summary>
    public bool FeatureEnabled { get; init; }

    /// <summary>Gets whether the current trusted dashboard scope permits visible entry keys and dimensions.</summary>
    /// <example><code>var dimensionsVisible = model.RevealSensitiveMetadata;</code></example>
    public bool RevealSensitiveMetadata { get; init; }
}

/// <summary>Loads each view through common Application requests without payload reads or source work.</summary>
/// <example><code>var model = await builder.BuildAsync(context);</code></example>
public sealed class CacheDashboardViewModelBuilder(IRequester requester, ICacheDashboardAccessContextFactory accessFactory,
    CacheWebConfiguration web, CacheCatalog catalog)
{
    /// <summary>Builds one finite view while preserving route, filter, and opaque continuation scope.</summary>
    public async Task<CacheDashboardViewModel> BuildAsync(HttpContext context)
    {
        var collection = context.Request.RouteValues["collection"]?.ToString();
        var view = CacheDashboardRoutes.View(context.Request.Path, collection);
        var scopeCollection = collection ?? (string.IsNullOrWhiteSpace(context.Request.Query["collection"]) ? null : context.Request.Query["collection"].ToString());
        var access = accessFactory.Create(context, CacheAccessActions.Read);
        var declared = await requester.SendAsync(new CacheCollectionsQuery(access), cancellationToken: context.RequestAborted).ConfigureAwait(false);
        var model = new CacheDashboardViewModel
        {
            View = view, Collection = collection, ScopeCollection = scopeCollection, FeatureEnabled = catalog.Options.Enabled,
            RevealSensitiveMetadata = access?.RevealSensitiveMetadata == true
        };
        if (declared.IsFailure) { return model with { Error = declared.Errors.FirstOrDefault()?.Message ?? "Declared cache scope is unavailable." }; }

        var selected = declared.Value.FirstOrDefault(item => item.Name.Equals(collection, StringComparison.OrdinalIgnoreCase));
        model = model with { Collections = declared.Value, Selected = selected, HttpExposed = collection is not null && web.Options.Enabled && web.IsExposed(collection) };
        if (collection is not null && selected is null) { return model with { Error = "The selected collection is unavailable in the current authorized scope." }; }

        if (view == "collections")
        {
            var counts = new Dictionary<string, CacheInvalidationPreview>(StringComparer.OrdinalIgnoreCase);
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            budget.CancelAfter(TimeSpan.FromSeconds(5));
            foreach (var item in declared.Value.Where(item => catalog.Options.Enabled && item.Options.Enabled))
            {
                context.RequestAborted.ThrowIfCancellationRequested();
                if (budget.IsCancellationRequested) { break; }

                try
                {
                    var count = await requester.SendAsync(new CacheInvalidationPreviewQuery(access, item.Name, CacheSelection.Pattern("*")),
                        cancellationToken: budget.Token).ConfigureAwait(false);
                    if (count.IsSuccess) { counts[item.Name] = count.Value with { Sample = [] }; }
                }
                catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
                {
                    break;
                }
            }

            model = model with { CollectionCounts = counts };
        }
        else if (view is "overview" or "shared-loads" or "diagnostics")
        {
            var diagnostics = await requester.SendAsync(new CacheDiagnosticsQuery(access, scopeCollection), cancellationToken: context.RequestAborted).ConfigureAwait(false);
            model = diagnostics.IsSuccess ? model with { Diagnostics = diagnostics.Value } : model with { Error = diagnostics.Errors.FirstOrDefault()?.Message ?? "Cache observations are unavailable." };
        }
        else if (view == "entries" && selected is not null)
        {
            var maximum = selected.Options.Limits.MetadataPageSize;
            var take = int.TryParse(context.Request.Query["pageSize"], out var requested) ? requested : Math.Min(25, maximum);
            try
            {
                var selection = CacheDashboardEntrySelection.Parse(context.Request.Query, selected.Options.Dimensions);
                var boundary = long.TryParse(context.Request.Query["listBoundary"], out var ticks) ? new DateTimeOffset(ticks, TimeSpan.Zero) : (DateTimeOffset?)null;
                var entries = await requester.SendAsync(new CacheEntriesQuery(access, collection,
                    selection, take, context.Request.Query["continuation"], boundary),
                    cancellationToken: context.RequestAborted).ConfigureAwait(false);
                model = entries.IsSuccess ? model with { Entries = entries.Value, Take = take } :
                    model with { Error = entries.Errors.FirstOrDefault()?.Message ?? "Entry metadata is unavailable.", Take = Math.Min(25, maximum) };
            }
            catch (ArgumentException exception)
            {
                model = model with { Error = "The entry filters are invalid. " + exception.Message };
            }
        }
        else if (view == "operations" && selected is not null)
        {
            var background = await requester.SendAsync(new CacheBackgroundCommand(access, collection), cancellationToken: context.RequestAborted).ConfigureAwait(false);
            model = background.IsSuccess ? model with { Background = background.Value } : model with { Error = background.Errors.FirstOrDefault()?.Message ?? "Node controls are unavailable." };
            var operationId = context.Request.Query["operationId"].ToString();
            if (operationId.Length != 0)
            {
                var operation = await requester.SendAsync(new CacheOperationCommand(access, new(selected.Provider, collection, operationId)),
                    cancellationToken: context.RequestAborted).ConfigureAwait(false);
                model = operation.IsSuccess ? model with { Operation = operation.Value } : model with { Error = operation.Errors.FirstOrDefault()?.Message ?? "Operation status is unavailable." };
            }
        }

        return model;
    }
}