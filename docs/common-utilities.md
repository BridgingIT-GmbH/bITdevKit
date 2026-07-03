# Common Utilities Documentation

> Collect low-level utility building blocks for resiliency, activity helpers, ids, hashing, cloning, and more.

[TOC]

## Overview

The devkit includes a broad set of shared utilities for cross-cutting runtime concerns. It is not one single feature. Instead, it groups several lower-level building blocks that support application, domain, infrastructure, and presentation code.

This includes:

- resiliency and concurrency helpers such as `Retryer`, `Debouncer`, `Throttler`, `CircuitBreaker`, `RateLimiter`, `Bulkhead`, and `TimeoutHandler`
- lightweight background and in-process messaging helpers such as `BackgroundWorker`, `SimpleNotifier`, and `SimpleRequester`
- reusable diagram builders and Mermaid renderers for state, flow, activity, sequence, class, and component diagrams
- business calendars with culture-based registration and dynamic calculated holidays
- date/time range utilities with half-open range algebra
- human-readable duration and relative-time text formatting
- dynamic predicate and reflection helpers
- content-type, compression, hashing, and cloning utilities
- id and key generation helpers
- low-level activity and tracing helpers
- startup-task primitives and behaviors
- validation helpers for FluentValidation

Some of those areas also have higher-level feature docs elsewhere in `docs/`. This page focuses on the shared utilities available across the devkit and gives a short usage example for each main utility family.

## Business Calendars

Business calendars provide culture-aware working-day calculations for due dates, planning windows, and date ranges.

Use them when an application needs weekends, holidays, regional calendars, or tenant-specific working-day rules to influence date calculations.

### Registration

Register calendars once in `Program.cs`. Calendar-aware convenience methods then resolve the matching calendar from the supplied culture at runtime.

```csharp
builder.Services.AddBusinessCalendars(calendars => calendars
    .SetDefault(new BusinessCalendar())
    .RegisterCountry("NL", new BusinessCalendar(
        holidays: [
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 25)
        ]))
    .Register(
        CultureInfo.GetCultureInfo("de-DE"),
        new DynamicBusinessCalendar(
            new CalculatedHolidayProvider([
                new CalculatedHoliday("Good Friday", year =>
                    HolidayCalculations.GregorianEasterSunday(year).AddDays(-2)),
                new CalculatedHoliday("Easter Monday", year =>
                    HolidayCalculations.GregorianEasterSunday(year).AddDays(1))
            ]),
            nonWorkingDays: [DayOfWeek.Saturday, DayOfWeek.Sunday])));
```

Use the registered calendar through the date helpers when calculating due dates, reminders, or SLA deadlines:

```csharp
var culture = CultureInfo.GetCultureInfo("nl-NL");

var isOpen = date.IsBusinessDay(culture);
var dueDate = date.AddBusinessDays(3, culture);
var dueAt = createdAt.AddBusinessDays(2, culture);
```

For code that already has a calendar instance, call the calendar directly:

```csharp
var calendar = new BusinessCalendar(
    nonWorkingDays: [DayOfWeek.Friday, DayOfWeek.Saturday],
    holidays: [new DateOnly(2026, 1, 1)],
    rules: [
        new FixedHolidayRule([new FixedHoliday(12, 25, "Christmas Day")]),
        new ObservedHolidayRule([new FixedHoliday(1, 1, "New Year")])
    ]);

var nextWorkday = calendar.NextBusinessDay(date, includeCurrent: true);
var previousWorkday = calendar.PreviousBusinessDay(date);
var workingDaysInWindow = calendar.CountBusinessDays(start, end);
var info = calendar.GetBusinessDayInfo(date);
```

For libraries, console tools, and tests that do not use dependency injection, register calendars globally:

```csharp
BusinessCalendars.SetDefault(new BusinessCalendar());
BusinessCalendars.RegisterCountry("NL", dutchCalendar);

var dueDate = invoiceDate.AddBusinessDays(10, CultureInfo.GetCultureInfo("nl-NL"));
```

Resolution order is:

- exact culture, such as `nl-NL`
- country code, such as `NL`
- neutral language code, such as `nl`
- default calendar

### DI-Aware Calendars

Calendars that need services can be registered with factories or implementation types. This is useful for calendars that need configuration, tenants, repositories, or other scoped services.

```csharp
builder.Services.AddScoped<TenantHolidayRepository>();

builder.Services.AddBusinessCalendars(calendars => calendars
    .Register(
        CultureInfo.GetCultureInfo("nl-NL"),
        serviceProvider => new TenantBusinessCalendar(
            serviceProvider.GetRequiredService<TenantHolidayRepository>())));
```

For service-backed registrations, inject `IBusinessCalendarResolver` where the calendar is needed:

```csharp
public sealed class DueDateService(IBusinessCalendarResolver calendars)
{
    public DateOnly Calculate(DateOnly start, CultureInfo culture)
    {
        var calendar = calendars.Resolve(culture);
        return calendar.AddBusinessDays(start, 5);
    }
}
```

For database-backed holidays, create an application-specific `IBusinessCalendar` or `IHolidayProvider` that uses the project's repository or `DbContext`, then register it with `AddBusinessCalendars`.

Use `GetBusinessDayInfo` when the UI or audit log needs the reason a date is blocked:

```csharp
var info = calendar.GetBusinessDayInfo(date);

if (!info.IsBusinessDay)
{
    logger.LogInformation("Date is unavailable: {Reason}", info.Reason);
}
```

### Dynamic Holidays

Use `DynamicBusinessCalendar` when holidays are calculated by year instead of stored as a fixed list. The built-in `CalculatedHolidayProvider` supports simple year-based rules, and projects can implement `IHolidayProvider` for richer logic.

```csharp
var calendar = new DynamicBusinessCalendar(
    new CalculatedHolidayProvider([
        new CalculatedHoliday("Good Friday", year =>
            HolidayCalculations.GregorianEasterSunday(year).AddDays(-2)),
        new CalculatedHoliday("Easter Sunday", HolidayCalculations.GregorianEasterSunday)
    ]));
```

Dynamic calendars can also combine calculated holidays with business-day rules:

```csharp
var calendar = new DynamicBusinessCalendar(
    new CalculatedHolidayProvider([
        new CalculatedHoliday("Easter Monday", year =>
            HolidayCalculations.GregorianEasterSunday(year).AddDays(1))
    ]),
    rules: [
        new CustomBusinessDayRule(date =>
            date.Month == 12 && date.Day == 24
                ? new BusinessDayRuleResult(BusinessDayRuleResultKind.NonWorkingDay, "Company closure")
                : BusinessDayRuleResult.NoMatch)
    ]);
```

