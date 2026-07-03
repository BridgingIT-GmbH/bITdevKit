---
title: Developer Dashboards
---

# Developer Dashboards

`bITdevKit` includes a server-rendered developer dashboard for local and internal host insight. It gives developers, support engineers and coding agents a quick way to inspect the runtime surface exposed by a DevKit application.

The default route is:

```text
/_bdk/dashboard
```

Dashboards are modular. The shell comes from `Presentation.Web`, while feature packages and applications contribute their own pages, navigation entries and live cards.

## What the dashboard shows

The dashboard is built for day-to-day development and QA work:

| Page area | What it helps inspect |
| --- | --- |
| System overview | host metadata, environment, process and runtime state |
| Health | registered ASP.NET Core health checks and their current status |
| Metrics | DevKit, .NET and ASP.NET Core runtime metrics snapshots |
| Logs and errors | retained log entries, recent errors and correlation diagnostics |
| Jobs | durable jobs, occurrences, history, dispatch state and control actions |
| Messaging and queueing | subscriptions, waiting work, retained messages and operational controls |
| Identity | development identity-provider and client diagnostics where enabled |
| Console commands | host-local command execution from the browser shell |
| MCP | registered MCP handlers, operation schemas, active `bdk mcp` sessions and runtime targeting |

## MCP dashboard insight

The MCP dashboard page is especially useful when working with AI agents. It shows whether a local `bdk mcp` server is connected to the current runtime and what operations the host advertises.

Use it to answer QA questions such as:

- Is MCP enabled for this host?
- Which MCP handlers are registered?
- Which toolsets and operation names are available?
- Is the current `bdk mcp` process connected to this runtime?
- Which project-owned operations are visible?
- What argument schema does an operation expect?

The page refreshes like the other dashboard pages, so it can show live MCP session state while an agent is running.

## Extensible by feature and project

Feature packages can contribute dashboard pages without editing the dashboard shell. Project modules can do the same for application-specific operations.

The recommended pattern is a dashboard page set:

```csharp
public sealed class CatalogDashboard : DashboardPageSet
{
    protected override void Build(DashboardPageSetBuilder pages)
    {
        pages.MapPage<CatalogOverviewPage>("/catalog", "Catalog")
            .Icon("boxes")
            .Group("Application")
            .Card(async services => new DashboardPageCard
            {
                Title = "Catalog",
                Value = "Ready",
                Detail = "Products and inventory diagnostics"
            });
    }
}
```

## Register the dashboard

The dashboard is usually enabled through the DevKit web host setup:

```csharp
var builder = DevKitWebApplication.CreateBuilder(args)
    .AddConfiguration()
    .AddLogging()
    .AddModules(c => c.WithModule<CoreModule>());
```

Feature packages can register their own dashboard plugins when the relevant package is used. Applications can explicitly add plugin assemblies when needed.

For the full API and extension model, see:

- [Dashboard Reference](reference/features-presentation-dashboard.md)
- [Presentation Host Reference](reference/features-presentation.md)
- [DevKit MCP Reference](reference/features-cli-mcp.md)
