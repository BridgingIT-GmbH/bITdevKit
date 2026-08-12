// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Profiling.Dashboard;

using System.Text;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using BridgingIT.DevKit.Presentation.Web.Profiling;
using BridgingIT.DevKit.Presentation.Web.Profiling.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using IResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>Maps the authorized Profiling dashboard and operational endpoints.</summary>
/// <param name="options">The shared dashboard endpoint options.</param>
/// <example><code>services.AddProfiling(options => options.Enabled()); services.AddDashboard(options => options.Enabled());</code></example>
public sealed class DashboardEndpoints(DashboardEndpointsOptions options)
    : EndpointsBase,
        IDashboardEndpoints
{
    private const string ProfilingPath = "/profiling";
    private const string ContentPath = "/profiling/content";
    private const string DataPath = "/profiling/data";
    private const string StatusPath = "/profiling/status";
    private const string SessionsPath = "/profiling/sessions";
    private const string StartPath = "/profiling/start";
    private const string StopPath = "/profiling/stop";
    private const string SnapshotPath = "/profiling/snapshot";
    private const string GarbageCollectionPath = "/profiling/gc";
    private const string StressPath = "/profiling/stress";
    private const string MarkerPath = "/profiling/mark";
    private const string SessionRestartPath = "/profiling/sessions/{sessionKey}/restart";
    private const string SessionMetadataPath = "/profiling/sessions/{sessionKey}/metadata";
    private const string SessionDeletePath = "/profiling/sessions/{sessionKey}";
    private const string DeleteUnpinnedPath = "/profiling/sessions/unpinned";
    private const string ClearPath = "/profiling/clear";
    private const string ComparePath = "/profiling/compare";
    private const string AnalyzePath = "/profiling/analyze";
    private const string ExportPath = "/profiling/export";
    private const string SessionArchivePath = "/profiling/archive/sessions/{sessionKey}";
    private const string SessionPerfettoPath = "/profiling/export/perfetto/sessions/{sessionKey}";
    private const string SnapshotArchivePath = "/profiling/archive/sessions/{sessionKey}/nodes/{nodeKey}/snapshots/{snapshotKey}";
    private const string ArchiveImportPath = "/profiling/archive/import";

    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        options ??= new DashboardEndpointsOptions();
        if (!options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, options).WithTags("_bdk.Dashboard");
        group.MapDashboardPage<Pages.Index>(
            ProfilingPath,
            "_bdk.Dashboard.Profiling",
            "Dashboard Profiling",
            "Collects and analyzes focused runtime profiling snapshots."
        );
        group.MapDashboardPage<Pages.Content>(
            ContentPath,
            "_bdk.Dashboard.ProfilingContent",
            "Dashboard Profiling Content",
            "Shows the refreshable Profiling dashboard content."
        );

        group
            .MapGet(DataPath, (HttpContext context, string session = null, string node = null) =>
                this.GetDataAsync(context, session, node)
            )
            .WithName("_bdk.Dashboard.ProfilingData")
            .Produces<ProfilingDashboardDataResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        group
            .MapGet(StatusPath, (Func<HttpContext, Task<IResult>>)this.GetStatusAsync)
            .WithName("_bdk.Dashboard.ProfilingStatus")
            .Produces<ProfilingStatus>()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        group
            .MapGet(SessionsPath, (Func<HttpContext, Task<IResult>>)this.ListSessionsAsync)
            .WithName("_bdk.Dashboard.ProfilingSessions")
            .Produces<IReadOnlyList<ProfilingSession>>()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        group
            .MapGet(ExportPath, (HttpContext context, string session, string node = null) =>
                this.ExportAsync(context, session, node)
            )
            .WithName("_bdk.Dashboard.ProfilingExport")
            .Produces<string>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        group
            .MapGet(SessionArchivePath, (string sessionKey, HttpContext context) =>
                this.ExportSessionArchiveAsync(sessionKey, context)
            )
            .WithName("_bdk.Dashboard.ProfilingSessionArchive")
            .Produces(StatusCodes.Status200OK, contentType: "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        group
            .MapGet(SessionPerfettoPath, (string sessionKey, HttpContext context) =>
                this.ExportSessionPerfettoAsync(sessionKey, context)
            )
            .WithName("_bdk.Dashboard.ProfilingSessionPerfetto")
            .Produces(StatusCodes.Status200OK, contentType: "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        group
            .MapGet(SnapshotArchivePath, (string sessionKey, string nodeKey, string snapshotKey, HttpContext context) =>
                this.ExportSnapshotArchiveAsync(sessionKey, nodeKey, snapshotKey, context)
            )
            .WithName("_bdk.Dashboard.ProfilingSnapshotArchive")
            .Produces(StatusCodes.Status200OK, contentType: "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group
            .MapPost(StartPath, (ProfilingDashboardStartRequest request, HttpContext context) =>
                this.StartAsync(request, context)
            )
            .WithName("_bdk.Dashboard.ProfilingStart")
            .DisableAntiforgery();
        group
            .MapPost(StopPath, (Func<HttpContext, Task<IResult>>)this.StopAsync)
            .WithName("_bdk.Dashboard.ProfilingStop")
            .DisableAntiforgery();
        group
            .MapPost(SnapshotPath, (ProfilingDashboardSnapshotRequest request, HttpContext context) =>
                this.SnapshotAsync(request, context)
            )
            .WithName("_bdk.Dashboard.ProfilingSnapshot")
            .DisableAntiforgery();
        group
            .MapPost(
                GarbageCollectionPath,
                (Func<HttpContext, Task<IResult>>)this.CollectGarbageAsync
            )
            .WithName("_bdk.Dashboard.ProfilingGc")
            .DisableAntiforgery();
        group
            .MapPost(StressPath, (HttpContext context) => this.Stress(context))
            .WithName("_bdk.Dashboard.ProfilingStress")
            .DisableAntiforgery()
            .Produces<ProfilingStressResult>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        group
            .MapPost(MarkerPath, (ProfilingDashboardMarkerRequest request, HttpContext context) =>
                this.AddMarkerAsync(request, context)
            )
            .WithName("_bdk.Dashboard.ProfilingMarker")
            .DisableAntiforgery();
        group
            .MapPost(SessionRestartPath, (string sessionKey, HttpContext context) =>
                this.RestartAsync(sessionKey, context)
            )
            .WithName("_bdk.Dashboard.ProfilingRestart")
            .DisableAntiforgery();
        group
            .MapPut(SessionMetadataPath, (string sessionKey, ProfilingDashboardMetadataRequest request, HttpContext context) =>
                this.UpdateMetadataAsync(sessionKey, request, context)
            )
            .WithName("_bdk.Dashboard.ProfilingMetadata")
            .DisableAntiforgery();
        group
            .MapDelete(
                DeleteUnpinnedPath,
                (Func<HttpContext, Task<IResult>>)this.DeleteUnpinnedAsync
            )
            .WithName("_bdk.Dashboard.ProfilingDeleteUnpinned")
            .DisableAntiforgery();
        group
            .MapDelete(SessionDeletePath, (string sessionKey, HttpContext context) =>
                this.DeleteSessionAsync(sessionKey, context)
            )
            .WithName("_bdk.Dashboard.ProfilingDeleteSession")
            .DisableAntiforgery();
        group
            .MapPost(ClearPath, (ProfilingDashboardClearRequest request, HttpContext context) =>
                this.ClearAsync(request, context)
            )
            .WithName("_bdk.Dashboard.ProfilingClear")
            .DisableAntiforgery();
        group
            .MapPost(ComparePath, (ProfilingDashboardCompareRequest request, HttpContext context) =>
                this.CompareAsync(request, context)
            )
            .WithName("_bdk.Dashboard.ProfilingCompare")
            .DisableAntiforgery();
        group
            .MapPost(AnalyzePath, (ProfilingDashboardAnalyzeRequest request, HttpContext context) =>
                this.AnalyzeAsync(request, context)
            )
            .WithName("_bdk.Dashboard.ProfilingAnalyze")
            .DisableAntiforgery();
        group
            .MapPost(ArchiveImportPath, (Func<HttpContext, Task<IResult>>)this.ImportArchiveAsync)
            .WithName("_bdk.Dashboard.ProfilingArchiveImport")
            .DisableAntiforgery()
            .Produces<ProfilingArchiveImportResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    /// <summary>Builds the Profiling page path with optional shareable selections.</summary>
    /// <param name="options">The dashboard options.</param>
    /// <param name="sessionKey">The optional public session key.</param>
    /// <param name="nodeKey">The optional public node key.</param>
    /// <returns>The absolute page path.</returns>
    /// <example><code>var path = DashboardEndpoints.BuildProfilingPath(options, "sess0001", "node0001");</code></example>
    public static string BuildProfilingPath(
        DashboardEndpointsOptions options,
        string sessionKey = null,
        string nodeKey = null
    ) => BuildSelectionPath(options, ProfilingPath, sessionKey, nodeKey);

    /// <summary>Builds the Profiling content path.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildContentPath(options);</code></example>
    public static string BuildContentPath(DashboardEndpointsOptions options) => BuildPath(options, ContentPath);

    /// <summary>Builds the Profiling data path with optional selected public keys.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildDataPath(options, "sess0001", "node0001");</code></example>
    public static string BuildDataPath(DashboardEndpointsOptions options, string sessionKey = null, string nodeKey = null) =>
        BuildSelectionPath(options, DataPath, sessionKey, nodeKey);

    /// <summary>Builds the Profiling status path.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildStatusPath(options);</code></example>
    public static string BuildStatusPath(DashboardEndpointsOptions options) => BuildPath(options, StatusPath);

    /// <summary>Builds the stored-session list path.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildSessionsPath(options);</code></example>
    public static string BuildSessionsPath(DashboardEndpointsOptions options) => BuildPath(options, SessionsPath);

    /// <summary>Builds the session-start path.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildStartPath(options);</code></example>
    public static string BuildStartPath(DashboardEndpointsOptions options) => BuildPath(options, StartPath);

    /// <summary>Builds the active-session stop path.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildStopPath(options);</code></example>
    public static string BuildStopPath(DashboardEndpointsOptions options) => BuildPath(options, StopPath);

    /// <summary>Builds the manual-snapshot path.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildSnapshotPath(options);</code></example>
    public static string BuildSnapshotPath(DashboardEndpointsOptions options) => BuildPath(options, SnapshotPath);

    /// <summary>Builds the manual garbage-collection path.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildGarbageCollectionPath(options);</code></example>
    public static string BuildGarbageCollectionPath(DashboardEndpointsOptions options) => BuildPath(options, GarbageCollectionPath);

    /// <summary>Builds the fixed host-local stress-workload path.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildStressPath(options);</code></example>
    public static string BuildStressPath(DashboardEndpointsOptions options) =>
        BuildPath(options, StressPath);

    /// <summary>Builds the phase-marker path.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildMarkerPath(options);</code></example>
    public static string BuildMarkerPath(DashboardEndpointsOptions options) => BuildPath(options, MarkerPath);

    /// <summary>Builds the selected-session restart path.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildRestartPath(options, "sess0001");</code></example>
    public static string BuildRestartPath(DashboardEndpointsOptions options, string sessionKey) =>
        BuildSessionPath(options, SessionRestartPath, sessionKey);

    /// <summary>Builds the selected-session metadata path.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildMetadataPath(options, "sess0001");</code></example>
    public static string BuildMetadataPath(DashboardEndpointsOptions options, string sessionKey) =>
        BuildSessionPath(options, SessionMetadataPath, sessionKey);

    /// <summary>Builds the selected-session delete path.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildDeleteSessionPath(options, "sess0001");</code></example>
    public static string BuildDeleteSessionPath(DashboardEndpointsOptions options, string sessionKey) =>
        BuildSessionPath(options, SessionDeletePath, sessionKey);

    /// <summary>Builds the unpinned-session bulk-delete path.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildDeleteUnpinnedPath(options);</code></example>
    public static string BuildDeleteUnpinnedPath(DashboardEndpointsOptions options) => BuildPath(options, DeleteUnpinnedPath);

    /// <summary>Builds the confirmed clear-all path.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildClearPath(options);</code></example>
    public static string BuildClearPath(DashboardEndpointsOptions options) => BuildPath(options, ClearPath);

    /// <summary>Builds the raw two-snapshot comparison path.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildComparePath(options);</code></example>
    public static string BuildComparePath(DashboardEndpointsOptions options) => BuildPath(options, ComparePath);

    /// <summary>Builds the computed analysis path.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildAnalyzePath(options);</code></example>
    public static string BuildAnalyzePath(DashboardEndpointsOptions options) => BuildPath(options, AnalyzePath);

    /// <summary>Builds the raw-snapshot JSON export path.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildExportPath(options, "sess0001", "node0001");</code></example>
    public static string BuildExportPath(DashboardEndpointsOptions options, string sessionKey, string nodeKey = null) =>
        BuildSelectionPath(options, ExportPath, sessionKey, nodeKey);

    /// <summary>Builds the complete-session portable archive path.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildSessionArchivePath(options, "sess0001");</code></example>
    public static string BuildSessionArchivePath(DashboardEndpointsOptions options, string sessionKey) =>
        BuildSessionPath(options, SessionArchivePath, sessionKey);

    /// <summary>Builds the complete-session Perfetto trace export path.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildSessionPerfettoPath(options, "sess0001");</code></example>
    public static string BuildSessionPerfettoPath(
        DashboardEndpointsOptions options,
        string sessionKey
    ) => BuildSessionPath(options, SessionPerfettoPath, sessionKey);

    /// <summary>Builds the selected-snapshot portable archive path.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildSnapshotArchivePath(options, "sess0001", "node0001", "snap0001");</code></example>
    public static string BuildSnapshotArchivePath(
        DashboardEndpointsOptions options,
        string sessionKey,
        string nodeKey,
        string snapshotKey
    ) =>
        BuildPath(
            options,
            SnapshotArchivePath
                .Replace("{sessionKey}", Uri.EscapeDataString(sessionKey ?? string.Empty), StringComparison.Ordinal)
                .Replace("{nodeKey}", Uri.EscapeDataString(nodeKey ?? string.Empty), StringComparison.Ordinal)
                .Replace("{snapshotKey}", Uri.EscapeDataString(snapshotKey ?? string.Empty), StringComparison.Ordinal)
        );

    /// <summary>Builds the portable archive import path.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildArchiveImportPath(options);</code></example>
    public static string BuildArchiveImportPath(DashboardEndpointsOptions options) =>
        BuildPath(options, ArchiveImportPath);

    private IResult Stress(HttpContext context)
    {
        var stress = GetService<IProfilingStressService>(context);
        if (stress is null)
        {
            return Unavailable("Profiling stress services are not registered.");
        }

        var applicationStopping = GetService<IHostApplicationLifetime>(context)
            ?.ApplicationStopping ?? CancellationToken.None;
        var result = stress.TryStart(ProfilingStressRequest.Default, applicationStopping);
        return result.Started
            ? Results.Accepted(value: result)
            : Results.Problem(
                "A Profiling stress workload is already running in this process.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Profiling stress already running"
            );
    }

    private async Task<IResult> GetStatusAsync(HttpContext context)
    {
        var control = GetService<IProfilingControlService>(context);
        return control is null
            ? Unavailable("Profiling control is not registered.")
            : ToHttpResult(await control.GetStatusAsync(context.RequestAborted).ConfigureAwait(false));
    }

    private async Task<IResult> ListSessionsAsync(HttpContext context)
    {
        var queries = GetService<IProfilingQueryService>(context);
        return queries is null
            ? Unavailable("Profiling queries are not registered.")
            : ToHttpResult(await queries.ListSessionsAsync(context.RequestAborted).ConfigureAwait(false));
    }

    private async Task<IResult> GetDataAsync(HttpContext context, string session, string node)
    {
        var control = GetService<IProfilingControlService>(context);
        var queries = GetService<IProfilingQueryService>(context);
        if (control is null || queries is null)
        {
            return Unavailable("Profiling dashboard services are not registered.");
        }

        if (!string.IsNullOrWhiteSpace(node) && string.IsNullOrWhiteSpace(session))
        {
            return ValidationProblem("A session key is required when selecting a node.");
        }

        var statusResult = await control.GetStatusAsync(context.RequestAborted).ConfigureAwait(false);
        if (statusResult.IsFailure)
        {
            return ToHttpResult(statusResult);
        }

        var sessionsResult = await queries.ListSessionsAsync(context.RequestAborted).ConfigureAwait(false);
        if (sessionsResult.IsFailure)
        {
            return ToHttpResult(sessionsResult);
        }

        ProfilingSessionData selectedSession = null;
        ProfilingNodeSessionData selectedNode = null;
        if (!string.IsNullOrWhiteSpace(session))
        {
            if (string.IsNullOrWhiteSpace(node))
            {
                var sessionResult = await queries.GetSessionAsync(session, context.RequestAborted).ConfigureAwait(false);
                if (sessionResult.IsFailure)
                {
                    return ToHttpResult(sessionResult);
                }

                selectedSession = sessionResult.Value;
            }
            else
            {
                var nodeResult = await queries.GetNodeSessionAsync(session, node, context.RequestAborted).ConfigureAwait(false);
                if (nodeResult.IsFailure)
                {
                    return ToHttpResult(nodeResult);
                }

                selectedNode = nodeResult.Value;
            }
        }

        return Results.Ok(
            new ProfilingDashboardDataResponse(
                statusResult.Value,
                sessionsResult.Value,
                selectedSession,
                selectedNode
            )
        );
    }

    private async Task<IResult> StartAsync(ProfilingDashboardStartRequest request, HttpContext context)
    {
        var control = GetService<IProfilingControlService>(context);
        return control is null
            ? Unavailable("Profiling control is not registered.")
            : ToHttpResult(
                await control.StartAsync(
                    new(request.Name, request.SamplingInterval, request.Duration, request.Tags),
                    context.RequestAborted
                ).ConfigureAwait(false)
            );
    }

    private async Task<IResult> StopAsync(HttpContext context)
    {
        var control = GetService<IProfilingControlService>(context);
        return control is null
            ? Unavailable("Profiling control is not registered.")
            : ToHttpResult(await control.StopAsync(context.RequestAborted).ConfigureAwait(false));
    }

    private async Task<IResult> SnapshotAsync(ProfilingDashboardSnapshotRequest request, HttpContext context)
    {
        var control = GetService<IProfilingControlService>(context);
        return control is null
            ? Unavailable("Profiling control is not registered.")
            : ToHttpResult(await control.SnapshotAsync(request.Name, context.RequestAborted).ConfigureAwait(false));
    }

    private async Task<IResult> CollectGarbageAsync(HttpContext context)
    {
        var control = GetService<IProfilingControlService>(context);
        return control is null
            ? Unavailable("Profiling control is not registered.")
            : ToHttpResult(await control.CollectGarbageAsync(context.RequestAborted).ConfigureAwait(false));
    }

    private async Task<IResult> AddMarkerAsync(ProfilingDashboardMarkerRequest request, HttpContext context)
    {
        var control = GetService<IProfilingControlService>(context);
        return control is null
            ? Unavailable("Profiling control is not registered.")
            : ToHttpResult(await control.AddPhaseMarkerAsync(request.Name, context.RequestAborted).ConfigureAwait(false));
    }

    private async Task<IResult> RestartAsync(string sessionKey, HttpContext context)
    {
        var queries = GetService<IProfilingQueryService>(context);
        return queries is null
            ? Unavailable("Profiling queries are not registered.")
            : ToHttpResult(await queries.RestartAsync(sessionKey, context.RequestAborted).ConfigureAwait(false));
    }

    private async Task<IResult> UpdateMetadataAsync(
        string sessionKey,
        ProfilingDashboardMetadataRequest request,
        HttpContext context
    )
    {
        var queries = GetService<IProfilingQueryService>(context);
        return queries is null
            ? Unavailable("Profiling queries are not registered.")
            : ToHttpResult(
                await queries.UpdateMetadataAsync(
                    sessionKey,
                    new(request.Name, request.Tags ?? [], request.Note, request.IsPinned),
                    context.RequestAborted
                ).ConfigureAwait(false)
            );
    }

    private async Task<IResult> DeleteSessionAsync(string sessionKey, HttpContext context)
    {
        var queries = GetService<IProfilingQueryService>(context);
        return queries is null
            ? Unavailable("Profiling queries are not registered.")
            : ToHttpResult(await queries.DeleteSessionAsync(sessionKey, context.RequestAborted).ConfigureAwait(false));
    }

    private async Task<IResult> DeleteUnpinnedAsync(HttpContext context)
    {
        var queries = GetService<IProfilingQueryService>(context);
        return queries is null
            ? Unavailable("Profiling queries are not registered.")
            : ToHttpResult(await queries.DeleteUnpinnedSessionsAsync(context.RequestAborted).ConfigureAwait(false));
    }

    private async Task<IResult> ClearAsync(ProfilingDashboardClearRequest request, HttpContext context)
    {
        if (!request.Confirmed)
        {
            return ValidationProblem("Explicit confirmation is required to remove all profiling data, including pinned sessions.");
        }

        var queries = GetService<IProfilingQueryService>(context);
        return queries is null
            ? Unavailable("Profiling queries are not registered.")
            : ToHttpResult(await queries.ClearAsync(true, context.RequestAborted).ConfigureAwait(false));
    }

    private async Task<IResult> CompareAsync(ProfilingDashboardCompareRequest request, HttpContext context)
    {
        var queries = GetService<IProfilingQueryService>(context);
        return queries is null
            ? Unavailable("Profiling queries are not registered.")
            : ToHttpResult(
                await queries.CompareSnapshotsAsync(
                    request.SessionKey,
                    request.NodeKey,
                    request.SnapshotAKey,
                    request.SnapshotBKey,
                    context.RequestAborted
                ).ConfigureAwait(false)
            );
    }

    private async Task<IResult> AnalyzeAsync(ProfilingDashboardAnalyzeRequest request, HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(request.SnapshotAKey) != string.IsNullOrWhiteSpace(request.SnapshotBKey))
        {
            return ValidationProblem("Snapshot A and snapshot B must be supplied together.");
        }

        var queries = GetService<IProfilingQueryService>(context);
        return queries is null
            ? Unavailable("Profiling queries are not registered.")
            : ToHttpResult(
                await queries.EvaluateAsync(
                    new(request.SessionKey, request.NodeKey, request.SnapshotAKey, request.SnapshotBKey),
                    context.RequestAborted
                ).ConfigureAwait(false)
            );
    }

    private async Task<IResult> ExportAsync(HttpContext context, string session, string node)
    {
        var queries = GetService<IProfilingQueryService>(context);
        if (queries is null)
        {
            return Unavailable("Profiling queries are not registered.");
        }

        var result = await queries.ExportSnapshotsJsonAsync(session, node, context.RequestAborted).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.Text(result.Value, "application/json", Encoding.UTF8)
            : ToHttpResult(result);
    }

    private async Task<IResult> ExportSessionArchiveAsync(
        string sessionKey,
        HttpContext context
    )
    {
        var archives = GetService<IProfilingArchiveService>(context);
        if (archives is null)
        {
            return Unavailable("Profiling archives are not registered.");
        }

        await using var stream = new MemoryStream();
        var result = await archives
            .ExportSessionAsync(sessionKey, stream, context.RequestAborted)
            .ConfigureAwait(false);
        return result.IsSuccess
            ? Results.File(
                stream.ToArray(),
                "application/json",
                $"profiling-session-{sessionKey}.json"
            )
            : ToHttpResult(result);
    }

    private async Task<IResult> ExportSnapshotArchiveAsync(
        string sessionKey,
        string nodeKey,
        string snapshotKey,
        HttpContext context
    )
    {
        var archives = GetService<IProfilingArchiveService>(context);
        if (archives is null)
        {
            return Unavailable("Profiling archives are not registered.");
        }

        await using var stream = new MemoryStream();
        var result = await archives
            .ExportSnapshotAsync(
                sessionKey,
                nodeKey,
                snapshotKey,
                stream,
                context.RequestAborted
            )
            .ConfigureAwait(false);
        return result.IsSuccess
            ? Results.File(
                stream.ToArray(),
                "application/json",
                $"profiling-snapshot-{snapshotKey}.json"
            )
            : ToHttpResult(result);
    }

    private async Task<IResult> ExportSessionPerfettoAsync(
        string sessionKey,
        HttpContext context
    )
    {
        var perfetto = GetService<IProfilingPerfettoExportService>(context);
        if (perfetto is null)
        {
            return Unavailable("Profiling Perfetto export is not registered.");
        }

        await using var stream = new MemoryStream();
        var result = await perfetto
            .ExportSessionAsync(sessionKey, stream, context.RequestAborted)
            .ConfigureAwait(false);
        return result.IsSuccess
            ? Results.File(
                stream.ToArray(),
                "application/json",
                $"profiling-session-{sessionKey}.perfetto.json"
            )
            : ToHttpResult(result);
    }

    private async Task<IResult> ImportArchiveAsync(HttpContext context)
    {
        var archives = GetService<IProfilingArchiveService>(context);
        if (archives is null)
        {
            return Unavailable("Profiling archives are not registered.");
        }

        if (!context.Request.HasFormContentType)
        {
            return ValidationProblem("A multipart Profiling archive upload is required.");
        }

        var form = await context.Request
            .ReadFormAsync(context.RequestAborted)
            .ConfigureAwait(false);
        var file = form.Files.GetFile("archive");
        if (file is null || file.Length == 0)
        {
            return ValidationProblem("A non-empty Profiling archive file is required.");
        }

        if (file.Length > ProfilingArchiveFormat.MaximumSizeBytes)
        {
            return ValidationProblem("The Profiling archive exceeds the 25 MiB size limit.");
        }

        await using var stream = file.OpenReadStream();
        return ToHttpResult(
            await archives.ImportAsync(stream, context.RequestAborted).ConfigureAwait(false)
        );
    }

    private static T GetService<T>(HttpContext context)
        where T : class => context.RequestServices.GetService<T>();

    private static IResult ToHttpResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        var errors = result.Errors;
        var statusCode = errors.Any(error => error is ProfilingDisabledError or ProfilingUnavailableError)
            ? StatusCodes.Status503ServiceUnavailable
            : errors.Any(error => error is NotFoundError)
                ? StatusCodes.Status404NotFound
                : errors.Any(error => error is ProfilingInvalidStateError or ProfilingSharedStoreRequiredError)
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status400BadRequest;
        var safeMessages = errors
            .Where(error => error is ProfilingDisabledError
                or ProfilingUnavailableError
                or ProfilingInvalidKeyError
                or ProfilingInvalidStateError
                or ProfilingSharedStoreRequiredError
                or ProfilingValidationError
                or ProfilingArchiveError
                or ProfilingTraceExportError
                or NotFoundError)
            .Select(error => error.Message)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToArray();
        var detail = safeMessages.Length == 0
            ? "The profiling operation failed."
            : string.Join(" ", safeMessages);
        return Results.Problem(
            detail,
            statusCode: statusCode,
            title: "Profiling operation failed"
        );
    }

    private static IResult ToHttpResult(Result result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok();
        }

        var generic = Result<bool>
            .Failure()
            .WithErrors(result.Errors)
            .WithMessages(result.Messages);
        return ToHttpResult(generic);
    }

    private static IResult Unavailable(string detail) =>
        Results.Problem(
            detail,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Profiling unavailable"
        );

    private static IResult ValidationProblem(string detail) =>
        Results.Problem(
            detail,
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid profiling request"
        );

    private static string BuildPath(DashboardEndpointsOptions options, string relativePath) =>
        DashboardPath.Combine(options?.GroupPath, relativePath);

    private static string BuildSessionPath(
        DashboardEndpointsOptions options,
        string relativePath,
        string sessionKey
    ) => BuildPath(options, relativePath.Replace("{sessionKey}", Uri.EscapeDataString(sessionKey ?? string.Empty), StringComparison.Ordinal));

    private static string BuildSelectionPath(
        DashboardEndpointsOptions options,
        string relativePath,
        string sessionKey,
        string nodeKey
    )
    {
        var values = new List<KeyValuePair<string, string>>();
        if (!string.IsNullOrWhiteSpace(sessionKey))
        {
            values.Add(new("session", sessionKey));
        }

        if (!string.IsNullOrWhiteSpace(nodeKey))
        {
            values.Add(new("node", nodeKey));
        }

        var path = BuildPath(options, relativePath);
        return values.Count == 0 ? path : path + QueryString.Create(values);
    }
}