## Human-Readable Duration Text

Human-readable duration and relative-time text formats durations and relative values using the language registered for the current or supplied culture. Use it for activity feeds, notification text, dashboard ages, and compact duration labels.

Built-in languages are available for English, German, French, Dutch, Spanish, and Italian.

```csharp
var culture = CultureInfo.GetCultureInfo("nl-NL");

var text = TimeSpan.FromMinutes(3).ToDurationText(
    new RelativeTimeFormatOptions { Culture = culture });
```

The formatting methods resolve exact culture first, then neutral language, then the configured fallback language.

Format elapsed durations without relative suffixes:

```csharp
var duration = TimeSpan.FromMilliseconds(250);

var longText = duration.ToDurationText(); // 250 milliseconds
var shortText = duration.ToDurationText(new RelativeTimeFormatOptions
{
    UseShortUnits = true
}); // 250ms
```

Format dates and times relative to a reference value:

```csharp
var reference = new DateTime(2026, 6, 29, 12, 0, 0, DateTimeKind.Utc);

var past = reference.AddMinutes(-5).ToRelativeTimeText(reference); // 5 minutes ago
var future = reference.AddHours(2).ToRelativeTimeText(reference); // in 2 hours
```

Use the same API for date-only labels, time-only labels, and offset-aware instants:

```csharp
var dateLabel = DateOnly.FromDateTime(DateTime.Today)
    .AddDays(-1)
    .ToRelativeTimeText(DateOnly.FromDateTime(DateTime.Today)); // 1 day ago

var timeLabel = new TimeOnly(14, 30)
    .ToRelativeTimeText(new TimeOnly(14, 0)); // in 30 minutes

var instantLabel = eventTime.ToRelativeTimeText(DateTimeOffset.UtcNow);
```

Use options when UI text needs predictable rounding, compact units, or a different "just now" threshold:

```csharp
var timestamp = DateTimeOffset.UtcNow.AddMinutes(-90);

var text = timestamp.ToRelativeTimeText(DateTimeOffset.UtcNow, new RelativeTimeFormatOptions
{
    Culture = CultureInfo.GetCultureInfo("de-DE"),
    UseShortUnits = true,
    RoundingMode = RelativeTimeRoundingMode.Round,
    MinimumUnit = RelativeTimeUnit.Second,
    NowThreshold = TimeSpan.FromSeconds(10)
});
```

Add more application languages by implementing `IRelativeTimeLanguage` and registering them during startup:

```csharp
public sealed class PolishRelativeTimeLanguage : IRelativeTimeLanguage
{
    public string LanguageCode => "pl";

    public string Now(bool shortText) => "teraz";

    public string FormatUnit(RelativeTimeUnit unit, long value, bool shortText) => unit switch
    {
        RelativeTimeUnit.Millisecond => $"{value}ms",
        RelativeTimeUnit.Second => shortText ? $"{value}s" : $"{value} sekund",
        RelativeTimeUnit.Minute => shortText ? $"{value} min." : $"{value} minut",
        RelativeTimeUnit.Hour => shortText ? $"{value} godz." : $"{value} godzin",
        RelativeTimeUnit.Day => shortText ? $"{value} d" : $"{value} dni",
        _ => $"{value}"
    };

    public string FormatPast(string durationText, bool shortText) => $"{durationText} temu";

    public string FormatFuture(string durationText, bool shortText) => $"za {durationText}";
}

RelativeTimeLanguages.Register(new PolishRelativeTimeLanguage());
```

Set a fallback language when the application prefers a built-in language other than English for unsupported cultures:

```csharp
RelativeTimeLanguages.SetFallback("de");

var text = TimeSpan.FromMinutes(3).ToDurationText(
    new RelativeTimeFormatOptions { Culture = CultureInfo.GetCultureInfo("sv-SE") });
```

## Date And Time Ranges

The range types use half-open `[start, end)` semantics and support one open boundary:

- `DateTimeRange`
- `DateTimeOffsetRange`
- `DateOnlyRange`
- `TimeOnlyRange`

They are sortable and comparable, and include overlap, containment, intersection, union, gap, normalization, splitting, and ISO interval parsing/formatting helpers.

Sorting places open starts first and open ends last, which is useful before normalization or conflict checks:

```csharp
var sorted = ranges.OrderBy(range => range).ToArray();
```

Use finite ranges for bookings, report windows, retention periods, and availability checks:

```csharp
var range = new DateOnlyRange(
    new DateOnly(2026, 1, 1),
    new DateOnly(2026, 2, 1));

var contains = range.Contains(new DateOnly(2026, 1, 15));
var businessDays = range.BusinessDays(CultureInfo.GetCultureInfo("nl-NL")).ToArray();
var count = range.BusinessDayCount(CultureInfo.GetCultureInfo("nl-NL"));
```

Use open-ended ranges for states that start or end at a boundary, such as "valid from" or "valid until":

```csharp
var validFrom = new DateTimeOffsetRange(
    startInclusive: DateTimeOffset.UtcNow,
    endExclusive: null);

var validUntil = new DateOnlyRange(
    startInclusive: null,
    endExclusive: new DateOnly(2026, 12, 31));
```

Find conflicts, merge adjacent ranges, or calculate gaps:

```csharp
var requested = new TimeOnlyRange(new TimeOnly(9, 0), new TimeOnly(11, 0));
var existing = new TimeOnlyRange(new TimeOnly(10, 30), new TimeOnly(12, 0));

if (requested.TryIntersection(existing, out var conflict))
{
    // conflict is 10:30:00/11:00:00
}

var gap = requested.Gap(new TimeOnlyRange(new TimeOnly(13, 0), new TimeOnly(14, 0)));
```

Merge overlapping or adjacent ranges when building availability windows:

```csharp
if (requested.TryMerge(existing, out var merged))
{
    availability = merged;
}

var union = requested.Union(existing);
```

Normalize unsorted or touching ranges before storing or comparing them:

```csharp
var normalized = new[]
{
    new DateTimeRange(new DateTime(2026, 1, 5), new DateTime(2026, 1, 10)),
    new DateTimeRange(new DateTime(2026, 1, 1), new DateTime(2026, 1, 5))
}.Normalize();
```

Split finite ranges for grouping and reporting:

```csharp
var invoicePeriod = new DateOnlyRange(
    new DateOnly(2026, 1, 15),
    new DateOnly(2026, 4, 1));

var months = invoicePeriod.SplitByMonth().ToArray();
```

Use ISO interval text at API boundaries, query strings, and persisted filters:

