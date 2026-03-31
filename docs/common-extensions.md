# Common Extensions

> Reuse a broad set of shared helper extensions for composition, collections, async flows, and more.

`Common.Abstractions` contains a broad extensions layer under [Extensions](/f:/projects/bit/bITdevKit/src/Common.Abstractions/Extensions). This is the shared utility surface that many other devkit packages build on.

This page is intentionally an overview, not an API catalog. The goal is to show what kinds of extensions are available and where to look when you need them.

## How The Extensions Are Organized

The folder contains two styles of extension classes:

- focused classes with explicit names such as `DateTimeExtensions`, `StringExtensions`, or `ConditionalLinqExtensions`
- many small helper files that contribute methods to the partial static `Extensions` class

That means the package is best understood by capability area rather than by a single class name.

## Main Categories

## Collection And Sequence Helpers

These extensions help with shaping, slicing, filtering, and traversing in-memory collections.

Representative files:

- `ConditionalCollectionExtensions.cs`
- `ConditionalLinqExtensions.cs`
- `EnumerableExtensions.cs`
- `ListExtensions.cs`
- `DictionaryExtensions.cs`
- `AsyncEnumerableExtensions.cs`
- `ToHierarchy.cs`
- `Batch.cs`
- `Partition.cs`
- `Slice.cs`
- `SliceFrom.cs`
- `SliceTill.cs`
- `DistinctIf.cs`
- `Merge.cs`
- `ForEach.cs`
- `ParallelForeach.cs`

Typical use cases:

- conditional `Where`, `Select`, and ordering logic
- batching and partitioning large in-memory collections
- building tree structures from flat data
- working with `IAsyncEnumerable<T>` without pulling in another utility package

## Fluent Optional And Null-Safe Composition

This area overlaps with the higher-level extension docs in [Extensions](./features-extensions.md), but the code lives here in `Common.Abstractions`.

Representative files:

- `LinqFluentExtensions.cs`
- `Match.cs`
- `To.cs`
- `ToType.cs`
- `SafeNull.cs`
- `SafeAny.cs`
- `SafeWhere.cs`
- `SafeEquals.cs`
- `SafeRemove.cs`
- `EmptyToNull.cs`
- `ExcludeNull.cs`
- `IsNullOrEmpty.cs`

Typical use cases:

- fluent chaining around nullable or optional values
- safe collection access when inputs may be null
- lightweight conversions and pattern-style branching
- avoiding repetitive guard code in application and infrastructure layers

If you want the conceptual guidance for these fluent patterns, start with [Extensions](./features-extensions.md). This page is the package-level map of where those helpers live.

## Date And Time Helpers

These extensions provide common date and time calculations and parsing helpers.

Representative files:

- `DateTimeExtensions.cs`
- `DateTimeOffsetExtensions.cs`
- `DateOnlyExtensions.cs`
- `TimeOnlyExtensions.cs`
- `TimeSpanExtensions.cs`

Typical use cases:

- start/end of day, week, month, or year
- relative date calculations
- parsing dates from multiple string formats
- range checks and date/time convenience operations

## Text, Enum, And Argument Helpers

These extensions focus on small but frequently used text and enum operations.

Representative files:

- `StringExtensions.cs`
- `StartsWithAny.cs`
- `ContainsAny.cs`
- `Truncate.cs`
- `EnumExtensions.cs`
- `ConcatArgs.cs`
- `ToString.cs`

Typical use cases:

- string cleanup and matching
- working with multiple prefixes or tokens
- enum metadata and helper logic
- building shell or command arguments safely

## Stream, Task, And Async Helpers

These support lower-level workflow code where streams, tasks, and async pipelines are common.

Representative files:

- `StreamExtensions.cs`
- `TaskExtensions.cs`
- `AsyncEnumerableExtensions.cs`

Typical use cases:

- convenience helpers for stream handling
- async sequence querying and projection
- task composition helpers used by infrastructure code

## Reflection, Type, And Expression Helpers

These are support extensions for infrastructure code, configuration, and dynamic behavior.

Representative files:

- `TypeExtensions.cs`
- `AssemblyExtensions.cs`
- `ExpressionExtensions.cs`
- `ExceptionExtensions.cs`

Typical use cases:

- finding types during registration or assembly scanning
- working with expression trees
- extracting useful exception information
- reflection-heavy infrastructure code

## Configuration And DI Helpers

These extensions help with configuration access and service-collection inspection.

Representative files:

- `ConfExtensions.cs`
- `ServiceCollectionExtensions.cs`

Typical use cases:

- reading configuration sections while replacing placeholder values such as `{{Some:Key}}`
- checking whether a service has already been added to `IServiceCollection`
- finding or locating a registered service descriptor during startup composition

## Practical Guidance

- Use these helpers to reduce boilerplate, not to hide business logic.
- Prefer the narrowly named extension classes when you are browsing for behavior such as date, string, or configuration helpers.
- Expect many small general-purpose methods to live in the partial `Extensions` class.
- For the fluent null-safe and query-composition style, read [Extensions](./features-extensions.md) for the conceptual usage patterns and this page for the package layout.

## What This Page Does Not Cover

This page does not list every overload or method. The extensions folder is too broad for that to stay useful. Treat this page as a map of the available families of helpers, then go to the specific file when you need exact behavior.

## Related Docs

- [Extensions](./features-extensions.md)
- [Common Infrastructure](./INDEX.md)
- [Common Options Builders](./common-options-builders.md)
- [Common Serialization](./common-serialization.md)
