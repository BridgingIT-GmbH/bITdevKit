---
title: Developer Dashboards
---

# Developer Dashboards

`bITdevKit` includes a server-rendered dashboard for inspecting local and internal application hosts. Developers, support engineers, and coding agents can use it to inspect registered features and runtime state.

[![Dashboard overview in Tokyo Night with feature status cards for profiling, jobs, logs, messaging, storage, and MCP](assets/dashboard/01-overview-overview.png)](assets/dashboard/01-overview-overview.png)

The overview brings feature status and recent activity together. Open a card to inspect that area, or select the image to view it at full size.

The default route is:

```text
/_bdk/dashboard
```

The dashboard shell comes from `Presentation.Web`. Feature packages and applications can contribute pages, navigation entries, and status cards.

The documentation theme picker uses the same 15 palettes as the dashboard, with Tokyo Night as the documentation default. Choices are saved in the browser; System follows Matte Black in dark mode and White in light mode.

## What the dashboard shows

Each page area links to its feature's dashboard guide, with screenshots, setup, and usage details. Available pages depend on the features registered by the application.

| Page area | Information |
| --- | --- |
| [Overview](reference/features-presentation-dashboard.md#dashboard-index) | feature status cards, recent activity, and shortcuts to each area |
| [Identity](reference/features-presentation-authentication.md#dashboard-identity) | the current principal, roles, claims, and authentication context |
| [System](reference/features-presentation-dashboard.md#system) | host metadata, process CPU, memory, traffic, thread-pool, and GC observations |
| [Health](reference/features-presentation-dashboard.md#health) | registered ASP.NET Core health checks and their current status |
| [Metrics](reference/features-metrics.md#dashboard-guide) | DevKit, .NET, and ASP.NET Core runtime metrics snapshots |
| [Profiling](reference/features-profiling.md#dashboard) | runtime snapshots, recorded operations and requests, timeline, insights, and comparisons |
| [Logs](reference/features-log-entries.md#search-persisted-logs) | retained log entries, filters, structured properties, and correlation links |
| [Errors](reference/features-log-entries.md#investigate-errors) | recent errors, exception details, and related logs |
| [Logs Stream](reference/features-log-entries.md#follow-a-live-stream) | live log entries in a browser terminal |
| [Console commands](reference/features-presentation-console-commands.md#dashboard-console) | host-local command execution from the browser shell |
| [Jobs](reference/features-jobs.md#dashboard-guide) | durable jobs, occurrences, history, dispatch state, and control actions |
| [Messaging](reference/features-messaging.md#dashboard-guide) | subscriptions, retained broker messages, handler state, and operational controls |
| [Queueing](reference/features-queueing.md#dashboard-guide) | queues, waiting work, retained messages, and operational controls |
| [Broadcasting](reference/features-broadcasting.md#dashboard-guide) | registered nodes, scopes, and live delivery diagnostics |
| [Orchestrations](reference/features-orchestrations.md#dashboard-guide) | instances, execution history, signals, timers, and control actions |
| [Files](reference/features-storage-files.md#dashboard-guide) | storage locations, files, transfers, and provider capabilities |
| [Documents](reference/features-storage-documents.md#dashboard-and-diagnostics) | registered document clients, stored content, and metadata |
| [Blobs](reference/features-storage-blobs.md#dashboard) | registered blob clients, storage health, and content inspection |
| [Permalinks](reference/features-storage-permalinks.md#dashboard-guide) | stored download links, expiration, and cleanup actions |
| [MCP](reference/features-cli-mcp.md#dashboard-inspection) | registered MCP handlers, operation schemas, active `bdk mcp` sessions, and runtime targeting |
| [Change History](reference/features-domain-change-history.md#dashboard-guide) | tracked entities, recorded changes, and filtered history |

## MCP runtime details

The MCP page shows whether a local `bdk mcp` server is connected to the current runtime and which operations the host advertises.

Use it to answer QA questions such as:

- Is MCP enabled for this host?
- Which MCP handlers are registered?
- Which toolsets and operation names are available?
- Is the current `bdk mcp` process connected to this runtime?
- Which project-owned operations are visible?
- What argument schema does an operation expect?

The page refreshes with the other dashboard pages and displays the current MCP session state.

## Add feature and project pages

Feature packages can contribute dashboard pages without editing the dashboard shell. Project modules can do the same for application-specific operations.

Define a dashboard page set for application-specific pages:

```csharp
public sealed class CatalogDashboard(DashboardEndpointsOptions options)
    : DashboardPageSet(options)
{
    protected override void Configure(DashboardPageSetBuilder pages)
    {
        pages.Group("Application")
            .Page("catalog", "/catalog")
                .Title("Catalog")
                .Icon("boxes")
                .Razor<CatalogOverviewPage>()
                .Card(card => ValueTask.FromResult(
                    card.Value("Ready", "Products and inventory diagnostics")));
    }
}
```

## Register the dashboard

Register the dashboard, then map the shared endpoint pipeline:

```csharp
using BridgingIT.DevKit.Presentation.Web.Dashboard;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDashboard();

var app = builder.Build();
app.MapEndpoints();
app.Run();
```

Feature packages can register their own dashboard plugins. Applications can explicitly add plugin assemblies when needed.

For the API and extension model, see:

- [Dashboard Reference](reference/features-presentation-dashboard.md)
- [Presentation Host Reference](reference/features-presentation.md)
- [DevKit MCP Reference](reference/features-cli-mcp.md)