```csharp
var text = range.ToIsoRangeString(); // 2026-01-01/2026-02-01

if ("2026-01-01/2026-02-01".TryParseDateOnlyRange(out var parsed))
{
    var days = parsed.Days;
}
```

Convert date-only ranges to instants when scheduling across offsets or time zones:

```csharp
var localPeriod = new DateOnlyRange(
    new DateOnly(2026, 6, 1),
    new DateOnly(2026, 6, 8));

var offsetRange = localPeriod.AtStartAndEndOfDay(TimeSpan.FromHours(2));
```

Convert offset ranges to a target time zone when displaying schedules:

```csharp
var displayRange = offsetRange.ToTimeZone(userTimeZone);
```

### Entity Framework Core

Store ranges as two boundary columns when an entity needs to persist them. This keeps filters, indexes, and overlap queries database-friendly while the entity can still expose a range value.

```csharp
public sealed class Contract
{
    private DateOnly? validityStart;
    private DateOnly? validityEnd;

    public Guid Id { get; private set; }

    public DateOnlyRange Validity => new(this.validityStart, this.validityEnd);

    public void ChangeValidity(DateOnlyRange validity)
    {
        this.validityStart = validity.StartInclusive;
        this.validityEnd = validity.EndExclusive;
    }
}
```

Configure the boundary fields as columns and ignore the computed range property:

```csharp
public sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.HasKey(contract => contract.Id);

        builder.Ignore(contract => contract.Validity);

        builder.Property<DateOnly?>("validityStart")
            .HasColumnName("ValidityStart");

        builder.Property<DateOnly?>("validityEnd")
            .HasColumnName("ValidityEnd");

        builder.HasIndex("validityStart", "validityEnd");
    }
}
```

Use the same pattern for instant ranges:

```csharp
private DateTimeOffset? activeFrom;
private DateTimeOffset? activeUntil;

public DateTimeOffsetRange ActivePeriod => new(this.activeFrom, this.activeUntil);
```

When querying, compare the stored boundary columns so EF Core can translate the expression to SQL:

```csharp
var queryRange = new DateOnlyRange(
    new DateOnly(2026, 1, 1),
    new DateOnly(2026, 2, 1));
var queryStart = queryRange.StartInclusive;
var queryEnd = queryRange.EndExclusive;

var contracts = await db.Contracts
    .Where(contract =>
        (!queryStart.HasValue ||
            EF.Property<DateOnly?>(contract, "validityEnd") == null ||
            queryStart.Value < EF.Property<DateOnly?>(contract, "validityEnd").Value) &&
        (!queryEnd.HasValue ||
            EF.Property<DateOnly?>(contract, "validityStart") == null ||
            EF.Property<DateOnly?>(contract, "validityStart").Value < queryEnd.Value))
    .ToListAsync();
```

Use ISO interval strings for API filters, query strings, exports, or logs. For normal relational persistence, separate boundary columns are easier to query and index.

## Composition

`Common.Utilities.Composition` adds low-level service composition building blocks for developers who want explicit, fluent DI-driven composition without repeatedly hand-writing wrapper registration code.

Use it when you want to:

- wrap a service with ordered same-contract behavior
- adapt one contract to another through an explicit adapter
- attach reusable runtime interception behavior to an interface contract
- resolve implementations by named strategy key
- combine multiple implementations behind one composite
- run ordered request handlers as a chain

The public entry point is:

```csharp
var services = new ServiceCollection();

services.AddComposition();
```

### Pattern Guide

| Pattern | Use it when | Typical result |
| --- | --- | --- |
| `Decorator` | You need the same contract with explicit wrapper behavior. | Ordered wrapper classes around the implementation. |
| `Adapter` | You need to expose one contract through a different contract. | A translation layer between source and target services. |
| `Interception` | You need cross-cutting behavior around interface method calls. | Runtime behaviors such as logging, timeout, retry, metrics, authorization, or lazy activation. |
| `Strategy` | You need multiple named implementations and runtime selection. | A keyed resolver with optional default behavior. |
| `Composite` | You need to treat many implementations as one service. | One contract backed by a configured child set. |
| `Chain` | You need ordered handlers that may handle or pass on a request. | A `next`-driven pipeline with handled/unhandled outcomes. |

### Full Showcase

`AddComposition()` is additive, so multiple modules can contribute registrations and the final service resolves with the configured composition order of decorators, explicit interceptors, runtime interception behaviors, and the concrete implementation.

```csharp
var services = new ServiceCollection();

services.AddComposition()
    .For<IWeatherClient>()
        .Use<WeatherClient>()
        .Decorate(decorators => decorators
            .With<CachedWeatherClient>())
        .Intercept(interception => interception
            .With<AuthorizationWeatherInterceptor>()
            .WithLogging()
            .WithTimeout(TimeSpan.FromSeconds(5))
            .WithRetry(3))
        .RegisterScoped();

services.AddComposition()
    .Strategies<INotificationSender>()
    .Add<SmtpNotificationSender>("smtp")
    .Add<WebhookNotificationSender>("webhook")
    .WithDefault("smtp");

using var provider = services.BuildServiceProvider();

var weatherClient = provider.GetRequiredService<IWeatherClient>();
var notificationStrategies = provider.GetRequiredService<IStrategyResolver<INotificationSender>>();
var defaultSender = notificationStrategies.ResolveDefault();
```

### Decorator

Decorator pattern: wrap a service with one or more same-contract classes so you can add behavior before or after the inner implementation.

Use a decorator when the behavior should be a normal wrapper class that still implements the same contract.

```csharp
services.AddComposition()
    .For<INotificationSender>()
        .Use<SmtpNotificationSender>()
        .Decorate(decorators => decorators
            .With<LoggingNotificationSender>()
            .With<MetricsNotificationSender>())
        .RegisterScoped();
```

Typical constructor shape:

```csharp
public sealed class LoggingNotificationSender(INotificationSender inner, ILogger<LoggingNotificationSender> logger)
    : INotificationSender
{
    public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("sending notification");
        return inner.SendAsync(message, cancellationToken);
    }
}
```

### Adapter

Adapter pattern: translate one API or contract into another contract that the rest of the application expects.

Use an adapter when you already have an implementation but consumers need a different contract.

```csharp
services.AddComposition()
    .Adapt<LegacyMailClient>()
    .To<INotificationSender>()
    .Using<LegacyMailClientAdapter>()
    .RegisterScoped();

using var provider = services.BuildServiceProvider();
var sender = provider.GetRequiredService<INotificationSender>();
```

Typical adapter shape:

```csharp
public sealed class LegacyMailClientAdapter(LegacyMailClient client)
    : INotificationSender
{
    public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        client.Send(message.Subject, message.Body, message.Recipients);
        return Task.CompletedTask;
    }
}
```

For adapting non-DI source instances at runtime, use `IAdapterFactory`:

```csharp
var adapterFactory = provider.GetRequiredService<IAdapterFactory>();
var client = new LegacyMailClient();
var sender = adapterFactory.Adapt<LegacyMailClient, INotificationSender>(client);
```

### Interception

Interception pattern: surround method calls on the same contract so cross-cutting concerns can run around the invocation pipeline.

Interception is interface-only and is designed for cross-cutting method behavior, not for hiding application logic. When runtime behaviors such as logging, retry, timeout, metrics, authorization, or lazy activation are configured, interception may internally create an interface proxy host for that invocation chain. Built-in retry and timeout interception reuse the existing `Retryer` and `TimeoutHandler` utilities instead of adding a second resiliency engine.

```csharp
var services = new ServiceCollection();

services.AddComposition()
    .For<IInventoryClient>()
        .Use<InventoryClient>()
        .Intercept(interception => interception
            .With<InventoryAuthorizationInterceptor>()
            .WithLogging()
            .WithMetrics()
            .WithAuthorization()
            .WithLazy()
            .WithTimeout(TimeSpan.FromSeconds(2))
            .WithRetry(3))
        .RegisterTransient();

using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IInventoryClient>();
```

Typical explicit interceptor shape:

```csharp
public sealed class InventoryAuthorizationInterceptor(
    IInventoryClient inner,
    ICurrentUserService currentUser)
    : IInventoryClient
{
    public async Task<InventoryItem> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission("inventory.read"))
        {
            throw new UnauthorizedAccessException("Missing inventory.read permission.");
        }

        return await inner.GetBySkuAsync(sku, cancellationToken);
    }
}
```

Typical runtime authorization authorizer shape for `.WithAuthorization()`:

```csharp
public sealed class InventoryAuthorizationAuthorizer
    : IInterceptionAuthorizer<IInventoryClient>
{
    public ValueTask<Result> AuthorizeAsync(
        InterceptionInvocationContext<IInventoryClient> context,
        CancellationToken cancellationToken = default)
    {
        var isAllowed = context.Method.Name.StartsWith("Get", StringComparison.Ordinal);
        return ValueTask.FromResult(isAllowed
            ? Result.Success()
            : Result.Failure().WithMessage("Inventory operation is not authorized."));
    }
}
```

Execution order is:

```text
Decorators
-> Explicit interceptors added with .With<TInterceptor>()
-> Runtime interception behaviors such as logging/retry/timeout
-> Concrete implementation
```

### Strategy

Strategy pattern: register multiple implementations for the same contract and choose one by key at runtime.

Use a strategy when runtime selection by string key is part of the design.

```csharp
services.AddComposition()
    .Strategies<INotificationSender>()
    .Add<SmtpNotificationSender>("smtp")
    .Add<WebhookNotificationSender>("webhook")
    .WithDefault("smtp");

using var provider = services.BuildServiceProvider();
var resolver = provider.GetRequiredService<IStrategyResolver<INotificationSender>>();

var sender = resolver.Resolve("webhook");
var defaultSender = resolver.ResolveDefault();
var availableKeys = resolver.Keys;
```

Typical strategy implementations:

```csharp
public sealed class SmtpNotificationSender : INotificationSender
{
    public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public sealed class WebhookNotificationSender : INotificationSender
{
    public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
```

### Composite

Composite pattern: combine several implementations behind one contract so callers interact with one service while the composite coordinates its children.

Use a composite when multiple implementations should be coordinated behind one contract.

```csharp
services.AddComposition()
    .Composite<INotificationSender, BroadcastNotificationSender>(children => children
        .With<EmailNotificationSender>()
        .With<TeamsNotificationSender>()
        .With<WebhookNotificationSender>())
    .RegisterScoped();

using var provider = services.BuildServiceProvider();
var sender = provider.GetRequiredService<INotificationSender>();
```

Typical composite constructor shape:

```csharp
public sealed class BroadcastNotificationSender(IEnumerable<INotificationSender> children)
    : INotificationSender
{
    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        foreach (var child in children)
        {
            await child.SendAsync(message, cancellationToken);
        }
    }
}
```

### Chain

Chain of responsibility pattern: pass a request through ordered handlers until one handles it or the chain completes without a handler taking responsibility.

Use a chain when each handler may process the request or pass it to the next handler.

```csharp
services.AddComposition()
    .Chain<IImportHandler, ImportContext>(chain => chain
        .With<CsvImportHandler>()
        .With<JsonImportHandler>()
        .With<XmlImportHandler>())
    .RegisterScoped();

using var provider = services.BuildServiceProvider();
var executor = provider.GetRequiredService<IChainExecutor<ImportContext>>();
var result = await executor.ExecuteAsync(new ImportContext("orders.csv"), cancellationToken);
```

Handlers return `ChainResult` to indicate whether the request was handled.

Typical handler shape:

```csharp
public sealed class CsvImportHandler : IImportHandler
{
    public ValueTask<ChainResult> HandleAsync(
        ImportContext context,
        ChainExecutionDelegate<ImportContext> next,
        CancellationToken cancellationToken)
    {
        if (!context.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return next(context, cancellationToken);
        }

        return ValueTask.FromResult(new ChainResult
        {
            Handled = true,
            Result = Result.Success()
        });
    }
}
```

### Choosing A Pattern

- Use `Decorator` when the behavior should be visible as an explicit wrapper class and still implement the same contract.
- Use `Adapter` when the implementation already exists but the consuming code needs a different contract.
- Use `Interception` when the behavior is cross-cutting and method-oriented rather than domain-specific.
- Use `Strategy` when runtime selection by string key is part of the design.
- Use `Composite` when multiple implementations should be coordinated behind one contract.
- Use `Chain` when each handler may continue or stop processing.

### Limitations

- Runtime interception supports interface contracts only. Class proxying is not included.
- The composition package does not include caching interception behavior.
- Configuration is intentionally explicit and fluent. It does not rely on source generation or attribute-first setup.

## Diagrams

The shared diagrams utilities provide a lightweight, deterministic way to build reusable diagram documents and render them without bringing in external graph or diagram packages.

The current Common diagrams surface covers:

- `StateDiagramBuilder` with `MermaidStateDiagramRenderer`
- `StateDiagramBuilder` with `SvgStateDiagramRenderer`
- `FlowDiagramBuilder` with `MermaidFlowDiagramRenderer`
- `FlowDiagramBuilder` with `SvgFlowDiagramRenderer`
- `ActivityDiagramBuilder` with `MermaidActivityDiagramRenderer`
- `ActivityDiagramBuilder` with `SvgActivityDiagramRenderer`
- `SequenceDiagramBuilder` with `MermaidSequenceDiagramRenderer`
- `SequenceDiagramBuilder` with `SvgSequenceDiagramRenderer`
- `ClassDiagramBuilder` with `MermaidClassDiagramRenderer`
- `ClassDiagramBuilder` with `SvgClassDiagramRenderer`
- `ComponentDiagramBuilder` with `MermaidComponentDiagramRenderer`
- `ComponentDiagramBuilder` with `SvgComponentDiagramRenderer`
- `BitmapDiagramRenderer` placeholder registrations for all built-in diagram kinds
- `AddDiagramRendering()` and `IDiagramRendererFactory` for dependency injection driven renderer resolution by `DiagramKind` and `DiagramRenderFormat`

The renderer abstraction is format-aware. Mermaid remains the main built-in text format, every built-in diagram kind now also has a native SVG renderer, and bitmap registrations are present as explicit `NotImplementedException` placeholders through `DiagramRenderFormat`, `DiagramRenderResult`, and format-specific render option types.

Example:

```csharp
var document = new SequenceDiagramBuilder()
    .AddParticipant("User", kind: DiagramNodeKind.Actor)
    .AddParticipant("Api", "Todo API")
    .AddMessage("User", "Api", "GET /todos")
    .AddMessage("Api", "User", "200 OK", DiagramEdgeKind.Reply)
    .Build();

var renderer = new MermaidSequenceDiagramRenderer();
var mermaid = renderer.Render(document).GetText();

var services = new ServiceCollection();
services.AddDiagramRendering();
var provider = services.BuildServiceProvider();
var factory = provider.GetRequiredService<IDiagramRendererFactory>();

var svg = factory.Render(
    new StateDiagramBuilder()
        .AddState("Created")
        .AddTransition("[*]", "Created")
        .Build(),
    DiagramRenderFormat.Svg,
    new SvgDiagramRenderOptions { Width = 640, Height = 320 })
    .GetText();
```

## Resiliency Helpers

The strongest concentration of reusable behavior here is the resiliency set.

### Retryer

`Retryer` reruns an asynchronous operation a configured number of times with a fixed or exponential delay.

Use it when:

- an operation can fail transiently
- the caller should stay in-process instead of using an external queue
- retry state and progress should remain explicit in code

Key capabilities:

- fixed-delay retries
- optional exponential backoff
- `Task` and `Task<T>` overloads
- optional `ILogger`-based error handling
- optional `IProgress<RetryProgress>` reporting

Example:

```csharp
var progress = new Progress<RetryProgress>(p =>
    Console.WriteLine($"retry {p.CurrentAttempt}/{p.MaxAttempts}: {p.Status}"));

var retryer = new RetryerBuilder(3, TimeSpan.FromSeconds(1))
    .UseExponentialBackoff()
    .WithProgress(progress)
    .Build();

await retryer.ExecuteAsync(
    async cancellationToken => await ImportAsync(cancellationToken),
    cancellationToken);
```

### Debouncer And SimpleDebouncer

`Debouncer` delays execution until no new call arrived during the configured interval. It is useful for noisy inputs such as UI typing, file-change bursts, or repeated refresh triggers.

`SimpleDebouncer` is the lighter-weight sibling when you only need basic delayed coalescing behavior without the richer progress shape.

Use them when:

- repeated calls should collapse into one execution
- the latest trigger matters more than the earlier ones
- you want cancellation-aware delayed execution

Example:

```csharp
var progress = new Progress<DebouncerProgress>(p => Console.WriteLine(p.Status));

var debouncer = new DebouncerBuilder(
        TimeSpan.FromMilliseconds(500),
        async ct => await SearchAsync(ct))
    .WithProgress(progress)
    .Build();

await debouncer.DebounceAsync(cancellationToken);

using var simpleDebouncer = new SimpleDebouncer(
    TimeSpan.FromMilliseconds(250),
    async () => await SaveDraftAsync());

simpleDebouncer.Debounce();
```

### Throttler

`Throttler` lets calls happen immediately and then suppresses repeated execution until the throttle interval expires.

Use it when:

- work should happen at most once per interval
- first-call responsiveness matters
- repeated triggers during the interval should not queue up unlimited work

Example:

```csharp
var progress = new Progress<ThrottlerProgress>(p =>
    Console.WriteLine($"remaining: {p.RemainingInterval.TotalMilliseconds} ms"));

using var throttler = new ThrottlerBuilder(
        TimeSpan.FromSeconds(1),
        async ct => await RefreshCacheAsync(ct))
    .WithProgress(progress)
    .Build();

await throttler.ThrottleAsync(cancellationToken);
```

### CircuitBreaker

`CircuitBreaker` protects callers from repeatedly invoking a failing dependency.

Key capabilities:

- `Closed`, `Open`, and `HalfOpen` states
- configurable failure threshold
- configurable reset timeout
- optional handled-error mode
- optional `IProgress<CircuitBreakerProgress>` reporting

Use it when:

- a downstream dependency is unstable
- fast failure is better than repeatedly waiting for the same failing call
- you want the dependency to get a recovery window before traffic resumes

Example:

```csharp
var progress = new Progress<CircuitBreakerProgress>(p =>
    Console.WriteLine($"{p.State}: {p.Status}"));

var circuitBreaker = new CircuitBreakerBuilder(3, TimeSpan.FromSeconds(30))
    .WithProgress(progress)
    .Build();

await circuitBreaker.ExecuteAsync(
    async ct => await CallRemoteServiceAsync(ct),
    cancellationToken);
```

### TimeoutHandler

`TimeoutHandler` wraps an async operation with a maximum allowed duration.

Use it when:

- a call should not outlive a known SLA
- you need explicit timeout behavior even when the underlying code lacks one
- callers need remaining-time progress information

Example:

```csharp
var progress = new Progress<TimeoutHandlerProgress>(p =>
    Console.WriteLine($"{p.RemainingTime.TotalSeconds:n1}s remaining"));

var timeout = new TimeoutHandlerBuilder(TimeSpan.FromSeconds(5))
    .WithProgress(progress)
    .Build();

await timeout.ExecuteAsync(
    async ct => await GenerateReportAsync(ct),
    cancellationToken);
```

### Bulkhead

`Bulkhead` limits concurrency using a semaphore and isolates pressure from one workload from starving the rest of the process.

Use it when:

