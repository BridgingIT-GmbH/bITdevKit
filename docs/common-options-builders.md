# Common Options Builders

`Common.Options` provides the lightweight builder pattern that many devkit packages use for configuration objects. It is not a replacement for `Microsoft.Extensions.Options`; it is a small, reusable convention for constructing feature-specific options with fluent APIs.

This explains why many devkit packages expose configuration like:

```csharp
builder.Services.AddSomething(o => o
    .WithX(...)
    .WithY(...));
```

## Why This Pattern Exists

The devkit uses builder-based options when a package needs:

- a fluent configuration API
- simple construction without a large framework dependency
- optional logging support on the options object itself
- reusable configuration code across runtime and tests

This pattern keeps package configuration small and explicit while still being pleasant to use.

## Core Types

### `IOptionsBuilder` and `IOptionsBuilder<T>`

These interfaces define the minimal contract for builders that construct option objects. They expose the underlying target object and a `Build()` method.

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

This is useful for infrastructure options that need to create internal loggers without forcing every caller to wire that manually.

### `OptionsBuilderBase<TOption, TBuilder>`

This is the typed fluent base class for builders whose option type derives from `OptionsBase`.

Its main shared feature is:

- `LoggerFactory(ILoggerFactory loggerFactory)`

Builders in other packages often inherit from this class so they automatically support fluent logger configuration plus package-specific settings.

## Typical Usage Pattern

### Simple Builder

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

### Logger-Aware Builder

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

## Where You Will See It

This pattern appears across the devkit in package-specific builders for:

- storage providers
- messaging and broker configuration
- startup tasks
- scheduling
- authentication and test helpers
- other infrastructure services with fluent setup APIs

That shared base pattern is what keeps those APIs feeling similar even though they live in different packages.

## Relationship To `Microsoft.Extensions.Options`

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

## Design Guidance

- Keep builders small and specific to one package.
- Put shared fluent behavior into the base classes, not into every feature package.
- Use `OptionsBase` only when logger creation on the options object is genuinely useful.
- Avoid turning these builders into mini-frameworks. Their value is simplicity.

## Related Docs

- [Common Caching](./common-caching.md)
- [Common Mapping](./common-mapping.md)
- [StartupTasks](./features-startuptasks.md)
- [JobScheduling](./features-jobscheduling.md)
