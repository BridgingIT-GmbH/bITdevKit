---
title: Developer Dashboards
---

# Developer Dashboards

`bITdevKit` includes a server-rendered dashboard for inspecting local and internal application hosts. Developers, support engineers, and coding agents can use it to inspect registered features and runtime state.

The default route is:

```text
/_bdk/dashboard
```

The dashboard shell comes from `Presentation.Web`. Feature packages and applications can contribute pages, navigation entries, and status cards.

## What the dashboard shows

| Page area | Information |
| --- | --- |
| System overview | host metadata, environment, process, and runtime state |
| Health | registered ASP.NET Core health checks and their current status |
| Metrics | DevKit, .NET, and ASP.NET Core runtime metrics snapshots |
| Logs and errors | retained log entries, recent errors, and correlation diagnostics |
| Jobs | durable jobs, occurrences, history, dispatch state, and control actions |
| Messaging and queueing | subscriptions, waiting work, retained messages, and operational controls |
| Identity | development identity-provider and client diagnostics where enabled |
| Console commands | host-local command execution from the browser shell |
| MCP | registered MCP handlers, operation schemas, active `bdk mcp` sessions, and runtime targeting |

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
