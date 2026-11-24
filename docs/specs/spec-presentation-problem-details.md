---
created: 2026-11-23
status: implemented
---

# DevKit HTTP Problem Details

## Goal and compatibility policy

Provide a DevKit-owned Problem Details solution that replaces Hellang and defines a shared HTTP error format for thrown exceptions and failed Results in Minimal APIs and MVC.

The shared format intentionally changes existing responses where needed for alignment. Use the current Minimal API Result mapper for failure classification and align MVC Result mapping with it. Keep the effective exception status mappings as the defaults.

The architecture decision records the trade-off and the breaking changes for hosts and clients. [ADR-0046](../adr/0046-shared-http-problem-details.md)

## Current implementation

### Two registrations, several response paths

The existing host setup registers two different systems:

- Hellang's `AddProblemDetails` configures exceptions through the former `Configure.ProblemDetails` helper. Its `UseProblemDetails()` middleware catches downstream exceptions and fills eligible empty error responses. Its final write hook adds `correlationId`.
- [`AddResultHttpProblemDetails`](../../src/Presentation.Web/Result/ResultHttpServiceCollectionExtensions.cs) registers Microsoft's Problem Details service, configures Result diagnostic disclosure, and composes the native customization callback. It is part of the Problem Details feature, not an unrelated Result registration to remove.

Minimal API Result processing follows [`ResultMapHttpExtensions`](../../src/Presentation.Web/Result/Map/ResultMapHttpExtensions.cs), [`ResultHttpProblemDetails`](../../src/Presentation.Web/Result/ResultHttpProblemDetails.cs), and the native `ProblemHttpResult` writer. MVC Result processing follows [`DefaultActionResultMapper`](../../src/Presentation.Web/Result/DefaultActionResultMapper.cs), prepares the response explicitly, and invokes MVC's `ObjectResult` executor.

The native customization callback runs for supported Minimal API problem writes. It does not automatically run for the existing MVC Result `ObjectResult` path. Framework-generated MVC model-state problems have their own factory path, which can invoke that callback.

Hellang leaves already-written Result responses alone. Its callback is therefore not what adds correlation or diagnostics to failed Results. Replacing only that callback would miss a substantial part of the feature.

### Current exception mappings

The former `Configure.ProblemDetails.cs` defined these effective mappings for hosts using the helper.

- `ValidationException` returns 400, title `Validation Error`, detail `[ValidationException] A validation error has occurred`, and `data.errors`. Messages are sorted and grouped by property, with the case-insensitive `Entity.` prefix removed.
- `RuleException` and `DomainPolicyException` return 400 with title `Bad Request`, a type-prefixed exception message in `detail`, and empty `data`.
- `SecurityException` returns 401 with title `Unauthorized`, a type-prefixed message, and empty `data`.
- `ModuleNotEnabledException` returns 503 with title `Module Not Enabled`, a type-prefixed message, and empty `data`.
- `AggregateNotFoundException` and `EntityNotFoundException` return 404 with their existing titles, type-prefixed messages, and empty `data`.
- The catch-all returns 500 with title `Internal Server Error`. With the helper's diagnostic flag disabled, its detail is `An unexpected error occurred.` and `data` is empty. With that flag enabled, `detail` contains the exception type and message, and `data.errors` contains objects with `type`, `message`, and `stackTrace`.

These mappings use `https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/{status}`. They leave `instance` unset unless another component supplies it.

Hellang evaluates mappings in registration order. The helper registers its catch-all before the extra `mappings` delegates and the `NotImplementedException` and `HttpRequestException` status mappings. Consequently, those later mappings are shadowed. The latter two exceptions currently return 500, despite the apparent 501 and 503 registrations. Preserve effective behavior during dependency removal. Any correction needs a separate, explicit behavior decision.

The helper also configures `Rethrow<NotSupportedException>()`. Hellang writes the problem response before rethrowing the exception. The new setup uses the normal ASP.NET Core handling described below.

### Diagnostic flags are currently independent

The helper's `includeExceptionDetails` argument controls its catch-all `detail` and `data.errors`. Hellang's `IncludeExceptionDetails` callback separately controls the additional `exceptionDetails` extension.

The helper normally enables the latter in Development or Staging, or when its argument is true. A host can override that callback after calling the helper. The examined configuration overrides it to Development only, and passes `IsDevelopment()` to both the helper and `AddResultHttpProblemDetails`. Its Production and Staging responses therefore omit the additional diagnostics.

The legacy disclosure setting leaves the type-prefixed exception messages from explicit mappings in public `detail`. The shared format changes this behavior for built-in mappings.

Thrown exceptions and Result errors currently use different diagnostic shapes:

- Hellang adds `exceptionDetails` as objects with `message`, `type`, `raw`, and `stackFrames`. Stack-frame objects can include file, function, line, and source-context fields.
- Result errors add `exceptionDetails` as strings containing exception text. Their normal `detail` remains safe, including when diagnostic strings are enabled.
- An exception-bearing Result suppresses the ordinary `data.result` diagnostics. A mixed validation and exception failure retains validation messages in `data.errors` without serializing the exception through `data.result`.

### Minimal API Result contract

Preserve the existing default mapping precedence: forbidden, unauthorized, not found, validation, conflict, concurrency, domain policy, cancellation, timeout, exception, rule exception, rule, and fallback. Preserve custom error handlers and the behavior of explicitly selected typed response arms.

The normal Problem Details outcomes include:

- Forbidden returns 403, unauthorized 401, and not found 404.
- Validation returns 400 with title `Validation Error` and `data.errors`.
- Conflict and concurrency return 409. Domain policy returns 400. Cancellation returns 499. Timeout returns 504.
- Exception and rule failures return their existing 500 statuses and titles. An unclassified failure uses `Unexpected Error` and an instance identifier.

Non-exception failures can include `data.result`, containing `isSuccess`, `messages`, and typed error information. Validation also includes grouped `data.errors`. The shared format below defines where Result and exception responses converge.

The type URI uses the MDN pattern above. Do not replace it with the native framework's default RFC URI. Preserve existing `instance` values and omission rather than setting the request path globally.

### MVC Result differences to align

The current MVC mapper recognizes forbidden, unauthorized, not-found, and validation failures, then falls back to 500. It does not share all Minimal API status rules. For example, conflict, timeout, and cancellation currently become 500 in this path.

MVC validation uses title `Bad Request`, a flat `detail`, and top-level `IsSuccess`, `Messages`, and `Errors`. It does not produce the Minimal API `data.result` and `data.errors` structure. MVC exception-bearing Results use title `Unhandled Result Error`, and ordinary MVC Result problems lack the Minimal API writer's `traceId`.

Use the existing Minimal API classification for MVC Results. Both adapters then produce the shared format below.

Framework model binding and automatic `[ApiController]` validation are a separate origin. They currently use framework `ValidationProblemDetails`, top-level `errors`, and framework type URIs. Keep that established contract and integrate shared correlation and customization. Do not confuse automatic model validation with a thrown FluentValidation exception or a failed Result.

