# ADR-0046: Use a Shared HTTP Problem Details Format

## Status

Accepted, 2026-11-24.

## Context

`Presentation.Web` depends on `Hellang.Middleware.ProblemDetails` to turn unhandled exceptions and eligible empty error responses into Problem Details. Failed Results already use DevKit adapters with ASP.NET Core. Those paths use different validation and diagnostic payloads, and the MVC Result adapter also differs from the Minimal API adapter in status mappings. Clients can therefore receive different formats for the same failure depending on how the endpoint reports it.

Removing Hellang reduces the number of dependencies DevKit maintains. The migration also gives applications a single format for validation and opt-in diagnostics across the two endpoint styles. The trade-off is that some existing clients must update their error parsing.

## Decision

Replace Hellang with ASP.NET Core exception handling and status-code pages. The quickstart uses `AddResponseProblemDetails()` and `UseResponseProblemDetails()`. The wrapper installs the native middleware in order. Hosts that need separate placement can use `UseExceptionHandler()` and `UseStatusCodePages()` directly. The distinct registration name avoids ambiguity at existing `AddProblemDetails(...)` call sites, and DevKit's `ProblemDetailsOptions` lives in `BridgingIT.DevKit.Presentation.Web.Errors`.

DevKit leaves native exception-page rewrites to the host by default. Mixed hosts can set `ShouldHandleRequest` to handle selected API failures before the native HTML exception page and to scope default status pages. The original request identifies the API during exception-page re-execution. Explicit Result mappings remain available across the host.

Keep the effective default exception status mappings. Use the existing Minimal API Result mapper as the authority for Result classification and reuse its rules for MVC. Explicit host mappings take precedence over module defaults regardless of registration order. Both precede the built-in mappings. A custom exception handler can use the registered `IProblemDetailsService` to receive shared enrichment. Custom writers control how the prepared problem is serialized.

Use `data.errors` for field validation and a structured `exceptionDetails` array for diagnostics in both paths. One `IncludeExceptionDetails` setting controls disclosure and defaults to `false`. Public Result data remains available under `data.result` where the mapper exposes it. Framework-generated MVC model-state errors still follow ASP.NET Core's format.

The selected status drives the HTTP response, the body, and DevKit logging. Enrichment hooks can customize the other Problem Details members. If no writer accepts an automatically handled API problem, return the public JSON problem with request identifiers. Explicit mapped Result problems retain their native JSON fallback.

## Breaking changes

### Host registration

- Removing the old `Configure.ProblemDetails(...)` overloads is a source and binary break for consumers that call them. Those signatures expose Hellang types. Replace the old registration, imports, and middleware with `AddResponseProblemDetails(...)` and the middleware above. Consumers compiled against the removed APIs must rebuild.
- Choose either the existing full DevKit `AddExceptionHandler(...)` chain or the new combined registration. A host that registers both fails at startup. Individual native `AddExceptionHandler<THandler>()` registrations still work with the new setup.
- Move custom exception mappings into DevKit options. During migration, check whether a legacy mapping was shadowed before enabling it in the new setup.
- Configure a common diagnostic disclosure policy when adopting the combined registration. If the old host used different settings for exceptions and Results, the combined setting replaces that split. Result-only hosts can still use `AddResultHttpProblemDetails(bool)` and receive the shared diagnostic format.
- The combined registration logs mapped 5xx Problem Details and custom MVC Result responses by default. Set `ShouldLogResultErrors = false` to disable this log. An explicit mapper logger takes precedence and can also log failures mapped to the native `InternalServerError` arm.
- Hosts that use one native exception middleware for HTML and API requests can configure the API request scope to give DevKit first refusal for API exceptions.
- Move status changes from response customization hooks into mappings or handlers. Enrichment can change the other Problem Details fields.

#### Before migration

This API host enables exception diagnostics only in Development. Hellang handles exceptions and empty error responses. The separate DevKit registration prepares failed Result responses.