- only a fixed number of expensive operations should run in parallel
- you want queued work rather than unrestricted concurrency
- a resource such as CPU, network, or a fragile dependency needs protection

Example:

```csharp
var progress = new Progress<BulkheadProgress>(p =>
    Console.WriteLine($"{p.CurrentConcurrency}/{p.MaxConcurrency} active"));

var bulkhead = new BulkheadBuilder(4)
    .WithProgress(progress)
    .Build();

await bulkhead.ExecuteAsync(
    async ct => await ProcessFileAsync(ct),
    cancellationToken);
```

### RateLimiter

`RateLimiter` enforces a maximum number of operations inside a time window.

Use it when:

- a dependency has rate limits
- local work should be smoothed over time
- excess requests should fail or be skipped explicitly

Example:

```csharp
var progress = new Progress<RateLimiterProgress>(p =>
    Console.WriteLine($"{p.CurrentOperations}/{p.MaxOperations} in window"));

var rateLimiter = new RateLimiterBuilder(10, TimeSpan.FromMinutes(1))
    .WithProgress(progress)
    .Build();

await rateLimiter.ExecuteAsync(
    async ct => await SendWebhookAsync(ct),
    cancellationToken);
```

### BackgroundWorker

`BackgroundWorker` is a lightweight helper for running cancellable background work with progress reporting.

Use it when:

- you want an in-process long-running task with cooperative cancellation
- the work should expose progress updates
- a full hosted-service or scheduler abstraction would be excessive

Example:

```csharp
var progress = new Progress<BackgroundWorkerProgress>(p =>
    Console.WriteLine($"{p.ProgressPercentage}%"));

var worker = new BackgroundWorkerBuilder(async (ct, p) =>
    {
        for (var i = 0; i <= 100; i += 10)
        {
            await Task.Delay(100, ct);
            p.Report(i);
        }
    })
    .WithProgress(progress)
    .Build();

await worker.StartAsync(cancellationToken);
```

### SimpleNotifier And SimpleRequester

These two types are lightweight in-process messaging helpers:

- `SimpleNotifier`: publish/subscribe notification fan-out
- `SimpleRequester`: single-handler request/response dispatch

They support progress reporting and pipeline-style extensibility, but they are the lighter-weight option. For the richer devkit-level guidance around in-process request/notification handling, see [Requester and Notifier](./features-requester-notifier.md).

Example:

```csharp
public sealed record UserImported(string Email) : ISimpleNotification;
public sealed record Ping(string Text) : ISimpleRequest<string>;

var notifier = new SimpleNotifierBuilder()
    .WithProgress(new Progress<SimpleNotifierProgress>(p => Console.WriteLine(p.Status)))
    .Build();

notifier.Subscribe<UserImported>((message, ct) =>
{
    Console.WriteLine(message.Email);
    return ValueTask.CompletedTask;
});

await notifier.PublishAsync(new UserImported("alice@example.com"), cancellationToken: cancellationToken);

var requester = new SimpleRequesterBuilder()
    .WithProgress(new Progress<SimpleRequesterProgress>(p => Console.WriteLine(p.Status)))
    .Build();

requester.RegisterHandler<Ping, string>((request, ct) => new ValueTask<string>($"pong: {request.Text}"));

var response = await requester.SendAsync<Ping, string>(new Ping("hello"), cancellationToken: cancellationToken);
```

### Progress Types

The resiliency family also defines typed progress models such as:

- `RetryProgress`
- `DebouncerProgress`
- `ThrottlerProgress`
- `CircuitBreakerProgress`
- `RateLimiterProgress`
- `BackgroundWorkerProgress`
- `TimeoutHandlerProgress`
- `BulkheadProgress`
- `SimpleNotifierProgress`
- `SimpleRequesterProgress`

That lets callers observe utility-specific state without reducing everything to plain log messages.

Example:

```csharp
var progress = new Progress<RetryProgress>(p =>
    logger.LogInformation("attempt {Attempt}/{Max}: {Status}", p.CurrentAttempt, p.MaxAttempts, p.Status));
```

## Metrics

The devkit metrics feature is a thin developer-friendly layer over .NET diagnostics metrics from `System.Diagnostics.Metrics`.

It does not invent a separate metrics runtime. Instead, it builds on the standard .NET `Meter`, `Counter<T>`, `UpDownCounter<T>`, and `Histogram<T>` primitives so applications can emit custom metrics and let the hosting app decide how those metrics are collected and exported.

The shared devkit meter name is `bdk`.

### What It Provides

- `Metrics` for low-level metric naming and recording helpers
- `IMetricsService` and `MetricsService` for application code that wants a simpler API
- `AddMetrics(...)` for DI registration
- optional system metrics endpoints via `AddMetrics(options => options.AddEndpoints())`
- built-in metrics behaviors for features such as requester, notifier, messaging, queueing, job scheduling, orchestrations, and repositories

### Registering Metrics

Register the feature once in the host:

```csharp
services.AddMetrics(options => options
    .Enabled()
    .AddEndpoints());
```

That registers `IMetricsService` and the supporting snapshot services used by the web metrics endpoints.

### Emitting Custom Metrics

Use `IMetricsService` in application or infrastructure code when you want custom metrics without dealing with raw `Meter` APIs directly.

```csharp
public sealed class InventoryRefreshService(IMetricsService metrics)
{
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        using var scope = metrics.Track("inventory_refresh", "warehouse_a");

        try
        {
            await Task.Delay(50, cancellationToken);
        }
        catch
        {
            metrics.IncrementFailure("inventory_refresh", "warehouse_a");
            throw;
        }
    }
}
```

The helper methods map to standard metric concepts:

- `Increment(...)` records cumulative totals
- `IncrementFailure(...)` records failure totals
- `ChangeCurrent(...)` records live concurrency style values with an up/down counter
- `RecordDuration(...)` records latency in milliseconds with a histogram
- `Track(...)` combines total, current, and duration tracking into one disposable scope

Metric names are normalized automatically and follow the shared naming pattern:

- base series: `family_part_a_part_b`
- failure series: `family_part_a_part_b_failure`
- current series: `family_part_a_part_b_current`
- duration series: `family_part_a_part_b_duration`

Prefer low-cardinality parts such as operation names, message types, or status values. Avoid ids, titles, emails, or other unbounded values in metric parts.

### Built-In Feature Metrics

Several devkit features already have ready-made behaviors that emit metrics without additional custom instrumentation in your handlers or services.

Examples include:

- `MetricsRequestBehavior<,>`
- `MetricsNotificationBehavior<,>`
- `MetricsNotificationHandlerBehavior<,>`
- `MetricsMessagePublisherBehavior`
- `MetricsMessageHandlerBehavior`
- `MetricsQueueEnqueuerBehavior`
- `MetricsQueueHandlerBehavior`
- `MetricsJobSchedulingBehavior`
- `MetricsOrchestrationBehavior`
- `RepositoryMetricsBehavior`
- composition interception via `.WithMetrics()`

That means developers can often add metrics to higher-level features just by registering the corresponding behavior:

```csharp
services.AddMessaging(builder.Configuration)
    .WithBehavior<MetricsMessagePublisherBehavior>()
    .WithBehavior<MetricsMessageHandlerBehavior>();

services.AddJobScheduling(builder.Configuration)
    .WithBehavior<MetricsJobSchedulingBehavior>();
```

### OpenTelemetry And Collector Compatibility

The instrumentation itself is OpenTelemetry-friendly because it uses the standard .NET diagnostics metrics stack.

In practice that means a host can export devkit metrics to an OpenTelemetry collector as long as it:

- registers OpenTelemetry metrics
- subscribes to the `bdk` meter
- configures an exporter such as OTLP or Prometheus

The devkit does not configure OpenTelemetry exporters or collector endpoints on behalf of the host application. That setup remains the responsibility of the client application.

If a host wants devkit metrics to participate in its OpenTelemetry pipeline, it should make sure the `bdk` meter is included:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("bdk")
        .AddRuntimeInstrumentation()
        .AddAspNetCoreInstrumentation());
        // .AddOtlpExporter()
        // or .AddPrometheusExporter()
```

### Endpoints

Metrics exposes JSON snapshot endpoints such as:

- `/_bdk/api/metrics/bdk`
- `/_bdk/api/metrics/overview`
- `/_bdk/api/metrics/dotnet`
- `/_bdk/api/metrics/aspnet`

These endpoints are useful for dashboards, debugging, demos, and internal operational inspection. They are backed by in-process snapshot services that listen to the `bdk` meter and project the measurements into JSON models.

They are not an OTLP endpoint, and they are not a Prometheus scrape endpoint. Those concerns belong to the host application's OpenTelemetry configuration.

## Requester Utilities

The devkit also includes a fuller in-process request/notification stack than the simple resiliency helpers.

It includes:

- `Requester` and `RequesterBuilder`
- `Notifier` and `NotifierBuilder`
- DI registration helpers
- handler discovery and caching
- pipeline behaviors
- policy attributes for retry, timeout, chaos, cache invalidation, and authorization
- no-op implementations for optional wiring scenarios

This area overlaps conceptually with the higher-level feature documentation in [Requester and Notifier](./features-requester-notifier.md). That feature page should be the main conceptual guide. This page just gives a short orientation and example.

Example:

```csharp
services.AddRequester()
    .AddHandlers()
    .WithBehavior(typeof(ValidationPipelineBehavior<,>))
    .WithRetryOptions(3, 250);

services.AddNotifier()
    .AddHandlers();
```

## Startup Task Utilities

The devkit includes shared startup-task primitives and behaviors, including:

- `StartupTaskOptions`
- `StartupTaskOptionsBuilder`
- `StartupTasksBuilderContext`
- retry, timeout, circuit-breaker, and chaos behaviors for startup work

These are the lower-level building blocks behind the startup-task concept. For the feature-level usage story, see [StartupTasks](./features-startuptasks.md).

Example:

```csharp
var options = new StartupTaskOptionsBuilder()
    .Enabled()
    .Order(100)
    .StartupDelay(TimeSpan.FromSeconds(5))
    .HaltOnFailure()
    .Build();
```

## Activity And Tracing Helpers

The devkit provides lower-level helpers around `System.Diagnostics.Activity` and `ActivitySource`.

This includes:

- `ActivityHelper`
- `ActivitySourceExtensions`
- `ActivityConstants`

Use these helpers when you need explicit activity creation, tagging, baggage propagation, or exception/status recording in low-level code.

This is closely related to [Common Observability Tracing](./common-observability-tracing.md), which documents the higher-level tracing conventions and decorators used elsewhere in the devkit.

Example:

```csharp
var source = new ActivitySource("MyModule");

await source.StartActvity(
    "import-users",
    async (activity, ct) =>
    {
        activity?.SetTag("tenant.id", tenantId);
        await ImportUsersAsync(ct);
    },
    cancellationToken: cancellationToken);
```

## Reflection And Expression Helpers

### PredicateBuilder

`PredicateBuilder<T>` is a fluent builder for dynamic LINQ predicates.

It is useful when:

- filters are assembled conditionally
- the final predicate must stay EF Core compatible
- nested `if` trees would otherwise make query construction noisy

It supports:

- `Add(...)` and `Or(...)`
- conditional additions like `AddIf(...)` and `OrIf(...)`
- grouped conditions
- custom combinators

Example:

```csharp
var predicate = new PredicateBuilder<Customer>()
    .Add(c => c.IsActive)
    .AddIf(minAge.HasValue, c => c.Age >= minAge.Value)
    .BeginGroup(useOr: true)
    .Add(c => c.City == "Berlin")
    .Or(c => c.City == "Hamburg")
    .EndGroup()
    .BuildExpression();

var customers = dbContext.Customers.Where(predicate);
```

### ReflectionHelper And PrivateReflection

`ReflectionHelper` provides cached reflection access and helpers for:

- reading and writing properties dynamically
- discovering methods and properties with caching
- creating low-level getter delegates
- scanning assemblies for matching types

Private-reflection helpers complement that with more ergonomic access to non-public members.

These helpers are mainly useful in infrastructure, testing, diagnostics, and framework-style code where dynamic access is justified.

Example:

```csharp
var customer = new Customer();

ReflectionHelper.SetProperty(customer, "Name", "Alice");
var name = ReflectionHelper.GetProperty<string>(customer, "Name");

var handlers = ReflectionHelper.FindTypes(
    t => t.Name.EndsWith("Handler"),
    typeof(Customer).Assembly);
```

## Shared State And Value Helpers

### TimeProviderAccessor

`TimeProviderAccessor` gives ambient access to the current `TimeProvider`. It is useful when code needs the current time without threading a `TimeProvider` through every constructor or method.

Use it when:

- domain or helper code needs the current time
- tests need to replace time deterministically
- you want one consistent time source within an async flow

Example:

```csharp
var now = TimeProviderAccessor.Current.GetUtcNow();

TimeProviderAccessor.Current = fakeTimeProvider;
var later = TimeProviderAccessor.Current.GetUtcNow();