### Correlation, headers, and media types

The exception hook currently sets `correlationId` from `TryGetCorrelationId()`, falling back to `HttpContext.TraceIdentifier`. Result preparation uses request correlation, ambient `CorrelationId.Current`, the current Activity trace identifier, then the request trace identifier. Preserve these fallback semantics unless a host explicitly overrides them.

Hellang adds `traceId` using `Activity.Current?.Id ?? context.TraceIdentifier`, unless the extension already exists. Minimal API native writes also add `traceId`. Keep `traceId` and `correlationId` distinct.

On exception and empty-status responses, Hellang clears the response while preserving its header allowlist. The helper adds `CorrelationId`, `FlowId`, `TraceId`, and caller-provided header names. The library also retains CORS, HSTS, and authentication challenge headers and sets cache-prevention headers. Existing Result responses do not automatically acquire that cache policy.

Default JSON responses currently differ in transport details. Hellang and MVC writes use `application/problem+json; charset=utf-8`. Native Minimal API writes use `application/problem+json`. The host's HTTP JSON options and MVC JSON options are separate. Preserve host-configured serializers and converters. Do not require byte-identical indentation, property ordering, or charset spelling for MVC alignment.

With the examined configuration, Hellang and MVC use the available JSON formatter even for `Accept: text/html` or `application/xml`. Native `ProblemHttpResult` can fall back to JSON when no native writer accepts the request. That fallback currently bypasses native customization, so its `traceId` and opted-in Result diagnostics can be absent. The replacement defines the fallback for each response origin. [Writer fallback](#writer-fallback)

## Shared response format

Use standard Problem Details fields and the existing `traceId` and `correlationId` extensions. The format applies to default DevKit mappings for thrown exceptions and failed Results. Custom handlers and writers can supply their own responses. Framework-generated MVC model validation continues to use its native `errors` member.

### Field validation and Result data

Use `data.errors` exclusively for field validation, as a JSON object whose keys are field names and whose values are arrays of message strings. Both paths remove a leading `Entity.` prefix case-insensitively, preserve the casing of the remaining name, and group missing or blank field names under `validation`. Apply normalization before grouping so messages for `Entity.Name` and `Name` appear under the same key. Omit `data.errors` when it has no messages.

The default validation response uses HTTP 400, title `Validation Error`, and detail `One or more validation errors occurred.` in both paths. Keep the existing status precedence when a Result contains multiple error categories.

Include `data.result` for failed Results when the existing mapper exposes its public messages and typed errors. Continue omitting `data.result` for exception-bearing Results. Mixed failures can still expose public validation messages under `data.errors`. Omit `data` when it has no members.

For example, a thrown validation exception can produce the following response in either endpoint style.

```json
{
	"type": "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/400",
	"title": "Validation Error",
	"status": 400,
	"detail": "One or more validation errors occurred.",
	"data": {
		"errors": { "Name": ["Name is required."] }
	},
	"traceId": "request-trace",
	"correlationId": "inventory-request-42"
}
```

A failed validation Result can also include `data.result`.

```json
{
	"type": "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/400",
	"title": "Validation Error",
	"status": 400,
	"detail": "One or more validation errors occurred.",
	"correlationId": "inventory-request-42",
	"data": {
		"result": {
			"isSuccess": false,
			"messages": [],
			"errors": [
				{ "propertyName": "Name", "properties": {}, "message": "Name is required." }
			]
		},
		"errors": { "Name": ["Name is required."] }
	},
	"traceId": "request-trace"
}
```

### Public exception messages

Use fixed public `detail` text for the built-in thrown-exception mappings at either disclosure setting. The status, title, and type URI follow the mapped exception.

- Validation uses `One or more validation errors occurred.` and keeps the field messages in `data.errors`.
- Rule and domain policy failures use `The request could not be completed.`
- Security failures use `Access is not authorized.`
- Disabled modules use `The requested feature is unavailable.`
- Aggregate and entity not-found failures use `The requested resource was not found.`
- The catch-all uses `An unexpected error occurred.`

Use a mapping factory or a failed Result to supply intentional client-facing detail. `IncludeExceptionDetails` controls the diagnostic extension independently of the public text.

### Exception diagnostics

When `IncludeExceptionDetails` is enabled, use one top-level `exceptionDetails` array of objects for thrown exceptions and exception-bearing Results. Each object has a string `type` and `message`. For exceptions, `type` is the exception's fully qualified CLR type name. Include `stackTrace` as a string when available and `innerExceptions` as an array of the same objects when there are inner exceptions. Preserve the order of Result error entries and aggregate inner exceptions. For diagnostic-only Result errors without an exception object, use the error's fully qualified CLR type and its existing diagnostic text as `message`. Such entries omit `stackTrace` and `innerExceptions`.

Use the same formatter for both paths. The shared schema omits the old `raw` and `stackFrames` members. The catch-all exception mapping and the default exception-bearing Result detail use `An unexpected error occurred.` at either disclosure setting. Exception messages and stacks appear only in `exceptionDetails` for these failures. Validation and explicitly configured public details follow their mappings.

Omit `exceptionDetails` when disclosure is disabled, no diagnostic entry is available, or the response uses the JSON writer fallback described below. [Writer fallback](#writer-fallback)

For example, either endpoint style can return the following when a Result captures an exception and diagnostics are enabled. A thrown exception uses the same diagnostic object with the title from its exception mapping.

```json
{
	"type": "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/500",
	"title": "Exception Error",
	"status": 500,
	"detail": "An unexpected error occurred.",
	"correlationId": "inventory-request-42",
	"traceId": "request-trace",
	"exceptionDetails": [
		{
			"type": "System.InvalidOperationException",
			"message": "Inventory store is unavailable."
		}
	]
}
```

## Proposed DevKit integration

### Ownership and configuration

Own the feature in `Presentation.Web` as HTTP Problem Details. Replace Hellang for unhandled exceptions and eligible empty error responses, and integrate the existing preparation of Result problems and framework-generated problems. `ResultMapHttpExtensions` remains responsible for normal Result status and error mapping. Keep HTTP-independent Results in `Common.Results`. Keep Entity Framework exception classification in `Presentation.Web.EntityFramework` and opt-in.

Build on ASP.NET Core's standard implementation. Use its exception-handler middleware, `IExceptionHandler` registrations, `IProblemDetailsService`, `Microsoft.AspNetCore.Http.ProblemDetailsOptions.CustomizeProblemDetails`, status-code pages, and MVC factories and executors. Use the existing `Microsoft.AspNetCore.App` framework reference. Add no replacement NuGet dependency or separate compatibility package.

Use `AddResponseProblemDetails()` and `UseResponseProblemDetails()` for the default setup. The distinct registration name avoids introducing a second `AddProblemDetails` candidate into existing ASP.NET Core call sites.

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddResponseProblemDetails();

var app = builder.Build();
app.UseResponseProblemDetails();
```

`UseResponseProblemDetails()` calls `UseExceptionHandler()` and then `UseStatusCodePages()`. Hosts that need separate placement or only one middleware can use the native calls directly. DevKit registration supplies mappings, Problem Details preparation, and status-code options for either setup.

Provide `AddResponseProblemDetails()` for defaults and `AddResponseProblemDetails(Action<ProblemDetailsOptions> configure)` for application overrides. The callback receives `BridgingIT.DevKit.Presentation.Web.Errors.ProblemDetailsOptions`. Both overloads call Microsoft's service registration internally. Native `AddProblemDetails()` and `AddProblemDetails(options => ...)` use `Microsoft.AspNetCore.Http.ProblemDetailsOptions`.

Keep the DevKit type in the `BridgingIT.DevKit.Presentation.Web.Errors` namespace so importing `BridgingIT.DevKit.Presentation.Web` does not introduce a second `ProblemDetailsOptions` name. The registration callback infers its type. Use a qualified name or a type alias when referencing the DevKit type directly in a host that uses both namespaces.

The new registration combines setup that currently requires two registrations. Its single `IncludeExceptionDetails` option controls the shared diagnostic extension for thrown exceptions and exception-bearing Results.

Use the standard options pattern for configuration. Wire `OnBeforeWriteDetails` into response preparation and preserve native customization and custom `IProblemDetailsWriter` registrations. Plain ASP.NET Core registration and explicit DevKit handler and options registration remain an alternative to the convenience overload.

Preserve `AddResultHttpProblemDetails(bool)` and `ResultHttpProblemDetailsOptions` as supported Result-only entry points into the same feature. Calling the Result-only method must not activate global exception middleware, override an exception diagnostic policy, or require MVC services for Minimal API hosts.

Make native registration, Result-only registration, and the new combined registration compose without duplicate handlers, writers, hooks, or reset options. Specify deterministic option precedence and test both registration orders.

### Shared policy with endpoint adapters

Keep the existing classification and normal mapping implementation in `ResultMapHttpExtensions` largely unchanged. Do not move its rules into a new Problem Details classifier, replace its typed mapping APIs, or rewrite its success paths. The replacement feature consumes the problems those mappings already create.

Share validation formatting, diagnostic formatting, correlation, and applicable customization. MVC Result alignment reuses the existing Minimal API failure path through a narrow helper or adapter. An MVC `ActionResult` adapter can execute the existing mapped `IResult` against its `HttpContext`. Verify typed and custom-handler cases.

MVC keeps its public `IActionResultMapper` and action-result extension methods. Its failed Results use the Minimal API classification and the shared response format. Its successful responses, ordinary controller responses, model-state problems, and application-configured MVC formatters remain under MVC. Preserve custom Result handlers and explicitly selected non-problem responses.

Use the existing exception-handler mechanisms where useful, but do not adopt their current output unchanged. [`ValidationExceptionHandler`](../../src/Presentation.Web/Exceptions/ValidationExceptionHandler.cs) currently emits top-level `errors`; [`ExceptionHandlerBase`](../../src/Presentation.Web/Exceptions/ExceptionHandlerBase.cs) sets a request-path instance and a different type URI. Those defaults do not match the exception baseline.

The existing DevKit `AddExceptionHandler(...)` chain remains a standalone setup with its own options and defaults. When a host registers that full chain and `AddResponseProblemDetails(...)`, reject the combination at startup in either registration order. The error must name both registrations and tell developers to choose one. Individual native `AddExceptionHandler<THandler>()` registrations are supported.

Apply the shared format to default DevKit exception and Result problems. Retain correlation and trace semantics, and run applicable customization once. Preserve framework-generated `ValidationProblemDetails`, application extension values, and host-supplied writers.

### Middleware boundaries

Let `UseExceptionHandler()` own exception capture, dispatch through registered `IExceptionHandler` implementations, and framework fallback. Let `UseStatusCodePages()` own eligibility for empty error responses. DevKit supplies mappings and response preparation through those extension points. Do not reproduce either middleware's general request-processing loop.

Use the framework's `ProblemDetails` types and writers to serialize the shared format. Small adapters may retain headers across response clearing or handle a writer fallback. Use the standard middleware and extend it at the points that need different behavior.

Replace Hellang's exception and empty-status behavior together. An empty 400 through 599 response with no content type or content length is eligible under the current library policy. `Content-Length: 0` is different from an unset content length. Preserve explicit bodies, declared content types, successful responses, redirects, and statuses outside that range.

Default empty-status problems use `https://httpstatuses.io/{status}`, the reason phrase, and the existing trace and correlation fields. Do not replace these with the MDN exception mapping or the framework's RFC defaults.

Preserve existing middleware placement and host scope during migration. Mixed API and HTML hosts can explicitly scope DevKit's automatic handling to their API routes and keep their native UI error pages. Characterize mixed hosts, authentication, CORS, request correlation, and request profiling before changing their pipeline. [Mixed API and HTML hosts](#mixed-api-and-html-hosts)

Use ASP.NET Core's exception propagation and request-abort behavior. Before the response starts, `NotSupportedException` receives the normal 500 Problem Details response when no custom rule handles it. The exception is handled when that response is written. The migration removes Hellang's special post-write rethrow. For an exception raised after the response starts, ASP.NET Core handles propagation according to its normal streaming and abort rules.

Verify `HEAD`, disconnected clients, partial streaming writes, and failures in a custom mapper or write hook. Document differences from the Hellang baseline that affect migrating applications.

Preserve server-side failure visibility and request-profiling observations. Account for native .NET 10 diagnostics suppression when a handler returns `true`. Do not duplicate error logging or report one exception multiple times through adapters.

## Configuration reference

This section defines the proposed DevKit `ProblemDetailsOptions` contract.

### Registration and activation

`AddResponseProblemDetails` registers the compatibility mappings, request-time response preparation, and supporting standard ASP.NET Core services. The registration works for Minimal APIs without adding controllers. MVC hosts additionally call `AddControllers()` and `MapControllers()`.

Service registration does not install middleware. Hosts activate unhandled exception handling with `UseExceptionHandler()` and empty-error response handling with `UseStatusCodePages()`. Either middleware can be omitted independently. Omitting status-code pages does not disable exception handling or explicit Result problem responses.

`UseResponseProblemDetails()` is the quickstart setup. Choose either the wrapper or the explicit middleware calls.

`AddResultHttpProblemDetails(bool)` remains sufficient for Result-only hosts. It registers the shared preparation and native Problem Details service without installing exception handling, status-code pages, or MVC.

### Mixed API and HTML hosts

Use the two-call quickstart when automatic Problem Details should apply across the host. If native exception middleware rewrites the request to an error page, DevKit leaves that exception to the page by default. A host can therefore retain an outer native error page and use `UseResponseProblemDetails()` at its existing error boundary with default options.

Use `ShouldHandleRequest` to override exception handling and scope default status-code pages when a host uses one native exception middleware for both APIs and HTML pages. When the predicate returns `false`, the DevKit exception handler declines before running any mappings or DevKit exception logs, and DevKit's status-code handler leaves the response to the host. Other registered exception handlers and host-supplied status pages still follow the native pipeline. Explicit Result mapping and MVC automatic model validation remain available at any route.

Use a request property that identifies the API even when routing finds no endpoint. The example uses the original request path so that `/api` exceptions still match when ASP.NET Core dispatches to `/Error`. A host that checks endpoint metadata can read the original endpoint from `IExceptionHandlerFeature.Endpoint`, and needs a rule for unmatched routes. Use `Accept` for representation selection on requests in scope.

```csharp
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddResponseProblemDetails(options =>
	options.ShouldHandleRequest = IsApiRequest);

var app = builder.Build();
app.UseExceptionHandler("/Error");
app.UseWhen(IsApiRequest, api => api.UseStatusCodePages());
app.UseWhen(context => !IsApiRequest(context), ui =>
	ui.UseStatusCodePages("text/html; charset=utf-8", "<h1>HTTP {0}</h1>"));
app.UseRouting();

static bool IsApiRequest(HttpContext context)
{
	var originalPath = context.Features.Get<IExceptionHandlerPathFeature>()?.Path;
	var path = originalPath is null ? context.Request.Path : new PathString(originalPath);
	return path.StartsWithSegments("/api");
}
```

In this example, the application provides an HTML `/Error` endpoint for the original HTTP methods it serves. Keep the exception middleware before the routes it protects. If the UI already uses native status-page re-execution, configure it in the UI branch and keep the existing page route. A host that wants UI empty statuses to stay empty can call `UseStatusCodePages()` once with the predicate configured. With a parameterless `UseExceptionHandler()` and no HTML fallback, ASP.NET Core can still use its own Problem Details service and native defaults for an excluded exception.

### Options and defaults

The new options expose these settings:

- `IncludeExceptionDetails` is a `bool` with a default of `false`. It controls whether the shared `exceptionDetails` extension appears for thrown exceptions and exception-bearing Results when diagnostic preparation applies. Public detail and validation messages are independent of this flag.
- `ShouldHandleRequest` is an optional `Func<HttpContext, bool>`, default `null`. The default handles eligible requests and leaves native exception-page rewrites to the host. An explicit predicate controls automatic exception and empty-status handling. [Mixed API and HTML hosts](#mixed-api-and-html-hosts)
- `OnBeforeWriteDetails` is an optional `Action<HttpContext, ProblemDetails>`, default `null`. It enriches a problem immediately before serialization, after applicable default preparation and native customization. It applies during normal writer or MVC formatter preparation for exceptions, failed Results, and eligible framework-generated problems managed by the feature. [Writer fallback](#writer-fallback)
- `AllowedHeaderNames` is a case-insensitive `ISet<string>` initialized with the compatibility header allowlist. Adding a name retains an existing response header across exception-response clearing. It does not copy a request header into the response.
- `ShouldLogUnhandledException` is a `bool` with a default of `true`. When enabled, DevKit logs handled exceptions with a selected 5xx status, including those handled by individually registered `ExceptionHandlerBase<TException>` implementations in the combined setup. Set it to `false` to disable the DevKit exception log. ASP.NET Core diagnostics and standalone handlers have separate settings.
- `ShouldLogResultErrors` is a `bool` with a default of `true`. It logs mapped 5xx Problem Details and custom MVC Result responses at execution. An explicit logger passed to the Result mapper takes precedence.

Set `IncludeExceptionDetails` explicitly to select an environment policy. Hosts that previously configured the legacy diagnostic switches differently choose a common value when migrating. Standalone `GlobalExceptionHandlerOptions` retains its disclosure behavior. Result-only hosts keep their existing disclosure option and use the shared diagnostic format.

The following reproduces the examined host's Development-only disclosure policy.

```csharp
builder.Services.AddResponseProblemDetails(options =>
{
	options.IncludeExceptionDetails = builder.Environment.IsDevelopment();
	options.OnBeforeWriteDetails = (context, problem) =>
		problem.Extensions["correlationId"] =
			context.TryGetCorrelationId() ?? context.TraceIdentifier;
});
```

Bind simple values using the standard options API. Configure predicates, factories, and handlers in code.

```csharp
builder.Services.Configure<BridgingIT.DevKit.Presentation.Web.Errors.ProblemDetailsOptions>(
	builder.Configuration.GetSection("ProblemDetails"));
```

### Extend exception mappings

Applications and modules can contribute mappings through `AddResponseProblemDetails` or the standard options configuration API. Use `Map<TException>(...)` for a host override. Use `MapDefault<TException>(...)` in reusable modules. Both use the same typed status and factory signatures.

```csharp
ProblemDetailsOptions Map<TException>(
	int statusCode, string title = null, string typeUri = null)
	where TException : Exception;

ProblemDetailsOptions Map<TException>(
	Func<TException, ProblemDetails> factory)
	where TException : Exception;

ProblemDetailsOptions Map<TException>(
	Func<TException, HttpContext, ProblemDetails> factory,
	Func<TException, HttpContext, bool> when = null)
	where TException : Exception;
```

These proposed methods configure thrown exceptions. Reuse the existing `ExceptionMapping` representation where suitable.

An application can override status, title, and type, or supply a complete problem factory. Use the one-argument factory for existing mappings that only need the exception. Use the context-aware factory and optional predicate when a mapping depends on the request.

```csharp
builder.Services.AddResponseProblemDetails(options =>
{
	options.Map<HttpRequestException>(
		StatusCodes.Status502BadGateway, "Inventory Provider Failed");

	options.Map<KeyNotFoundException>(
		StatusCodes.Status404NotFound,
		"Inventory Item Not Found",
		"https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/404");

	options.Map<ArgumentException>(
		(exception, context) => new ProblemDetails
		{
			Status = StatusCodes.Status400BadRequest,
			Title = "Invalid Inventory Request",
			Type = "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/400",
			Extensions = { ["data"] = new { code = "invalid_inventory_request" } }
		},
		when: (exception, context) => context.Request.Path.StartsWithSegments("/inventory"));
});

builder.Services.Configure<BridgingIT.DevKit.Presentation.Web.Errors.ProblemDetailsOptions>(options =>
{
	options.MapDefault<HttpRequestException>(
		StatusCodes.Status503ServiceUnavailable, "Inventory Provider Unavailable");
});
```

Evaluate host overrides first, module defaults second, built-in mappings third, and the catch-all last. Within each group, use declaration order and take the first matching mapping. A base exception mapping also matches derived exceptions. Register specific exceptions before broad ones in the same group. A predicate that returns `false` lets evaluation continue.

In the example, the host's 502 wins even if the module registers first. If the host removes its override, the module supplies 503. With neither mapping, the built-in catch-all returns 500.

In a mixed host using an exception-page path, read the original path or endpoint from the native exception feature in path-dependent mapping predicates and factories. [Mixed API and HTML hosts](#mixed-api-and-html-hosts)

For the status overload, an omitted title uses the reason phrase and an omitted type uses the established MDN URI pattern. The selected status must be between 400 and 599. The shared diagnostics policy controls exception disclosure. Use a factory to set public `detail` or `instance` explicitly.

A custom factory owns the problem's explicit values and extension data. The pipeline adds applicable diagnostics and shared preparation, while retaining supplied members except for intentional overrides in customization callbacks. The factory must return a non-null error problem with a valid status. Invalid static mapping configuration fails at startup. A failing or invalid runtime factory must not produce a successfully handled empty response.

Migrate each existing mapping to the corresponding typed status or factory registration. Identify any legacy mapping that was shadowed by the catch-all before enabling it in the new configuration.

### Use one problem type for an exception and a Result error

Assign an application-owned `type` URI and the same status to equivalent failures. Register the exception with `Map<TException>()` and the failed Result with the existing Result error handler registration at startup.

```csharp
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

const string conflictType = "https://inventory.example/problems/item-conflict";
const string conflictTitle = "Inventory item conflict";

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddResponseProblemDetails(options =>
	options.Map<InventoryConflictException>(
		StatusCodes.Status409Conflict, conflictTitle, conflictType));

ResultMapHttpExtensions.RegisterErrorHandler<InventoryConflictError>((_, _) =>
	TypedResults.Problem(
		statusCode: StatusCodes.Status409Conflict,
		title: conflictTitle,
		type: conflictType));

var app = builder.Build();
app.UseResponseProblemDetails();

app.MapPost("/api/items/reserve", HttpResult () => throw new InventoryConflictException())
	.ProducesResultProblem(StatusCodes.Status409Conflict);
app.MapPost("/api/items/release", () =>
		Result.Failure(new InventoryConflictError()).MapHttpNoContent())
	.ProducesResultProblem(StatusCodes.Status409Conflict);

app.Run();

sealed class InventoryConflictException : Exception { }
sealed class InventoryConflictError() : ResultErrorBase("The inventory item changed.") { }
```

Both endpoints return 409 with the same `type` and `title`. Clients can branch on that URI and present their own localized message. The same mappings apply to an MVC action that throws the exception or passes the failed Result through the DevKit HTTP mapper.

### Custom handlers and framework ordering

Use ASP.NET Core's `AddExceptionHandler<THandler>()` for application handlers. Register them before the DevKit convenience registration so they execute before its mappings and catch-all. For example, an application-defined `InventoryExceptionObserver` can observe an exception and return `false` to continue:

```csharp
builder.Services.AddExceptionHandler<InventoryExceptionObserver>();
builder.Services.AddResponseProblemDetails(options =>
{
	options.IncludeExceptionDetails = builder.Environment.IsDevelopment();
});
```

`InventoryExceptionObserver` is an application-supplied `IExceptionHandler`. Native registration order determines which handler runs first. A handler returns `true` after writing a response, or `false` to continue the chain.

To use shared correlation, diagnostic disclosure, and `OnBeforeWriteDetails`, write through the registered `IProblemDetailsService`. Set the response status and supply the exception in `ProblemDetailsContext.Exception`. A handler that writes directly owns its body and diagnostics.

Native `AddExceptionHandler<THandler>()` registration uses singleton handlers. Resolve request-scoped dependencies from the supplied `HttpContext.RequestServices` when needed. Do not retain a request context or scoped service in a singleton handler.

Existing `GlobalExceptionHandlerOptions.AddHandler(...)` priority configuration remains available with the standalone DevKit chain. With the combined setup, native registration order controls handlers and the mapping rules above control DevKit mappings.

Database handlers remain explicit. Preserve existing `AddEntityFrameworkHandlers()` configuration. For combined setup, the existing handlers can be registered through native DI before the DevKit catch-all, with their existing `GlobalExceptionHandlerOptions` dependency supplied explicitly:

```csharp
builder.Services.AddSingleton(new GlobalExceptionHandlerOptions
{
	EnableLogging = true
});
builder.Services.AddExceptionHandler<DbUpdateConcurrencyExceptionHandler>();
builder.Services.AddExceptionHandler<DbUpdateExceptionHandler>();
builder.Services.AddExceptionHandler<DbExceptionHandler>();
builder.Services.AddResponseProblemDetails(options =>
{
	options.IncludeExceptionDetails = builder.Environment.IsDevelopment();
});
```

The EF handler types come from `Presentation.Web.EntityFramework`. Concurrency must precede the broader update handler. This setup adds the selected handlers, not a second complete global chain. Their activation must not be a side effect of referencing the EF package or enabling Problem Details. Their response writing must participate in the shared preparation as required by the implementation.

### Native customization and writers

Use Microsoft's `ProblemDetailsOptions.CustomizeProblemDetails` to enrich framework-generated problems. Qualify the type to select Microsoft's options.

```csharp
builder.Services.Configure<Microsoft.AspNetCore.Http.ProblemDetailsOptions>(options =>
{
	options.CustomizeProblemDetails = context =>
		context.ProblemDetails.Extensions["service"] = "inventory-api";
});
```

Preserve the existing order of host native customization and Result preparation when those paths compose. `OnBeforeWriteDetails` is the final DevKit enrichment hook before serialization. Each applicable callback runs once per serialized problem, including when preparation creates a copy. Hook exceptions follow the framework's failure path and must not trigger a second response write.

A custom `IProblemDetailsWriter` participates in native writer selection. Register it in the order ASP.NET Core expects. The shared service prepares the problem before the selected writer serializes it, so enrichment does not depend on the writer invoking the native customization callback itself. Each applicable hook runs once.

Choose the status in the exception mapping, Result mapping, or handler before writing. An eligible empty response keeps its existing status. On DevKit-managed writes, `CustomizeProblemDetails` and `OnBeforeWriteDetails` can change `type`, `title`, `detail`, `instance`, and extensions. After callbacks finish, restore the selected value to both the HTTP status and the problem's `status` before serialization. Use that same value for DevKit logging. A custom writer controls the representation and honors the selected status. Handlers that write their own response own the status as well.

### Writer fallback

The registered writers get the first opportunity to write a Problem Details response based on the request and available metadata. If none accepts an in-scope DevKit exception or empty-status response, write the default problem as `application/problem+json` with the selected status, public fields, validation messages where applicable, and the usual `correlationId` and `traceId`. Use the host's HTTP JSON settings. Skip the native and DevKit customization hooks for this fallback and omit the optional diagnostic extension. This preserves a usable JSON response for API requests that accept only HTML or XML.

For mapped Result problems, preserve the native `ProblemHttpResult` JSON fallback across both endpoint styles. It serializes the mapper's problem, including any correlation ID already attached. It skips the customization hooks and adds neither writer-generated `traceId` nor opted-in diagnostics. The fallback applies when selection declines before writing. A selected writer or hook that fails follows the framework's failure path without a second write.

### Status-code handling, headers, and logging

`UseStatusCodePages()` enables processing of eligible empty error responses. `Configure<StatusCodePagesOptions>(...)` supplies application-specific status-page behavior. Applications can disable it for an individual request through `IStatusCodePagesFeature.Enabled`. Such overrides use the native mechanism and take precedence over DevKit's default empty-status preparation.

The initial response header allowlist contains `CorrelationId`, `FlowId`, `TraceId`, `WWW-Authenticate`, `Strict-Transport-Security`, and the existing `Access-Control-Allow-Credentials`, `Access-Control-Allow-Headers`, `Access-Control-Allow-Methods`, `Access-Control-Allow-Origin`, `Access-Control-Expose-Headers`, and `Access-Control-Max-Age` names. Hosts can extend it:

```csharp
builder.Services.AddResponseProblemDetails(options =>
{
	options.AllowedHeaderNames.Add("X-Inventory-Version");
});
```

Cache-prevention headers retain the baseline behavior for exception and empty-status responses. This does not introduce a global cache policy for explicit Result or successful responses. Host-specific cache changes use the standard response pipeline.

By default, DevKit logs handled server errors through `ILogger`. ASP.NET Core 10 suppresses its own diagnostics when an `IExceptionHandler` handles the exception. To use framework diagnostics for server errors, disable the DevKit exception log and allow framework diagnostics for responses with status 500 or higher.

```csharp
builder.Services.AddResponseProblemDetails(options =>
{
	options.ShouldLogUnhandledException = false;
});

builder.Services.Configure<Microsoft.AspNetCore.Builder.ExceptionHandlerOptions>(options =>
{
	options.SuppressDiagnosticsCallback = context =>
		context.HttpContext.Response.StatusCode < StatusCodes.Status500InternalServerError;
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
```

Returning `true` suppresses framework diagnostics. Enabling framework diagnostics while leaving DevKit exception logging enabled can log the same exception twice.

`AddResponseProblemDetails()` enables HTTP Result logging by default. DevKit records one structured `Error` entry when a mapped 5xx Problem Details response executes, including when JSON fallback runs. The entry includes the HTTP status, Result error types, and the response's correlation ID when available. A custom MVC result that writes its own body uses its executed status and request correlation for logging. This setting is independent of `IncludeExceptionDetails`. Expected 4xx Result failures can use the existing mapper logger or request logging.

For a Minimal API endpoint that declares the native `InternalServerError` arm, pass an explicit logger to the mapper to log the failure. The framework executes that arm directly. Declare `ProblemHttpResult` to use shared HTTP Result logging.

Set `ShouldLogResultErrors` to `false` to disable HTTP Result logging.

```csharp
builder.Services.AddResponseProblemDetails(options =>
{
	options.ShouldLogResultErrors = false;
});
```

Hosts that pass an explicit logger to the Result mapper keep that logger for the mapped failure. The Result-only registration and `UseResultLogger()` have separate logging behavior.

Applications with custom propagation requirements can use a standard `IExceptionHandler` and native fallback. A handler that declines an exception returns `false` so the next handler or framework fallback can handle it. The combined setup uses standard propagation after the response starts.

### Option precedence and validation

Apply defaults once. Evaluate `Configure` delegates in registration order, then `PostConfigure` delegates according to the normal options pattern. Later explicit scalar assignments win. Mappings append in declaration order within their precedence group. Header additions accumulate case-insensitively. Assigning a callback replaces that callback. To compose two callbacks, call both from a wrapper.

With `AddResponseProblemDetails`, both response paths read the final DevKit `ProblemDetailsOptions.IncludeExceptionDetails` value. If the host also calls `AddResultHttpProblemDetails(bool)`, the combined policy takes precedence in either registration order. In a Result-only host, `AddResultHttpProblemDetails(bool)` still controls Result disclosure. A host migrating to the combined registration can remove the Result-only call.

Register infrastructure once even when configuration is supplied in several calls. Do not repeat user configuration delegates to discover services, append duplicate built-in mappings, or wrap an existing callback multiple times. Configure and validate options before serving requests; runtime mutation of mapping lists and captured options is outside the supported contract.

Reject a null configuration delegate, invalid static statuses, and invalid mapping registrations with clear configuration errors. Use `AddResponseProblemDetails()` to select the DevKit defaults.


## OpenAPI response contract

Keep `ProducesResultProblem(statusCode)` as the endpoint metadata API for the shared DevKit response. Use it to declare the statuses an endpoint promises for failed Results or thrown exceptions that follow the shared format. [Result problem endpoint metadata](../../src/Presentation.Web/Result/ResultProblemExtensions.cs)

Use the existing ASP.NET Core OpenAPI registration and `ResultProblemDetailsSchemaTransformer`. The current OpenAPI model describes `data.errors` as an array. Correct the emitted schema so a declared status uses `application/problem+json` and describes the actual JSON. [OpenAPI schema model](../../src/Presentation.Web/Result/OpenApi/ResultProblemDetails.cs) · [OpenAPI schema transformer](../../src/Presentation.Web/Result/OpenApi/ResultProblemDetailsTransformer.cs)

- `data` is optional. When present, `errors` is an optional object whose additional properties are arrays of strings. `result` is optional and describes the public Result fields and typed error properties where the mapper exposes them.
- `exceptionDetails` is an optional array of objects. Entries require string `type` and `message` fields. `stackTrace` and recursive `innerExceptions` are optional. Disabling disclosure at runtime omits the array.
- `correlationId` and `traceId` are optional string extensions. Accept other extension properties that hosts or custom handlers add.

Framework-generated MVC model validation keeps its native schema with top-level `errors`. When an action documents both that response and a shared DevKit response for the same status and media type, document both shapes as alternatives in the response schema using the host's OpenAPI configuration. Application-owned representations can use their own endpoint metadata.

Register the transformer with `AddOpenApi(options => options.AddSchemaTransformer<ResultProblemDetailsSchemaTransformer>())` and expose the document with `MapOpenApi()` when desired. For example, an inventory creation endpoint can declare a validation failure and a server error.

```csharp
app.MapPost("/api/items", CreateItem)
	.ProducesResultProblem(StatusCodes.Status400BadRequest)
	.ProducesResultProblem(StatusCodes.Status500InternalServerError);
```

## Scope and delivery

The implementation covers Presentation Problem Details, narrow integration with the existing Result HTTP adapters, host registration, dependency and lock files, focused tests, and migration documentation. Normal Minimal API Result mapping remains in its existing implementation. Domain rules, Result values, success payloads, database schemas, authentication policy, and unrelated dependencies remain unchanged.

Remove the direct Hellang reference, its central version, obsolete helper overloads exposing Hellang types, host imports, and middleware calls after characterizing the replacement. [Presentation.Web.csproj](../../src/Presentation.Web/Presentation.Web.csproj)

Removing the Hellang-specific public helper is a source and binary API change. Document how to migrate mapping delegates, consolidate diagnostic settings, retain allowed response headers, and migrate write hooks. The generic `Hellang*` assembly-scan exclusion can remain for consumer applications.

Both hosts migrated by this specification set `IncludeExceptionDetails = builder.Environment.IsDevelopment()` for thrown exceptions and exception-bearing Results. Verify the policy in Development, Staging, and Production.

Release notes and migration guidance must identify the client-facing changes.

- Read `exceptionDetails` as an array of objects. Replace reads of Result diagnostic strings, Hellang's `raw` and `stackFrames` fields, and any catch-all diagnostics formerly read from `detail` or `data.errors`.
- Read `data.errors` as a field-to-messages object. Account for normalized field names, the shared validation detail, and omission of empty `data`.
- Built-in exception mappings use fixed public messages. Use validation fields, status codes, or a custom mapping for error information clients need to act on.
- `NotSupportedException` now follows the normal exception pipeline. Update code that relied on Hellang rethrowing after it wrote a response.
- For MVC Result responses, switch from the old flat Result fields to the shared payload and account for the Minimal API mapper's status rules.
- Review mixed API and HTML middleware setup when migrating a host that already has HTML error pages. Use the request scope to preserve the existing division between API and UI responses.
- Regenerate clients that consume the DevKit Problem Details schema. It describes grouped validation, structured diagnostics, and optional extensions.

### Feature documentation

Update the Exception Handling guide as part of implementing this specification. Make it the main reference for configuring the new setup and show the implemented behavior in a fictional application. [Exception Handling guide](../features-presentation-exception-handling.md)

- Lead with a working `AddResponseProblemDetails()` and `UseResponseProblemDetails()` example. Show how to use `UseExceptionHandler()` and `UseStatusCodePages()` directly when separate middleware placement is required.
- Show a mixed API and HTML host with scoped Problem Details, a native exception page, and a native HTML status page. Explain what happens when no writer accepts an API request.
- Explain the supported combinations of registrations and the startup error for two full DevKit chains. Show how to register a native `AddExceptionHandler<THandler>()` before the combined setup and write through `IProblemDetailsService`.
- Show thrown exceptions and failed Results in Minimal APIs and MVC. Document the shared response fields, default mappings, custom mappings, and how to configure correlation, diagnostic disclosure, and exception and Result logging.
- Include the recipe for one application-owned `type` URI shared by a thrown exception and a failed Result. Show how `ProducesResultProblem` and the existing transformer document the shared format in OpenAPI.
- Include the Hellang migration steps and link to the recorded breaking changes. Verify the snippets and payload examples against the implementation. [ADR-0046](../adr/0046-shared-http-problem-details.md)

Update the Results guide's HTTP diagnostics example and link it to the Exception Handling guide for host setup. [Results guide](../features-results.md#problem-details-and-exception-diagnostics)

## Acceptance stories

The implementation verifies mixed-host routing and the generated OpenAPI contract in the shared server test host.

### 1. Align exception and Result payloads

As an API consumer, I want to read validation messages and exception diagnostics the same way across endpoints.

1. Given each effective exception mapping, when a Minimal API or MVC action throws, then the status remains the established default and the body follows the shared format.
2. Given equivalent validation failures across both paths, when the response is serialized, then status, title, public detail, and grouped `data.errors` match. A public Result payload may also include `data.result`.
3. Given the same exception thrown or captured in a Result, when diagnostics are enabled and prepared, then both serialize the same diagnostic fields and inner exception structure.
4. Given a diagnostic-only Result error without an exception object, when its diagnostics are emitted, then the entry uses the shared object shape.
5. Given an uncaught `NotSupportedException` before the response starts, when the default handler writes its 500 response, then the pipeline completes without a post-write rethrow. For failures after the response starts, verify ASP.NET Core's normal propagation.
6. Given a built-in exception mapping, when disclosure is enabled or disabled, then public detail stays fixed and the raw message appears only in diagnostics when enabled. Field validation messages remain available.

### 2. Align MVC Results with Minimal API Results

As a host developer, I want failed Results to use the leading Minimal API contract in both endpoint styles so that callers can interpret them consistently.

1. Given the same Result failure, when default Minimal API and MVC Result adapters execute, then both produce the same mapped status and shared payload, including correlation and applicable diagnostics.
2. Given validation, conflict, concurrency, cancellation, timeout, or multiple errors, when either adapter maps the Result, then classification follows the existing Minimal API precedence.
3. Given exception-bearing or mixed failures, when diagnostics are disabled, then validation messages remain available and exception text stays out of `data.result` and normal detail.
4. Given a custom mapper or an explicitly selected response arm, when mapping executes, then its supported customization remains effective and successful responses retain their current behavior.

### 3. Integrate host responses and configuration

As a host developer, I want one DevKit-owned Problem Details configuration so that exception and Result responses share policy without conflicting registrations.

1. Given `AddResponseProblemDetails` with standard exception and status-code middleware, its `UseResponseProblemDetails()` convenience, or existing Result-only registration, when a host starts, then the required services resolve once. Existing native `AddProblemDetails()` and callback calls, including an untyped empty callback, compile with the DevKit extensions in scope.
2. Given native options and DevKit customization in either registration order, when a problem is written, then each applicable hook runs once and preserves extension data.
3. Given eligible empty status responses, explicit problems, MVC model-state problems, or non-problem bodies, when the pipeline completes, then it preserves the origin-specific contract and required headers.
4. Given unsupported `Accept`, `HEAD`, cancellation, or a partially written stream, when handling runs, then it follows the documented writer and framework behavior without a second response.
5. Given both full DevKit registrations in either order, when the host starts, then startup fails with a message naming both. An individual native exception handler works with the combined setup.
6. Given a custom handler using `IProblemDetailsService`, when its response is written, then the selected writer receives shared enrichment once. A handler that writes directly retains control of its response.
7. Given a host with API and HTML routes and a native error page, when an API or UI request throws or reaches an unmatched route, then the original request selects the intended handler. API failures return Problem Details and UI failures use the configured HTML pages, even when the client sends the other representation in `Accept`.
8. Given that no writer accepts an in-scope exception, empty status, or failed Result response, when fallback runs, then it returns JSON with the selected status, public fields, and the documented identifiers. The optional diagnostic extension and writer customization hooks are absent.

### 4. Configure policies through supported extension points

As a host developer, I want explicit defaults and override rules so that I can migrate existing error-handling configuration without hidden behavior changes.

1. Given combined, Result-only, and native configuration in either order, when options resolve, then defaults, explicit assignments, and post-configuration follow the documented precedence without duplicate callbacks or handlers.
2. Given a host override and module default for the same exception, when either registration order is used, then the host override wins. A false predicate lets the next matching mapping run.
3. Given either value of `IncludeExceptionDetails`, when diagnostic preparation runs, then both responses include or omit the shared `exceptionDetails` array as configured. Catch-all exception detail stays generic and `data.errors` contains validation messages only. A legacy Result-only registration cannot override the combined value.
4. Given header extensions, native writer or status-page overrides, and logging configuration, when handling runs, then the selected framework extension points remain effective.
5. Given invalid configuration or a failing callback, when startup or response execution occurs, then the failure is explicit and cannot become an empty successful response or a second write.
6. Given the default combined registration, when a DevKit-mapped Result produces a 5xx problem or a custom MVC 5xx response, then either the explicit mapper logger handles the failure or the HTTP layer logs it once. Successful Results, expected 4xx Results, and responses with HTTP Result logging disabled produce zero entries from this policy. An explicit Minimal API `InternalServerError` arm uses the mapper logger when supplied.
7. Given an enrichment callback assigns a different status, when a DevKit-managed problem is written, then the HTTP status, JSON `status`, and DevKit log use the selected status. Other callback edits appear in the response.

### 5. Publish the shared response contract in OpenAPI

As an API consumer, I want generated clients to use the same field types and optional members that the API returns.

1. Given endpoints that declare validation and server error responses with `ProducesResultProblem`, when the host generates OpenAPI, then the declared statuses and media types agree with the actual responses. The schema describes grouped validation, public Result data, and optional nested exception diagnostics.
2. Given that diagnostics are enabled or disabled, when the same server failure is requested, then bodies with and without `exceptionDetails` both conform to the schema.
3. Given an MVC action with native automatic model validation, when its response and OpenAPI metadata are inspected, then model validation still uses the native top-level `errors` schema.

## Characterization and implementation gates

Before replacing the dependency, capture the combined existing registration and exercise it with real HTTP response serialization. Unit assertions on an unexecuted `ProblemDetails` object are insufficient.

### Shared Presentation test host

Set up the existing server test host like a DevKit web application so this and future API tests exercise the same startup conventions as consuming applications. Use `DevKitWebApplication.CreateBuilder(args)` with DevKit configuration and logging, `ConfigureJson()`, and request correlation in the HTTP pipeline. Keep common setup in the host entry point. Group each scenario's service registration and endpoints together under its own route prefix. [Presentation.TestHost](../../tests/Presentation.TestHost/Program.cs) · [Presentation Host guide](../features-presentation.md)

Add Problem Details as the first opt-in API scenario. Use `AddResponseProblemDetails()` and `UseResponseProblemDetails()` for the default setup, with correlation established before error handling. Give tests a way to enable or disable diagnostic disclosure at startup. Preserve the existing AppState and Blazor routes when the API scenario is disabled.

Exercise the host over HTTP. Include a thrown validation exception and an equivalent failed validation Result, an unexpected exception, a deliberately mapped exception, an empty error status, and an explicit response body. Assert the status, media type, correlation ID, and relevant JSON fields. Run the diagnostic cases with disclosure on and off.

Add a mixed-host startup profile to the same scenario. Keep the native HTML exception and status pages active for UI routes. Exercise API and UI exceptions, unmatched API and UI routes, already-written bodies, and an explicitly mapped Result outside the API scope. Include an API exception whose handler sees a rewritten error path and a UI exception reached by a non-GET request. Verify that changing `Accept` leaves the route scope intact and that custom HTML status-page re-execution stays within the UI branch.

Generate OpenAPI from the test host with the same transformer an application registers. Resolve response schema references and compare representative 400 validation and 500 server response bodies against the declared media types, property types, and optional fields. Include thrown failures and failed Results, test disclosure on and off, and cover a writer fallback. Keep a focused MVC check for the native model-state schema.

Document how to run the API scenario locally. Keep the shared host runnable with fictional inputs and in-memory behavior. Add feature services only to scenarios that need them, so future API tests can reuse the startup pipeline without a database or external service. Keep focused MVC tests for the MVC adapter and native model-state responses.

### Regression coverage

The regression set must cover:

- Exceptions and failed Results through Minimal APIs and MVC, including generic, non-generic, paged, and explicit typed mapping entry points.
- Production, Staging, and Development. Capture independent legacy diagnostic settings before migration, then verify the combined flag and the Result-only flag against the shared format.
- Every effective exception mapping and its public detail, shadowed mappings, precedence between host and module mappings, null or throwing factories, inner exceptions, and migration from the legacy post-write rethrow policy.
- Every Minimal API Result error category, message-only failures, mixed failures, custom handlers, serialized error properties, and HTTP-independent Result values.
- Explicit problems, MVC automatic model validation, empty statuses, `Content-Length: 0`, declared content types, explicit bodies, mixed API and UI routes, and error-page re-execution.
- Missing correlation middleware, request and ambient correlation, real Activity identifiers, response header retention, cache headers, and callback counts.
- Missing `Accept`, JSON, problem JSON, HTML, XML, wildcards, configured MVC formatters, and native writer fallback.
- `HEAD`, cancellation, client disconnects, already-started responses, streaming, logging, and request profiling.
- Explicit standard middleware setup and the convenience method, native options and writers, and ordinary ASP.NET Core hosts using either endpoint style. Verify that no parallel DevKit exception-dispatch loop is required.
- Documented option defaults, configuration binding, mapping and handler precedence, shared disclosure, invalid options, duplicate registration, final-hook execution, and status preservation through customization and logging.

Use normalized response snapshots for statuses, headers, member presence, JSON types, and values. Normalize dynamic identifiers, generated instances, stack text, and source locations. Compare the target payloads against the shared response format and the compatibility behavior against the baseline. Record any difference beyond the documented migration changes.

Keep the Minimal API Result mapper in place, integrate its response preparation, add the narrow MVC failure adapter, complete exception and status handling, migrate hosts, and then remove the package. Restore, build, and run relevant tests sequentially. Confirm no Hellang package remains in restored dependency graphs or lock files. Complete the feature guide updates and release notes against the verified implementation.

## Sources

- The [legacy HTTP fixture](../../tests/Presentation.UnitTests/Web/ProblemDetails/Fixtures/legacy-http-responses.json) records the baseline commit and observed wire behavior. [`ResultMapHttpExtensions.cs`](../../src/Presentation.Web/Result/Map/ResultMapHttpExtensions.cs) and [`DefaultActionResultMapper.cs`](../../src/Presentation.Web/Result/DefaultActionResultMapper.cs) define the current adapters.
- [Hellang middleware at the commit shipped in 6.5.1](https://github.com/khellang/Middleware/tree/2c41d08ca65c2ac62b47e5420f9be083033f430e/src/ProblemDetails) defines mapping order, response clearing, diagnostics, status handling, formatter execution, and post-write rethrow. Use this pinned version rather than assuming the default branch matches the deployed package.
- [ASP.NET Core `ProblemHttpResult`](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Http/Http.Results/src/ProblemHttpResult.cs) defines native service execution and JSON fallback.
- [ASP.NET Core MVC `DefaultProblemDetailsFactory`](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Mvc/Mvc.Core/src/Infrastructure/DefaultProblemDetailsFactory.cs) defines model-state defaults, trace identifiers, and native customization for framework-generated MVC problems.
- [ASP.NET Core error handling](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0) describes the standard registration and middleware extension points that this design uses.
- [ASP.NET Core exception middleware](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Middleware/Diagnostics/src/ExceptionHandler/ExceptionHandlerMiddlewareImpl.cs) and [status-code pages](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Middleware/Diagnostics/src/StatusCodePage/StatusCodePagesMiddleware.cs) define framework dispatch, boundary handling, and response eligibility. Adapt these implementations through their supported extension points.
- [ASP.NET Core status-code page extensions](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Middleware/Diagnostics/src/StatusCodePage/StatusCodePagesExtensions.cs) define how native status pages use configured options and re-execute requests.