```csharp
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Hellang.Middleware.ProblemDetails;
using HellangProblemDetailsOptions = Hellang.Middleware.ProblemDetails.ProblemDetailsOptions;

var builder = WebApplication.CreateBuilder(args);
var includeExceptionDetails = builder.Environment.IsDevelopment();

builder.Services.AddProblemDetails((HellangProblemDetailsOptions options) =>
{
    Configure.ProblemDetails(options, includeExceptionDetails: includeExceptionDetails);
    options.IncludeExceptionDetails = (_, _) => includeExceptionDetails;
    options.OnBeforeWriteDetails = (context, problem) =>
        problem.Extensions["correlationId"] =
            context.TryGetCorrelationId() ?? context.TraceIdentifier;
});
builder.Services.AddResultHttpProblemDetails(includeExceptionDetails);

var app = builder.Build();
app.UseRouting();
app.UseRequestCorrelation();
app.UseProblemDetails();
```

#### After migration

```csharp
using BridgingIT.DevKit.Presentation.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddResponseProblemDetails(options =>
{
    options.IncludeExceptionDetails = builder.Environment.IsDevelopment();
});

var app = builder.Build();
app.UseRouting();
app.UseRequestCorrelation();
app.UseResponseProblemDetails();
```

One option now controls diagnostic disclosure for both paths. DevKit adds `correlationId` by default. Use `OnBeforeWriteDetails` to carry over application-specific enrichment.

### HTTP responses

- `exceptionDetails` changes from an array of strings in Result responses to an array of objects with `type` and `message`. The new objects have optional `stackTrace` and `innerExceptions` fields. Thrown-exception diagnostics use the same objects and drop Hellang's `raw` and `stackFrames`.
- Catch-all exception responses keep the generic public `detail` even when diagnostics are enabled. Read exception text and stacks from `exceptionDetails`. Catch-all `data.errors` no longer carries diagnostic entries. Empty `data` is omitted.
- Built-in mappings use fixed public messages in `detail`. Clients that previously read raw exception messages need to use structured fields or an application-defined public message. Validation still exposes field messages.
- `NotSupportedException` follows native ASP.NET Core handling. Before the response starts, the default handler completes the 500 response without Hellang's post-write rethrow. Exceptions raised after the response starts follow the framework's native propagation and abort rules.
- DevKit validation uses the same public title and detail in both paths. Read `data.errors` as a map of field names to message arrays. Keys strip a leading `Entity.` prefix and use `validation` for unnamed fields.
- MVC Result failures adopt the Minimal API classification and payload. The former top-level `IsSuccess`, `Messages`, and `Errors` fields give way to the shared fields where applicable. Conflict, timeout, and cancellation map to 409, 504, and 499 respectively, replacing the old MVC fallback of 500 for those failures.
- When no registered writer accepts an automatic exception or empty-status response, the JSON fallback skips enrichment hooks and omits `exceptionDetails`, even when disclosure is enabled. Hellang could emit its customized JSON for the same request.
- The OpenAPI response schema describes `data.errors` as a map to arrays of strings and `exceptionDetails` as an optional array of objects. Regenerate affected clients that consume this schema.

## Alternatives considered

- **Keep Hellang.** Retains the current contract and avoids a migration. DevKit would keep the dependency, and clients would still need to handle the different error formats.
- **Replace Hellang and preserve each response shape.** Reduces the dependency count and protects existing client parsers. It keeps separate diagnostic formats and leaves DevKit maintaining special cases for legacy output.
- **Use native ASP.NET Core output for all failures.** Minimizes DevKit response code and matches framework defaults. It would change more of the existing behavior, including DevKit's Result payload and exception mappings that applications rely on.

## Consequences

Clients can use the same validation and diagnostic parser for DevKit-managed errors in Minimal APIs and MVC. DevKit removes Hellang and relies on the framework for middleware execution while retaining application-specific mapping.

Existing clients, response schemas, and contract tests that depend on the old fields or MVC status codes need updates. Release notes must list the breaks. Verify the serialized contract across both endpoint styles with diagnostics enabled and disabled. Compare middleware behavior against the legacy baseline, including authentication headers and exceptions raised after a response starts.

Verify API errors and HTML error pages in the same test host. Compare generated OpenAPI responses with HTTP responses before removing the dependency.

## References

- [HTTP Problem Details specification and response examples](../specs/spec-presentation-problem-details.md)
- [ADR-0002 on the Result pattern for error handling](0002-result-pattern-error-handling.md)
- [ADR-0014 on Minimal API endpoints](0014-minimal-api-endpoints-dto-exposure.md)
