// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching.Dashboard;

using System.Globalization;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>Maps cache-level and collection-level Dashboard tabs, read-only fragments/history, and authorized operational forms.</summary>
/// <example><code>cache.WithDashboard(); app.MapEndpoints();</code></example>
public sealed class DashboardEndpoints(DashboardEndpointsOptions options = null, CacheCatalog catalog = null,
    CacheDashboardConfiguration configuration = null) : EndpointsBase, IDashboardEndpoints
{
    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        if (configuration?.Enabled != true || catalog is null || options is not { Enabled: true }) { return; }

        var group = this.MapGroup(app, options).WithTags("_bdk.Dashboard").AddEndpointFilter<CacheDashboardAuthorizationFilter>();
        group.AddEndpointFilter(async (context, next) =>
        {
            var collection = context.HttpContext.Request.RouteValues["collection"]?.ToString();
            return collection is not null && !catalog.Collections.ContainsKey(collection) ? Results.NotFound() : await next(context).ConfigureAwait(false);
        });
        group.MapDashboardPage<Pages.Index>("/caching", "_bdk.Dashboard.ApplicationCaching", "Caching");
        group.MapDashboardPage<Pages.Collections>("/caching/collections", "_bdk.Dashboard.ApplicationCaching.Collections", "Cache collections");
        group.MapDashboardPage<Pages.SharedLoads>("/caching/shared-loads", "_bdk.Dashboard.ApplicationCaching.SharedLoads", "Shared cache loads");
        group.MapDashboardPage<Pages.Diagnostics>("/caching/diagnostics", "_bdk.Dashboard.ApplicationCaching.Diagnostics", "Cache diagnostics");
        group.MapDashboardPage<Pages.Operations>("/caching/operations", "_bdk.Dashboard.ApplicationCaching.Operations", "Cache operations")
            .WithMetadata(new CacheDashboardFormPageMetadata());
        group.MapDashboardPage<Pages.CollectionOverview>("/caching/collections/{collection}", "_bdk.Dashboard.ApplicationCaching.Collection", "Cache collection");
        group.MapDashboardPage<Pages.CollectionEntries>("/caching/collections/{collection}/entries", "_bdk.Dashboard.ApplicationCaching.Collection.Entries", "Cache metadata")
            .WithMetadata(new CacheDashboardFormPageMetadata());
        group.MapDashboardPage<Pages.CollectionSharedLoads>("/caching/collections/{collection}/shared-loads", "_bdk.Dashboard.ApplicationCaching.Collection.SharedLoads", "Collection shared loads");
        group.MapDashboardPage<Pages.CollectionDiagnostics>("/caching/collections/{collection}/diagnostics", "_bdk.Dashboard.ApplicationCaching.Collection.Diagnostics", "Collection diagnostics");
        group.MapDashboardPage<Pages.CollectionOperations>("/caching/collections/{collection}/operations", "_bdk.Dashboard.ApplicationCaching.Collection.Operations", "Collection operations")
            .WithMetadata(new CacheDashboardFormPageMetadata());
        group.MapDashboardPage<Pages.CollectionSettings>("/caching/collections/{collection}/settings", "_bdk.Dashboard.ApplicationCaching.Collection.Settings", "Collection settings");
        foreach (var tab in CacheDashboardRoutes.GlobalTabs)
        {
            var path = "/caching" + (tab.Segment.Length == 0 ? "" : "/" + tab.Segment);
            group.MapDashboardPage<Pages.Content>(path + "/content", "_bdk.Dashboard.ApplicationCaching.Content." + tab.Key, "Read-only cache observations").ExcludeFromDescription();
        }

        foreach (var tab in CacheDashboardRoutes.CollectionTabs)
        {
            var path = "/caching/collections/{collection}" + (tab.Segment.Length == 0 ? "" : "/" + tab.Segment);
            var content = group.MapDashboardPage<Pages.Content>(path + "/content", "_bdk.Dashboard.ApplicationCaching.Collection.Content." + tab.Key, "Read-only collection observations").ExcludeFromDescription();
            if (tab.Key == "entries") { content.WithMetadata(new CacheDashboardFormPageMetadata()); }
        }

        group.MapGet("/caching/history", HistoryAsync).WithName("_bdk.Dashboard.ApplicationCaching.History").ExcludeFromDescription();
        group.MapGet("/caching/collections/{collection}/history", HistoryAsync).WithName("_bdk.Dashboard.ApplicationCaching.Collection.History").ExcludeFromDescription();
        foreach (var action in new[] { "preview", "invalidate", "clear", "remove", "pause", "resume", "operation-resume", "operation-cancel" })
        {
            group.MapPost("/caching/actions/" + action, (HttpContext context, [FromServices] IRequester requester,
                [FromServices] ICacheDashboardAccessContextFactory access, CancellationToken ct) => this.ExecuteActionAsync(context, action, requester, access, ct))
                .WithMetadata(new CacheDashboardActionMetadata(action == "preview" ? CacheAccessActions.Read : CacheAccessActions.Invalidate))
                .WithName("_bdk.Dashboard.ApplicationCaching.Action." + action).ExcludeFromDescription().DisableAntiforgery();
        }

        this.IsRegistered = true;
    }

    private async Task<HttpResult> ExecuteActionAsync(HttpContext context, string action, IRequester requester,
        ICacheDashboardAccessContextFactory accessFactory, CancellationToken cancellationToken)
    {
        try
        {
            var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            var targets = form["collections"].ToArray();
            if (targets.Length is < 1 or > 32 || targets.Any(name => !catalog.Collections.ContainsKey(name)))
            {
                return Failure("Select between one and thirty-two declared collections.");
            }

            var scope = accessFactory.Create(context, action == "preview" ? CacheAccessActions.Read : CacheAccessActions.Invalidate);
            if (action is "preview" or "invalidate" or "clear")
            {
                var raw = form["selection"].ToString();
                var selection = action == "clear" ? CacheSelection.None : form["selectionKind"].ToString() switch
                {
                    "key" => CacheSelection.Key(raw),
                    "" or "prefix" => raw.Length == 0 ? CacheSelection.Pattern("*") : CacheSelection.Prefix(raw),
                    _ => throw new ArgumentException("The selection kind is invalid.")
                };
                if (action == "preview")
                {
                    var results = new List<CacheDashboardActionItem>();
                    foreach (var target in targets)
                    {
                        var preview = await requester.SendAsync(new CacheInvalidationPreviewQuery(scope, target, selection), cancellationToken: cancellationToken).ConfigureAwait(false);
                        results.Add(preview.IsSuccess ? new(target, "Preview", preview.Value.Count, preview.Value.CountIsExact, preview.Value.Continuation is null) :
                            new(target, "Unavailable", Error: preview.Errors.FirstOrDefault()?.Message));
                    }

                    return Results.Ok(new CacheDashboardActionResponse("Invalidation preview", results));
                }

                var submitted = await requester.SendAsync(new CacheInvalidateCommand(scope, targets, selection, action == "clear",
                    form["background"] == "true"), cancellationToken: cancellationToken).ConfigureAwait(false);
                if (submitted.IsFailure) { return CacheHttpResultMapper.Failure(submitted); }

                return Results.Ok(new CacheDashboardActionResponse(action == "clear" ? "Explicit clear" : "Selected invalidation",
                    submitted.Value.Select((result, index) => result.IsSuccess ? new CacheDashboardActionItem(result.Value.Handle.Collection,
                        result.Value.Status.State.ToString(), result.Value.Status.Outcome?.AffectedCount,
                        result.Value.Status.Outcome?.CountIsExact, result.Value.Status.Outcome?.Completed ?? result.Value.Status.State == CacheOperationState.Completed, result.Value.Accepted,
                        CacheDashboardRoutes.Collection(options, result.Value.Handle.Collection, "operations") + "?operationId=" + Uri.EscapeDataString(result.Value.Handle.Id)) :
                        new CacheDashboardActionItem(targets[index], "Failed", Error: result.Errors.FirstOrDefault()?.Message)).ToArray()));
            }

            if (targets.Length != 1) { return Failure("This operation requires exactly one collection."); }

            var collection = targets[0];
            if (action == "remove")
            {
                var version = form["version"].ToString();
                if (version.Length > 256) { return Failure("The acquired entry version exceeds its bound."); }

                var condition = new CacheWriteCondition(version.Length == 0 ? null : new CacheEntryVersion(version));
                var removed = await requester.SendAsync(new CacheEntryRemoveCommand(scope, collection, form["key"], condition), cancellationToken: cancellationToken).ConfigureAwait(false);
                return removed.IsFailure ? CacheHttpResultMapper.Failure(removed) : Results.Ok(new CacheDashboardActionResponse("Exact removal acknowledged", [new(collection, "Acknowledged")]));
            }

            if (action is "pause" or "resume")
            {
                var changed = await requester.SendAsync(new CacheBackgroundCommand(scope, collection,
                    action == "pause" ? CacheBackgroundAction.Pause : CacheBackgroundAction.Resume), cancellationToken: cancellationToken).ConfigureAwait(false);
                return changed.IsFailure ? CacheHttpResultMapper.Failure(changed) : Results.Ok(new CacheDashboardActionResponse("Node-local background admission",
                    [new(collection, changed.Value.Paused ? "Paused" : "Resumed")]));
            }

            var controlled = await requester.SendAsync(new CacheOperationCommand(scope,
                new(catalog.Collections[collection].Provider.Name, collection, form["operationId"]),
                action == "operation-resume" ? CacheOperationAction.Resume : CacheOperationAction.Cancel), cancellationToken: cancellationToken).ConfigureAwait(false);
            return controlled.IsFailure ? CacheHttpResultMapper.Failure(controlled) : Results.Ok(new CacheDashboardActionResponse("Tracked operation",
                [new(collection, controlled.Value.State.ToString(), controlled.Value.Outcome?.AffectedCount, controlled.Value.Outcome?.CountIsExact,
                    controlled.Value.Outcome?.Completed)]));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException)
        {
            return Failure("The bounded cache operation input is invalid.");
        }
    }

    private static async Task<HttpResult> HistoryAsync(HttpContext context, [FromServices] IRequester requester,
        [FromServices] ICacheDashboardAccessContextFactory accessFactory, [FromServices] CacheCatalog catalog, CancellationToken ct)
    {
        var collection = context.Request.RouteValues["collection"]?.ToString() ?? context.Request.Query["collection"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(collection)) { collection = null; }

        var access = accessFactory.Create(context, CacheAccessActions.Read);
        var declared = await requester.SendAsync(new CacheMetricSeriesQuery(access, collection), cancellationToken: ct).ConfigureAwait(false);
        if (declared.IsFailure) { return CacheHttpResultMapper.Failure(declared); }

        var window = double.TryParse(context.Request.Query["windowSeconds"], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ?
            seconds : catalog.Options.Diagnostics.ChartWindow.TotalSeconds;
        if (!double.IsFinite(window) || window <= 0 || window > catalog.Options.Diagnostics.HistoryRetention.TotalSeconds)
        {
            return Failure("The displayed history window exceeds its configured finite retention.");
        }

        var charts = new List<CacheDiagnosticsChart>();
        DateTimeOffset? from = null, to = null;
        var observedAt = DateTimeOffset.UtcNow;
        foreach (var family in CacheDiagnosticsCharts.Families)
        {
            var eligible = declared.Value.Where(series => family.Metrics.Contains(series.Metric, StringComparer.Ordinal))
                .OrderBy(series => series.Collection, StringComparer.Ordinal).ThenBy(series => series.Metric, StringComparer.Ordinal).ToArray();
            var ids = eligible.Take(16).Select(series => series.Id).ToArray();
            if (ids.Length == 0)
            {
                charts.Add(new(family, [], CacheObservationAvailability.Unavailable, "This metric family is not retained for the selected scope."));
                continue;
            }

            var history = await requester.SendAsync(new CacheMetricHistoryQueryRequest(access, new(collection, ids, TimeSpan.FromSeconds(window),
                catalog.Options.Diagnostics.MaximumChartPoints)), cancellationToken: ct).ConfigureAwait(false);
            if (history.IsFailure)
            {
                charts.Add(new(family, [], CacheObservationAvailability.Unavailable, history.Errors.FirstOrDefault()?.Message));
                continue;
            }

            observedAt = history.Value.ObservedAt;
            if (history.Value.AvailableFrom.HasValue && (!from.HasValue || history.Value.AvailableFrom.Value < from.Value)) { from = history.Value.AvailableFrom; }

            if (history.Value.AvailableTo.HasValue && (!to.HasValue || history.Value.AvailableTo.Value > to.Value)) { to = history.Value.AvailableTo; }

            charts.Add(new(family, history.Value.Series, history.Value.Availability, eligible.Length > ids.Length ?
                "Showing the first 16 of " + eligible.Length.ToString(CultureInfo.InvariantCulture) + " series. Select a collection for full detail." : null));
        }

        return Results.Ok(new CacheDiagnosticsCharts(observedAt, from, to, "NodeLocal", window / 60, charts));
    }

    private static HttpResult Failure(string message) => CacheHttpResultMapper.Failure(Result.Failure(new CacheError(CacheErrorCode.Validation, message)));
}

/// <summary>Reports bounded Dashboard action outcomes without presenting accepted work as completed invalidation.</summary>
/// <param name="Title">Action label.</param>
/// <param name="Items">Per-collection actual status and partial outcomes.</param>
/// <example><code>var response = new CacheDashboardActionResponse("Preview", items);</code></example>
public sealed record CacheDashboardActionResponse(string Title, IReadOnlyList<CacheDashboardActionItem> Items);

/// <summary>Describes one actual scope outcome for an operational form.</summary>
/// <param name="Collection">Declared target.</param>
/// <param name="State">Observed state.</param>
/// <param name="Count">Committed or preview count when known.</param>
/// <param name="CountIsExact">Count accuracy when known.</param>
/// <param name="Completed">Whether the logical scope is complete.</param>
/// <param name="Accepted">Whether background work was only accepted.</param>
/// <param name="StatusUrl">Direct operation detail link, when tracked.</param>
/// <param name="Error">Controlled child failure.</param>
/// <example><code>Show(item.Count, item.CountIsExact, item.Completed);</code></example>
public sealed record CacheDashboardActionItem(string Collection, string State, long? Count = null, bool? CountIsExact = null,
    bool? Completed = null, bool Accepted = false, string StatusUrl = null, string Error = null);