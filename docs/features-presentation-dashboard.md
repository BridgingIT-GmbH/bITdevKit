# Presentation Dashboard Feature Documentation

> Host developer dashboard pages as a modular shell with pluggable RazorSlice pages, grouped navigation, and live-updating dashboard cards.

[TOC]

## Overview

The Dashboard feature provides a server-rendered developer dashboard under a configurable route, by default `/_bdk/dashboard`. The shell owns the layout, sidebar, grouping, card index, and common styling. Feature packages and application projects contribute their own pages through small plugin contracts.

Dashboard pages are Minimal API endpoints that render [RazorSlices](https://github.com/DamianEdwards/RazorSlices). Pages can also publish navigation metadata and an optional compact card for the dashboard index page. Cards are refreshed in-place by a dashboard HTML fragment endpoint, so the index can show current plugin state without reloading the full shell.

### Challenges

- Extensibility: Let bITdevKit packages and application projects add dashboard pages without editing the dashboard shell.
- Navigation: Keep the sidebar aware of plugged-in pages and group pages by feature area.
- Server rendering: Render pages directly from in-process services instead of routing through HTTP JSON APIs.
- Live overview: Let plugin cards update on the dashboard index without a full page reload.
- External assemblies: Support dashboard plugins that live outside `Presentation.Web`.

### Solution

- Shell: `AddDashboard(...)` registers the dashboard shell and discovers dashboard plugins.
- Endpoints: `IDashboardEndpoints` marks endpoint classes that map dashboard routes.
- Pages: RazorSlice pages render dashboard content using normal Razor syntax and optional layout models.
- Navigation and cards: `IDashboardPageProvider` contributes sidebar items and optional dashboard index cards.
- Page sets: `DashboardPageSet` lets modules define multiple pages, content fragments, page-local actions, navigation, and cards from one builder.
- Helpers: `MapDashboardPage(...)`, `DashboardPath.Combine(...)`, and `Results.Extensions.DashboardRazorSlice(...)` simplify plugin page mapping.

## Core Contracts

- `DashboardEndpointsOptions` ([src/Presentation.Web/Dashboard/DashboardEndpointsOptions.cs](src/Presentation.Web/Dashboard/DashboardEndpointsOptions.cs))
  - `Enabled`: Enables or disables the dashboard.
  - `GroupPath`: Base dashboard path. Defaults to `/_bdk/dashboard`.
  - `GroupTag`: Endpoint group tag.
  - `PluginAssemblies`: Additional assemblies to scan for dashboard plugins.
- `IDashboardEndpoints` ([src/Presentation.Web/Dashboard/IDashboardEndpoints.cs](src/Presentation.Web/Dashboard/IDashboardEndpoints.cs))
  - Marker contract for dashboard-specific endpoint sets.
  - Extends the regular presentation `IEndpoints` contract.
- `IDashboardPageProvider` ([src/Presentation.Web/Dashboard/IDashboardPageProvider.cs](src/Presentation.Web/Dashboard/IDashboardPageProvider.cs))
  - Provides `DashboardPage` descriptors for sidebar navigation and optional index cards.
- `DashboardPageSet` ([src/Presentation.Web/Dashboard/DashboardPageSet.cs](src/Presentation.Web/Dashboard/DashboardPageSet.cs))
  - Recommended base for module dashboards that own one or more pages.
  - Implements endpoint mapping and page metadata from a single `DashboardPageSetBuilder` declaration.
- `DashboardPage` ([src/Presentation.Web/Dashboard/DashboardPage.cs](src/Presentation.Web/Dashboard/DashboardPage.cs))
  - Defines title, icon, URL, group, ordering, sidebar visibility, optional badge, and optional card provider.
- `DashboardPageCard` ([src/Presentation.Web/Dashboard/DashboardPage.cs](src/Presentation.Web/Dashboard/DashboardPage.cs))
  - Defines compact card content for the dashboard index.
- Route helpers ([src/Presentation.Web/Dashboard/DashboardRouteBuilderExtensions.cs](src/Presentation.Web/Dashboard/DashboardRouteBuilderExtensions.cs))
  - `MapDashboardPage<TPage>(...)`: Maps a typed RazorSlice page.
  - `MapDashboardPage(..., razorIdentifier, assembly, ...)`: Maps a compiled RazorSlice by identifier from a plugin assembly.
- Path helper ([src/Presentation.Web/Dashboard/DashboardPath.cs](src/Presentation.Web/Dashboard/DashboardPath.cs))
  - `DashboardPath.Combine(...)`: Combines route segments with one slash.

## Architecture

### Class Diagram

```mermaid
classDiagram
  class DashboardEndpointsOptions {
    +string GroupPath
    +List~Assembly~ PluginAssemblies
  }
  class IDashboardEndpoints {
    +Map(IEndpointRouteBuilder)
  }
  class IDashboardPageProvider {
    +GetPages(HttpContext)
  }
  class DashboardPage {
    +string Title
    +string Icon
    +string Url
    +string Group
    +bool ShowInSidebar
    +bool ShowOnIndex
    +Func Badge
    +Func Card
  }
  class DashboardPageCard {
    +string Title
    +string Subtitle
    +string Value
    +string Detail
    +string Url
  }
  class DashboardShell
  class DashboardPlugin

  IDashboardEndpoints <|.. DashboardPlugin
  IDashboardPageProvider <|.. DashboardPlugin
  DashboardPlugin ..> DashboardPage
  DashboardPage ..> DashboardPageCard
  DashboardShell ..> IDashboardEndpoints : maps
  DashboardShell ..> IDashboardPageProvider : navigation/cards
  DashboardShell ..> DashboardEndpointsOptions
```

### Sequence (Registration → Render)

```mermaid
sequenceDiagram
  participant Startup as Program.cs
  participant DI as IServiceCollection
  participant App as WebApplication
  participant Shell as Dashboard shell
  participant Plugin as Dashboard plugin
  participant Browser as Browser

  Startup->>DI: AddDashboard(options)
  DI->>DI: Scan dashboard plugin assemblies
  DI->>DI: Register IDashboardEndpoints
  DI->>DI: Register IDashboardPageProvider
  Startup->>App: app.MapEndpoints()
  App->>Plugin: Map dashboard plugin routes
  Browser->>Shell: GET /_bdk/dashboard
  Shell->>Plugin: GetPages(HttpContext)
  Plugin-->>Shell: DashboardPage + card providers
  Shell-->>Browser: dashboard shell + cards
  Browser->>Shell: GET /_bdk/dashboard/content
  Shell->>Plugin: refresh card providers
  Shell-->>Browser: card HTML fragment
```

## Getting Started

### Register the Dashboard

Register the dashboard during service configuration.

```csharp
builder.Services.AddDashboard(options =>
{
    options.WithGroupPath("/_bdk/dashboard");
});
```

Map registered endpoints once during application startup.

```csharp
var app = builder.Build();

app.MapEndpoints();
```

The dashboard uses the existing endpoint registration pipeline. `AddDashboard(...)` registers dashboard endpoint classes with `AddEndpoints(...)`; `app.MapEndpoints()` maps them.

### Include Plugin Assemblies

The dashboard scans the core dashboard assembly, explicitly configured plugin assemblies, and currently loaded assemblies containing dashboard contracts. For application plugins, prefer explicit registration so discovery does not depend on load order.

```csharp
builder.Services.AddDashboard(options =>
{
    options.WithPluginAssemblyContaining<CatalogDashboardEndpoints>();
});
```

For multiple assemblies:

```csharp
builder.Services.AddDashboard(options =>
{
    options.WithPluginAssemblies(
        typeof(CatalogDashboardEndpoints).Assembly,
        typeof(ReportingDashboardEndpoints).Assembly);
});
```

### Built-In Routes

The dashboard shell uses fixed built-in routes below the configured `GroupPath`. Plugin pages own and map their own route segments; the shell does not maintain a central list of plugin paths.

| Path | Default |
| --- | --- |
| Dashboard index | `/_bdk/dashboard` |
| Dashboard index content fragment | `/_bdk/dashboard/content` |
| Health page | `/_bdk/dashboard/health` |
| Health content fragment | `/_bdk/dashboard/health/content` |
| Metrics page | `/_bdk/dashboard/metrics` |
| Metrics content fragment | `/_bdk/dashboard/metrics/content` |
| Identity page | `/_bdk/dashboard/identity` |
| Identity client credentials login | `/_bdk/dashboard/identity/client-credentials/login` |

## Built-In Pages

### Dashboard Index

The dashboard index shows cards contributed by dashboard page providers. It includes a refresh interval dropdown with fixed intervals:

- `Off`
- `1 sec`
- `5 sec`
- `15 sec`
- `30 sec`

The selected interval is stored in `localStorage`. Refresh uses a recursive `setTimeout` loop and an `AbortController`, so refreshes do not overlap and an in-flight request is cancelled when the interval changes. The page pauses automatic refresh while the browser tab is hidden and refreshes once when the tab becomes visible again.

The index refresh endpoint returns only card HTML. It does not render the full dashboard layout.

### Metrics

The metrics page is server-rendered and reads in-process snapshot services directly. It does not call the metrics JSON endpoints. The page has its own content fragment endpoint and refresh controls for updating only the metrics body.

Metrics remain optional. Registering the dashboard does not automatically register metrics; applications opt into metrics separately.

### Health

The health page is server-rendered and invokes the registered ASP.NET Core health checks through `HealthCheckService` from `Microsoft.Extensions.Diagnostics.HealthChecks`. It does not call a `/healthz` endpoint.

The page shows the overall status, number of registered checks, unhealthy count, total duration, and a compact table of health check entries. It has its own content fragment endpoint and refresh controls for updating only the health body.

Register health checks in the host application with the standard ASP.NET Core API:

```csharp
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy());
```

If `AddHealthChecks()` is not registered, the page and card show an unavailable state instead of failing the dashboard.

### Identity

The identity page displays current user information using the current user accessor and request principal. When the fake identity provider is registered, the page can show a client credentials login action. If the fake provider is not available, fake-provider-specific UI is hidden.

## Adding Project-Specific Dashboard Pages

For new project or module pages, prefer `DashboardPageSet`. A page set lets one module declare all of its dashboard pages, content fragments, local action routes, sidebar metadata, and index cards in one class. The low-level `IDashboardEndpoints` and `IDashboardPageProvider` contracts remain available for advanced or unusual plugins.

### Folder Layout

```text
# Presentation.Web

Modules/
  Catalog/
    Dashboard/
      CatalogDashboard.cs
      Pages/
        Overview.cshtml
        OverviewContent.cshtml
        ProductManagement.cshtml
        ProductManagementContent.cshtml
        _ViewImports.cshtml
```

### Project Package Reference

The application or plugin assembly that owns the `.cshtml` files must reference `RazorSlices` directly so RazorSlice proxy types are generated for that assembly. With central package management, add the reference without a version:

```xml
<ItemGroup>
  <PackageReference Include="RazorSlices" />
</ItemGroup>
```

### Page Set

Use one page set per module. Each page owns its display route, optional content fragments, optional dashboard index card, and optional local actions.

```csharp
namespace MyApp.Modules.Catalog.Dashboard;

using BridgingIT.DevKit.Presentation.Web.Dashboard;

public sealed class CatalogDashboard(DashboardEndpointsOptions options)
    : DashboardPageSet(options)
{
    protected override void Configure(DashboardPageSetBuilder pages)
    {
        pages.WithTags("_bdk.Dashboard.Catalog");

        pages.Group("Catalog", order: 100)
            .Page("overview", "/catalog")
                .Title("Overview")
                .Icon("boxes")
                .Order(0)
                .Description("Catalog overview")
                .Razor<Pages.Overview>()
                .Content<Pages.OverviewContent>()
                .Card(GetOverviewCardAsync)
            .Page("product-management", "/catalog/products")
                .Title("Product Management")
                .Icon("box-seam")
                .Order(10)
                .Description("Manage catalog products")
                .Razor<Pages.ProductManagement>()
                .Content<Pages.ProductManagementContent>()
                .HideFromIndex()
                .Post("/create", CreateProductAsync)
                    .Name("_bdk.Dashboard.Catalog.ProductCreate");
    }

    private static async ValueTask<DashboardPageCard> GetOverviewCardAsync(DashboardPageCardContext card)
    {
        var requester = card.HttpContext.RequestServices.GetService<IRequester>();
        if (requester is null)
        {
            return card.Unavailable("Requester unavailable");
        }

        var result = await requester.SendAsync(new ProductSummaryQuery(), cancellationToken: card.HttpContext.RequestAborted);
        return result.IsSuccess
            ? card.Value(result.Value.ProductCount.ToString(CultureInfo.InvariantCulture), "products")
            : card.Error(result.Errors.FirstOrDefault()?.Message ?? "Could not load products");
    }
}
```

The builder derives:

- endpoint mappings for the page and content fragments
- absolute dashboard URLs
- sidebar items
- default index card metadata
- endpoint names, summaries, and descriptions
- page-local action routes such as `/catalog/products/create`

Use `.HideFromSidebar()` for utility pages and `.HideFromIndex()` for pages that should not appear as index cards.

### RazorSlice Imports

For project-local dashboard pages, add a `_ViewImports.cshtml` beside the pages.

```razor
@using BridgingIT.DevKit.Presentation.Web.Dashboard
@using BridgingIT.DevKit.Presentation.Web.Dashboard.Pages
@using Microsoft.Extensions.DependencyInjection
@using RazorSlices

@tagHelperPrefix __disable_tagHelpers__:
@removeTagHelper *, Microsoft.AspNetCore.Mvc.Razor
```

RazorSlices currently do not support Tag Helpers in this repo because warnings are treated as errors and RazorSlices marks Tag Helper APIs obsolete. Use RazorSlice base classes and normal Razor markup instead.

### RazorSlice Page

Use `DashboardPageSlice` for full dashboard pages and `DashboardContentSlice` for content fragments. The page can resolve services directly because it is server-rendered.

```razor
@using MyApp.Modules.Catalog.Application
@inherits DashboardPageSlice

@{
    var contentPath = this.Dashboard.Url("overview", "content");
}

<div class="d-flex justify-content-between align-items-center py-2 mb-2 border-bottom">
    <div>
        <h4 class="m-0">Catalog</h4>
        <div class="text-muted small">Product inventory overview</div>
    </div>
    <span class="text-muted small">Updated @DateTimeOffset.UtcNow.LocalDateTime.ToString("T", CultureInfo.InvariantCulture)</span>
</div>

<div id="catalog-overview-content">
    <div class="text-muted small py-2">Loading catalog content...</div>
</div>

<script>
    (() => {
        window.bdkDashboard.createRefresher({
            contentUrl: '@contentPath',
            contentSelector: '#catalog-overview-content'
        }).refresh(true);
    })();
</script>

@functions {
    public override string PageTitle => "Catalog";
}
```

### Page Descriptor Guidance

- `.Title(...)`: Display name in sidebar and default card title.
- `.Icon(...)`: Bootstrap icon name without the `bi-` prefix.
- `.Group(...)`: Sidebar/card group heading and group sort order.
- `.Order(...)`: Sort order inside the group.
- `.HideFromSidebar()`: Use for utility pages.
- `.HideFromIndex()`: Use for pages that should not create dashboard cards.
- `.Badge(...)`: Optional async count shown in the sidebar.
- `.Card(...)`: Optional async card provider for the dashboard index.
- `.Razor<TPage>()`: Main typed RazorSlice page.
- `.Content<TPage>()`: Optional typed refreshable RazorSlice fragment below the page route.
- `.Get(...)`, `.Post(...)`, `.Put(...)`, `.Delete(...)`: Optional page-local Minimal API action routes.

Page sets are called at render time to provide navigation and cards. Keep badge and card work lightweight, use in-process services, and handle missing optional services gracefully.

### Advanced Manual Mapping

For advanced cases, implement `IDashboardEndpoints` to map custom routes and `IDashboardPageProvider` to contribute sidebar/card metadata manually. Use `MapDashboardPage<TPage>(...)` for typed RazorSlices or `MapDashboardPage(..., razorIdentifier, assembly, ...)` when the generated RazorSlice type is awkward to reference.

## Dashboard Index Cards

Cards are rendered on the dashboard index and refreshed through the index content fragment endpoint. A card provider can return live values such as counts, health summaries, queue depth, or last activity.

If a page has `ShowOnIndex = true` but no `Card` delegate, the shell can still produce a default card from page metadata. Define `Card` when the plugin has useful summary data.

The dashboard catches page provider/card failures and keeps rendering the rest of the dashboard. Failed card providers are logged and replaced with an unavailable card state.

## Sidebar Grouping

The sidebar groups pages by `DashboardPage.Group`. Groups are visually separated. Built-in bITdevKit pages use the `bdk` group. Application pages should use an application-specific group such as `Application`, `Catalog`, `Operations`, or the module name.

Use `GroupOrder` to place groups predictably:

```csharp
new DashboardPage("Orders", "receipt", "/_bdk/dashboard/orders")
{
    Group = "Application",
    GroupOrder = 100,
    Order = 20
};
```

## Refresh Strategy

The dashboard shell uses fragment endpoints for refreshable regions:

- `/_bdk/dashboard/content`: dashboard index cards.
- `/_bdk/dashboard/health/content`: health body.
- `/_bdk/dashboard/metrics/content`: metrics body.

The browser-side refresh strategy:

- Stores interval selection in `localStorage`.
- Uses `fetch()` to request server-rendered HTML fragments.
- Replaces only the content container `innerHTML`.
- Uses recursive `setTimeout` instead of `setInterval` to avoid overlapping refreshes.
- Uses `AbortController` to cancel in-flight requests when the interval changes.
- Keeps previous content visible on failure and updates status text.
- Pauses automatic refresh while `document.hidden` is `true`.

Project-specific pages can use the same pattern when only part of a page should update. Add a fragment RazorSlice, map a second dashboard endpoint, and replace a scoped content container from JavaScript.

## External Plugin Assemblies

Dashboard plugins can live in separate packages or application assemblies. A plugin assembly should provide:

- One or more `DashboardPageSet` implementations, or advanced manual `IDashboardEndpoints` implementations.
- Optional manual `IDashboardPageProvider` implementations when not using `DashboardPageSet`.
- RazorSlice pages compiled into the plugin assembly. Reference `RazorSlices` directly when using typed `.Razor<TPage>()` and `.Content<TPage>()` page declarations.

Register the plugin assembly explicitly from the host:

```csharp
builder.Services.AddDashboard(options =>
{
    options.WithPluginAssemblyContaining<MyPluginDashboard>();
});
```

The dashboard scans configured plugin assemblies for endpoint and page provider implementations. It also scans currently loaded assemblies as a convenience, but explicit registration is recommended for reusable packages.

## Troubleshooting

- Dashboard returns 404: Ensure `builder.Services.AddDashboard(...)` is called and `app.MapEndpoints()` is executed.
- Plugin page route is missing: Ensure the plugin assembly is loaded and registered with `WithPluginAssemblyContaining<T>()`.
- Sidebar item is missing: Ensure an `IDashboardPageProvider` implementation is concrete, public, and in a scanned assembly.
- Card does not appear: Ensure `ShowOnIndex` is `true` and the page provider returns a page. Check logs for provider/card exceptions.
- Typed RazorSlice type is missing: Verify the application or plugin assembly references `RazorSlices` directly, and prefer unique page-specific `.cshtml` filenames such as `Overview.cshtml` and `OverviewContent.cshtml`.
- Path-based RazorSlice cannot render: Verify the RazorSlice identifier and assembly. The identifier is usually the project-relative `.cshtml` path with a leading slash.
- Authorization behaves differently than expected: Dashboard routes are endpoint routes. Apply authorization through the inherited endpoint options and `MapGroup(...)` behavior, or require authorization on the mapped route group.
- Refresh shows stale content: Confirm the fragment endpoint returns updated server-rendered HTML and that the browser interval is not set to `Off`.

## Appendix A — Minimal Plugin

The following is the smallest useful dashboard plugin with the recommended page-set API: one route, one page, one sidebar item, and one card.

```csharp
public sealed class HealthDashboard(DashboardEndpointsOptions options)
    : DashboardPageSet(options)
{
    protected override void Configure(DashboardPageSetBuilder pages)
    {
        pages.Group("Application", 100)
            .Page("health", "/health")
                .Title("Health")
                .Icon("activity")
                .Order(0)
                .Razor<Pages.Health>()
                .Card(card => ValueTask.FromResult(
                    card.Value("OK", "Application is responding", "System")));
    }
}
```

```razor
@inherits DashboardPageSlice

<div class="d-flex justify-content-between align-items-center py-2 mb-2 border-bottom">
    <h4 class="m-0">Health</h4>
    <span class="text-muted small">Updated @DateTimeOffset.UtcNow.LocalDateTime.ToString("T", CultureInfo.InvariantCulture)</span>
</div>

<div class="card">
    <div class="card-body p-3">
        <h6 class="card-title">Application</h6>
        <div class="fs-4 fw-semibold">OK</div>
    </div>
</div>

@functions {
    public override string PageTitle => "Health";
}
```

Register external plugin assemblies explicitly:

```csharp
builder.Services.AddDashboard(options =>
{
    options.WithPluginAssemblyContaining<HealthDashboard>();
});
```
