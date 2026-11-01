# Application.Auditing

Record finite business executions with explicit safe context, actual nested actions and optional source-confirmed change evidence. The core package runs in a plain .NET host and has no EF, HTTP or source-feature dependency.

```csharp
services.AddAuditing(options => options.Application("CustomerPortal"));

await audit.RunAsync(options => options.Key("customers.rename"), (scope, token) =>
{
    scope.AddResource(new("Customer", "C-42"));
    return RenameCustomerAsync(token);
});
```

`AddAuditing()` installs bounded process-local memory storage and enables no source behavior. Select Requester, repositories, jobs and other contributors on their own builders. Use `AddNoOpAuditContext()` for optional injection without capture; metadata callbacks are skipped, while business delegates still run.

The package provides scoped queries, retention, optional external-key HMAC-SHA-256, whole-record NDJSON/CSV exports, bounded diagnostics and framework-neutral contracts. A Queued receipt, confirmed audit-store presence and client-verified export progress are distinct acknowledgments. ReportOnly audit failures preserve business results and can lose evidence.

For durable SQL Server/PostgreSQL/SQLite persistence, add IAuditingDbContext and its audit/control DbSets to the client's own context, select the existing EF infrastructure provider, and generate/deploy client-owned migrations including singleton control-state initialization. Entity classes carry their EF mappings; the feature supplies no concrete context or migration helpers. Add `Presentation.Web.Auditing` for optional read routes/dashboard/history.

The CEF formatter is included under `Formatters` and requires explicit registration:

```csharp
using BridgingIT.DevKit.Application.Auditing;

services.AddAuditing().WithExportFormatter(_ => new CefAuditExportFormatter(new()
{
    Vendor = "Northwind",
    Product = "CustomerPortal",
    Version = "1"
}));
```

Supply host vendor/product/version. CEF:0 output contains root and retained-action events with stable identities, severity mappings, escaping and loss counters. It omits structured details/deltas and the source HMAC envelope. SHA-256 covers exact output lines including LF; confirmed companion metadata requires successful output/cleanup and final source checks. CEF registration installs no capture, Syslog transport or SIEM client.

See the [feature guide](../../docs/features-application-auditing.md) for complete setup, source opt-ins, privacy, delivery, query scope, limits, export confirmation, and restore responsibilities.
