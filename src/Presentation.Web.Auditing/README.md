# Presentation.Web.Auditing

ASP.NET Core integration for the Auditing application services. HTTP capture, public read routes, dashboard discovery and standalone resource history compose independently.

Inside an existing authenticated host:

```csharp
services.AddAuditing()
    .WithHttpRequests()
    .WithEndpoints(); // Optional public API.

app.UseRouting();
app.UseAuthentication();
app.UseAuditing();
app.UseExceptionHandler();
app.UseAuthorization();
app.MapAuditingEndpoints();
```

Configure the host's normal exception handler/problem-details services and access rules. `WithEndpoints()` defaults to requiring authorization. Trusted `WithReadScope` constraints provide row isolation when selected; client filters do not provide authorization.

With `AddDashboard(...)`, the Auditing page/card/navigation is discovered automatically and uses global dashboard access and prefix settings. It works without public audit endpoints. Native attachment forms stream directly through the browser and poll only bounded companion metadata. The client must verify file bytes/counts/digest before advancing progress.

`ResourceHistoryModel` and its Razor slice can render through a host-owned route with neither Dashboard nor public audit endpoints. `AuditReviewFilter.FromRequest` supplies bounded UTC/archive/page controls; the host supplies the mandatory typed resource and access rules.

Read/search/export/metadata/diagnostics operations suppress their own capture. Scoped metadata does not expose global activity counts or publication positions to restricted callers.

`services.AddAuditing().AddConsoleCommands()` explicitly registers the read-only `auditing` group with `list`, `show`, `related`, `history`, `stats`, and `diagnostics`. It uses the host's console access rules and the audit query service's trusted read scope, exposure, and integrity policies. Public endpoints and the dashboard are optional. Use `auditing show <audit-id> --json` for permitted details or `auditing history Customer C-42` for a typed resource trail.

See the [feature guide](../../docs/features-application-auditing.md).