TimeProviderAccessor.Reset();
```

### Version

`Version` is the devkit's semantic-version helper. It can parse version strings, compare versions, and render short or full version text.

Use it when:

- you need SemVer parsing and comparison
- prerelease or build metadata values matter
- version values should stay richer than plain strings

Example:

```csharp
var current = Version.Parse("2.4.0-beta.1+build45");
var released = Version.Parse("2.3.9");

var isNewer = current > released;
var shortText = current.ToString(VersionFormat.Short);
```

### ValueList

`ValueList<T>` is a tiny immutable list optimized for very small collections. It works well when a value object or helper only needs to carry a handful of items.

Use it when:

- most cases contain zero, one, or two items
- you want simple immutable append-style usage
- a full `List<T>` would be unnecessary overhead

Example:

```csharp
var tags = default(ValueList<string>)
    .Add("important")
    .Add("internal");

foreach (var tag in tags.AsEnumerable())
{
    Console.WriteLine(tag);
}
```

### PropertyBag

`PropertyBag` stores flexible named values with typed reads and optional typed keys. It works well for metadata, context, ad-hoc attributes, and extension points.

Use it when:

- you need named values without creating a dedicated class
- callers should read values back in a typed way
- metadata needs to travel alongside a request, event, or object

Example:

```csharp
var bag = new PropertyBag();
bag.Set("tenantId", "acme");
bag.Set("retryCount", 3);

var tenantId = bag.Get<string>("tenantId");
var retryCount = bag.Get<int>("retryCount");
```

### SafeDictionary

`SafeDictionary<TKey, TValue>` behaves like a normal mutable dictionary, but missing keys return the default value instead of throwing. For string keys it is case-insensitive by default.

Use it when:

- missing keys are expected and should be harmless
- callers prefer simple indexer access
- string-key lookups should be case-insensitive

Example:

```csharp
var values = new SafeDictionary<string, int>();
values["Retries"] = 3;

var retries = values["retries"];
var missing = values["unknown"]; // returns 0
```

### Enumeration And Smart Enumeration

`Enumeration` is the devkit's smart-enum base type. It lets you model fixed values as rich types instead of plain enums, while still supporting lookup by id or value.

Use it when:

- a fixed set of options needs behavior or metadata
- ids and display values both matter
- you want stronger domain semantics than a plain `enum`

Example:

```csharp
public sealed class OrderStatus : Enumeration
{
    public static readonly OrderStatus Draft = new(1, "Draft");
    public static readonly OrderStatus Submitted = new(2, "Submitted");

    private OrderStatus(int id, string value) : base(id, value) { }
}

var status = Enumeration.FromValue<OrderStatus>("Submitted");
var allStatuses = Enumeration.GetAll<OrderStatus>();
```

## Data And Content Helpers

### Content Types

The content-type helpers define a `ContentType` model plus extension methods for:

- resolving from MIME type
- resolving from file name
- resolving from file extension
- reading metadata such as `MimeType()`, `FileExtension()`, `IsText()`, and `IsBinary()`

This is a small but practical utility family for file, document, and HTTP-oriented scenarios.

Example:

```csharp
var contentType = ContentTypeExtensions.FromFileName("report.pdf");
var mimeType = contentType.MimeType();
var isBinary = contentType.IsBinary();
```

### CompressionHelper

`CompressionHelper` compresses and decompresses:

- strings
- byte arrays
- streams

It uses GZip and supports async workflows, making it useful for payload compression, export/import scenarios, and storage pipelines.

Example:

```csharp
var compressed = await CompressionHelper.CompressAsync("hello world");
var original = await CompressionHelper.DecompressAsync(compressed);
```

### HashHelper

`HashHelper` computes hashes for:

- strings
- byte arrays
- streams
- arbitrary objects serialized to JSON

It is handy for fingerprints, change detection, cache keys, and duplicate detection.

Example:

```csharp
var hash1 = HashHelper.Compute("hello world");
var hash2 = HashHelper.Compute(new { Id = 42, Name = "Alice" });
```

### CloneHelper And CloneHelperNew

`CloneHelper` performs deep cloning through serialization-based copying. `CloneHelperNew` appears to be the newer alternative that exists alongside the original helper.

These helpers are useful when:

- a defensive deep copy is needed
- mutable graph state should be duplicated for comparison or sandboxed modification

Because cloning is serialization-based, it is best treated as a utility of convenience rather than a universal object-copy strategy.

Example:

```csharp
var snapshot = CloneHelper.Clone(order);
var snapshot2 = CloneHelperNew.Clone(order);
```

## Id And Key Helpers

The devkit also includes several lightweight generation helpers:

- `GuidGenerator`
- `IdGenerator`
- `KeyGenerator`

These are useful for creating opaque identifiers, random keys, and various short-lived generated values in code that should not hand-roll randomness or string construction repeatedly.

Example:

```csharp
var id = IdGenerator.Create();
var apiKey = KeyGenerator.Create(32);
```

## Factory Helpers

`Factory<T>` and the non-generic `Factory` provide dynamic construction helpers. These are useful in framework-style code, plugin scenarios, or places where types are resolved dynamically and you want the call site to stay terse.

Example:

```csharp
var customer = Factory<Customer>.Create(new Dictionary<string, object>
{
    ["Name"] = "Alice",
    ["Age"] = 42
});

var handler = Factory.Create(typeof(MyHandler), serviceProvider);
```

## Validation Helpers

The devkit includes `FluentValidatorExtensions`, including `AddRangeRule<T>(...)`.

This helper is designed for dynamic validator construction, especially when validation rules are assembled from reflected metadata instead of hard-coded property expressions.

Example:

```csharp
var validator = new InlineValidator<Product>();
var property = typeof(Product).GetProperty(nameof(Product.Price));

validator.AddRangeRule(property, 0m, 9999m, "Price must stay within the allowed range.");
```

## Other Helpers

Several smaller low-level helpers round out this utility set:

- `ValueStopwatch` for lightweight elapsed-time measurement
- `Retry` as a compact retry utility alongside the richer `Retryer`
- smaller clone and guid validation helpers

These are small but useful support pieces that round out the shared utility set.

Example:

```csharp
var stopwatch = ValueStopwatch.StartNew();
await Retry.On<TimeoutException>(
    () => SendAsync(),
    delays: [TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(250)]);

Console.WriteLine(stopwatch.GetElapsedMilliseconds());
```

## Related Documentation

- [Requester and Notifier](./features-requester-notifier.md)
- [StartupTasks](./features-startuptasks.md)
- [Common Observability Tracing](./common-observability-tracing.md)
- [Common Extensions](./common-extensions.md)
