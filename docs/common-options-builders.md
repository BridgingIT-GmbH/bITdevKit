<img class="bdk-doc-icon" src="assets/features/options-builders.svg" width="32" height="32" alt="" align="right">

# Common Options Builders

> Use a lightweight fluent builder convention for feature-specific configuration objects.

[TOC]

`Common.Options` provides the lightweight builder pattern that many devkit packages use for configuration objects. It is not a replacement for `Microsoft.Extensions.Options`; it is a small, reusable convention for constructing feature-specific options with fluent APIs.

This explains why many devkit packages expose configuration like:

```csharp
builder.Services.AddSomething(o => o
    .WithX(...)
    .WithY(...));
```

## Why this pattern exists

The devkit uses builder-based options when a package needs:

- a fluent configuration API
- simple construction without a large framework dependency
- optional logging support on the options object itself
- reusable configuration code across runtime and tests

This pattern keeps package configuration small and gives feature packages a consistent fluent setup API.

## Core types

### `IOptionsBuilder` and `IOptionsBuilder<T>`

`IOptionsBuilder` exposes the underlying target object. `IOptionsBuilder<T>` adds the `Build()` method that returns the configured options.

### `OptionsBuilder<T>`

`OptionsBuilder<T>` is the simplest reusable builder. It:

- creates a new `T`
- exposes it through `Target`
- returns it from `Build()`

Use this when your options type does not need special logging or additional fluent base methods.

### `OptionsBase`

`OptionsBase` is the common base class for option types that want logger creation support. It carries an optional `ILoggerFactory` and exposes:

- `CreateLogger(string categoryName)`
- `CreateLogger<T>()`

This is useful for infrastructure options that create internal loggers. Both methods return a null logger when `LoggerFactory` is not set.

### `OptionsBuilderBase<TOption, TBuilder>`

This is the typed fluent base class for builders whose option type derives from `OptionsBase`.

Its main shared feature is:

- `LoggerFactory(ILoggerFactory loggerFactory)`

Builders in other packages often inherit from this class so they automatically support fluent logger configuration plus package-specific settings.

## Typical usage pattern

### Simple builder

Use `OptionsBuilder<T>` when you just need a small builder around a plain options class:

```csharp
public class MyFeatureOptions
{
    public bool Enabled { get; set; } = true;
}

public class MyFeatureOptionsBuilder : OptionsBuilder<MyFeatureOptions>
{
    public MyFeatureOptionsBuilder Enabled(bool enabled)
    {
        this.Target.Enabled = enabled;
        return this;
    }
}
```

### Logger-aware builder

Use `OptionsBase` plus `OptionsBuilderBase<TOption, TBuilder>` when the option object should be able to create loggers:

```csharp
public class MyProviderOptions : OptionsBase
{
    public string Name { get; set; }
}

public class MyProviderOptionsBuilder
    : OptionsBuilderBase<MyProviderOptions, MyProviderOptionsBuilder>
{
    public MyProviderOptionsBuilder Name(string value)
    {
        this.Target.Name = value;
        return this;
    }
}
```

## Presence-aware composition

Use `PatchOptionsBuilder<TOptions, TSelf>` when several modules contribute option values and an omitted setting must remain distinct from an explicit `false`, `0`, `null`, or empty collection. The builder records assignments in an `OptionsPatch`; it does not create a target object or choose which source wins.

Feature builders can expose short, specific methods while sharing assignment tracking:

```csharp
using BridgingIT.DevKit.Common;

var provenance = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
var builder = new ReportOptionsBuilder().Enabled(false).Attempts(0);

// Apply code assignments first, then let supplied configuration override them.
var options = builder.Patch.Apply(new ReportOptions(), "Module", provenance);
options = OptionsConfigurationOverlay.Apply(options, configuration.GetSection("Reports"), provenance);

public sealed class ReportOptions
{
    public bool Enabled { get; set; } = true;

    public int Attempts { get; set; } = 3;

    public List<string> Formats { get; set; } = ["json", "csv"];
}

public sealed class ReportOptionsBuilder : PatchOptionsBuilder<ReportOptions, ReportOptionsBuilder>
{
    public ReportOptionsBuilder Enabled(bool enabled = true) => this.With(options => options.Enabled, enabled);

    public ReportOptionsBuilder Attempts(int attempts) => this.With(options => options.Attempts, attempts);
}
```

The feature chooses the application order. In this example, configuration wins because its overlay runs last. Within the supplied configuration tree, normal `IConfiguration` provider precedence still applies. Neither helper opens configuration files or subscribes to reload.

For example, configuration supplying `Reports:Attempts = 2` and `Reports:Formats:0 = json` produces these values:

| Property | Default | Module assignment | Configuration assignment | Effective value | Provenance |
| --- | --- | --- | --- | --- | --- |
| `Enabled` | `true` | `false` | Omitted | `false` | `Module` |
| `Attempts` | `3` | `0` | `2` | `2` | `Configuration` |
| `Formats` | `["json", "csv"]` | Omitted | `["json"]` | `["json"]` | `Configuration` |

Apply each module's patch to the same target in the feature's chosen order. A module that omits a property leaves the previous assignment in place. An explicit assignment replaces it. Provenance records applied paths; defaults do not receive a source label automatically.

### Patch operations

`OptionsPatch` stores property paths case-insensitively. `Set` snapshots a value when it is recorded, so later changes to the caller's collection do not change that assignment. Reassigning the same exact path replaces its earlier value.

- `Contains(path)` distinguishes an explicit assignment from omission.
- `Merge(prefix, child)` copies a child patch under a nested property path. Use a null or empty prefix to merge at the root.
- `Extract(prefix)` copies only descendants of a property path and removes that prefix.
- `AppendAlias(path, alias)` appends a string alias once, ignoring case and preserving order.
- `Apply(options, source, provenance)` mutates the supplied target and records the source label for each exact assigned path. Nested parent objects must already be initialized.

`With` accepts a direct property path such as `options => options.Retry.Attempts`. Computed expressions and indexers are rejected. Feature-specific validation still belongs to the feature.

### Configuration overlays

`OptionsConfigurationOverlay.Apply` snapshots the supplied option data before applying configuration. Absent properties keep their prior values. Nested objects merge recursively. Supplied collections replace the complete prior collection rather than merging list positions or dictionary keys.

Arrays, `List<T>`, standard list interfaces, `HashSet<T>`, and dictionary shapes supported by both JSON serialization and the configuration binder can be used. String-key dictionaries are suitable for named settings. A present collection section with no bound value becomes an empty collection. An absent section keeps its default. Providers differ in how they represent empty sections; use a provider that retains that section when clearing a collection through configuration.

Only public, readable, writable properties are accepted. Unknown settings and indexers are rejected. Applied configuration paths receive the provenance label `Configuration`; omitted paths do not receive a new label.

### Serializable option data

Patches and overlays use default `System.Text.Json` serialization to copy data. Use plain option classes or records with supported property types. They do not preserve shared references or runtime object identity, and do not accept custom serializer settings. Services, delegates, runtime type registrations, and objects requiring custom converters belong in separate code-owned registrations.

The patch builder is an additional option for features that need presence tracking. Existing `OptionsBuilder<T>` and `OptionsBuilderBase<TOption, TBuilder>` continue to construct their targets directly.

## Where you will see it

Presence-aware patches compose settings in these features:

| Feature | Omission and explicit assignments |
| --- | --- |
| [Application Caching](features-application-caching.md) | Module assignments and configuration overlays compose before the effective catalog is finalized. |
| [Profiling](features-profiling.md) | Provider retention defaults apply only to operation settings that were never explicitly assigned. |
| [Presentation Streaming](features-presentation-streaming.md) | Nullable NDJSON overrides inherit defaults; explicit false values and clear flags override them. |

Each feature defines what counts as a supplied value. Runtime objects such as serializer settings stay outside JSON value patches. A builder that already edits one live options instance directly does not need a patch layer just to preserve false or zero.

This pattern appears across the devkit in package-specific builders for:

- storage providers
- messaging and broker configuration
- startup tasks
- scheduling
- authentication and test helpers
- other infrastructure services with fluent setup APIs

That shared base pattern is what keeps those APIs feeling similar even though they live in different packages.

## Relationship to `Microsoft.Extensions.Options`

This package solves a different problem than the built-in options stack.

Use the builder pattern when:

- you want a fluent API for composing a configuration object
- the options object is built once as part of registration
- the package does not need runtime reloading or named options

Use `Microsoft.Extensions.Options` when:

- you need standard configuration binding across the app
- you need `IOptions<T>`, `IOptionsSnapshot<T>`, or `IOptionsMonitor<T>`
- you want reloadable configuration from files or providers

The two approaches can coexist. The devkit builder pattern is often used to create or refine options during registration, while configuration binding may still happen elsewhere.

## Design guidance

- Keep builders small and specific to one package.
- Put shared fluent behavior into the base classes, not into every feature package.
- Use `OptionsBase` only when logger creation on the options object is genuinely useful.
- Keep these builders limited to constructing options.

## Related docs

- [Application Caching](./features-application-caching.md) uses explicit patches for module contributions and records effective configuration sources.
- [Common Caching](./common-caching.md)
- [Common Mapping](./common-mapping.md)
- [StartupTasks](./features-startuptasks.md)
- [JobScheduling](./features-jobscheduling.md)