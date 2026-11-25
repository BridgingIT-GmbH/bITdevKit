<img class="bdk-doc-icon" src="assets/features/common-extensions.svg" width="32" height="32" alt="" align="right">

# Common Extensions

> Reuse shared extensions for collections, asynchronous flows, date and time values, configuration, and reflection.

[TOC]

`Common.Abstractions` contains a broad extensions layer under `src/Common.Abstractions/Extensions`. This is the shared utility layer that many other devkit packages build on.

This page is intentionally an overview, not an API catalog. The goal is to show what kinds of extensions are available and where to look when you need them.

## How the extensions are organized

The folder contains two styles of extension classes:

- focused classes with explicit names such as `DateTimeExtensions`, `StringExtensions`, or `ConditionalLinqExtensions`
- many small helper files that contribute methods to the partial static `Extensions` class

That means the package is best understood by capability area rather than by a single class name.

## Main categories

## Collection and sequence helpers

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

## Fluent optional and null-safe composition

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

## Text pattern matching

`GlobPatternMatcher` prepares a whole-text pattern once for repeated string or span matching. `*` matches zero or more Unicode scalars, and `?` matches one scalar, including a supplementary character represented by two UTF-16 code units. Both wildcards include line breaks. Backslash escapes only `*`, `?`, or backslash itself. Literals use invariant case folding by default; pass `ignoreCase: false` for exact case.

```csharp
using BridgingIT.DevKit.Common;

var matcher = new GlobPatternMatcher(@"report:\?*");
var matches = matcher.IsMatch("REPORT:?42"); // true
var singleScalar = new GlobPatternMatcher("?").IsMatch("😀"); // true
```

The matcher has no cache-key or path policy. An empty pattern matches empty text. Patterns must contain valid UTF-16; matching rejects invalid UTF-16 when it is encountered. Each instance retains state proportional to the prepared pattern and can be shared between callers. Apply input and pattern limits at the consuming feature's boundary. Application Caching adds its nonempty, UTF-8 byte, and identifier checks before using the shared matcher.

The existing `Match` and `MatchAny` extensions provide a simpler contract. Only `*` is a wildcard; `?` and backslash are literal characters. Wildcards match both carriage returns and line feeds. Ignore-case comparisons follow the current culture, and the regex end-anchor convention permits a final line feed after an otherwise exact match. Use `GlobPatternMatcher` when whole-text anchoring and portable scalar semantics are required.

```csharp
var multiline = "first\nsecond".Match("first*second"); // true
var literalQuestionMark = "report?".Match("report?"); // true
```

### Match anchored path globs

`PathGlobMatcher(IReadOnlyList<string> patterns)` is in the `BridgingIT.DevKit.Common.Abstractions` package and the `BridgingIT.DevKit.Common` namespace. Call `IsMatch(string path)` to check whether any configured pattern matches the complete slash-rooted path.

```csharp
using BridgingIT.DevKit.Common;

var matcher = new PathGlobMatcher(["/reports/**", "/jobs/archive-*"]);
var report = matcher.IsMatch("/REPORTS/monthly/sales/"); // true
var archive = matcher.IsMatch("/jobs/archive-42"); // true
var nested = matcher.IsMatch("/jobs/archive-42/run"); // false
```

`*` matches zero or more code units in one segment. `**` matches zero or more segments and must occupy a complete segment. Trailing slashes are ignored, and interior empty segments are significant. Null and empty paths represent the root. A nonempty path without a leading slash returns `false`.

The constructor accepts up to 128 patterns, at most 256 UTF-16 code units each. An empty list matches nothing. A null list throws `ArgumentNullException`. Invalid patterns or exceeded limits throw `ArgumentException` during construction. Patterns must begin with `/` and exclude question marks, hash signs, backslashes, control characters, braces, brackets, and parentheses.

Path literals compare case-insensitively using invariant casing on individual UTF-16 code units. This keeps exact unpaired-surrogate literals and applies casing to BMP letters. Supplementary letters match their exact code units. Use `GlobPatternMatcher` for whole-text matching with Unicode-scalar casing and `?`. The path matcher reuses it for ASCII segments.

Request Profiling uses this matcher through `RequestProfilingPathMatcher` for its blacklist and whitelist. Its facade retains the original behavior for nonempty inputs without a leading slash by skipping the first UTF-16 code unit. Canonical profiling keys are still created by the feature. [Request Profiling setup](./features-profiling.md#operation-and-request-setup)

## Date and time helpers

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

### Parse durations

`TryParseDuration(this string source, out TimeSpan duration)` is in the `BridgingIT.DevKit.Common.Abstractions` package and the `BridgingIT.DevKit.Common` namespace. It first tries invariant `TimeSpan` text, such as `00:01:30`. It also accepts numbers with case-insensitive suffixes `ms`, `s`, `m`, and `h`. A number can include a sign, a fraction, or an exponent. Use a period for the decimal separator in every culture. Blank text, unsupported units, non-finite numbers, and overflow return `false` with `TimeSpan.Zero`.

```csharp
using BridgingIT.DevKit.Common;

if ("1.5m".TryParseDuration(out var timeout) && timeout > TimeSpan.Zero)
{
    Console.WriteLine(timeout.TotalSeconds); // 90
}
```

Check any minimum, maximum, or positivity requirement after parsing. Use `TryParseTime` for clock text such as `02:30 PM`. Profiling's `ProfilingDurationParser.TryParse` uses the Common duration parser. [Profiling console commands](./features-profiling.md#console-commands)

## Text, enum, and argument helpers

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
- combining object argument arrays

### Truncate text to a UTF-8 byte limit

`TruncateUtf8(this string source, int maximumBytes)` is in the `BridgingIT.DevKit.Common.Abstractions` package and the `BridgingIT.DevKit.Common` namespace. Use it when a field is limited by encoded size. It returns the longest prefix of complete Unicode scalars that fits, without adding a marker. For example, `é` takes two UTF-8 bytes and `😀` takes four. Use `Truncate` for character-count limits.

```csharp
using BridgingIT.DevKit.Common;

var label = "Café 😀".TruncateUtf8(8); // "Café "
```

Input that fits is returned as supplied, including null and empty strings. A zero limit returns an empty string for non-null input. A negative limit throws `ArgumentOutOfRangeException`. Byte counting treats each unpaired UTF-16 surrogate as the replacement character U+FFFD. If truncation is needed, retained unpaired surrogates are returned as U+FFFD. Auditing's `AuditCollector.Text` still returns an empty string for non-null input with a negative limit. [Application Auditing](./features-application-auditing.md)

When the UTF-16 length already exceeds the byte limit, truncation skips the full byte count and stops at the first scalar that cannot fit.

## Stream, task, and async helpers

These support lower-level workflow code where streams, tasks, and async pipelines are common.

Representative files:

- `StreamExtensions.cs`
- `TaskExtensions.cs`
- `AsyncEnumerableExtensions.cs`

Typical use cases:

- convenience helpers for stream handling
- async sequence querying and projection
- task composition helpers used by infrastructure code

## Reflection, type, and expression helpers

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

## Configuration and DI helpers

These extensions help with configuration access and service-collection inspection.

Representative files:

- `ConfExtensions.cs`
- `ServiceCollectionExtensions.cs`

Typical use cases:

- reading configuration sections while replacing placeholder values such as `{{Some:Key}}`
- checking whether a service has already been added to `IServiceCollection`
- finding or locating a registered service descriptor during startup composition

## HTTP helpers

### Evaluate representation conditions and byte ranges

`HttpRepresentationHeaders` is in the `BridgingIT.DevKit.Common.Extensions.Web` package and the `BridgingIT.DevKit.Common` namespace. It uses ASP.NET Core's `HttpRequest` and requires no service registration. Supply the selected representation's existence, strong quoted ETag, and last modification time.

- `Evaluate(HttpRequest request, bool exists, string entityTag, DateTimeOffset lastModified, bool read)` returns `HttpRepresentationPreconditionOutcome`. `If-Match` takes precedence over `If-Unmodified-Since`. If that check succeeds, `If-None-Match` takes precedence over `If-Modified-Since`. A standalone ETag wildcard checks existence. Date preconditions compare timestamps at whole-second precision. `If-Modified-Since` applies only to reads.
- `MatchesEntityTag(string values, string current, bool weak = false)` parses a list of quoted tags and compares their opaque values ordinally, preserving commas inside tags. Set `weak: true` to permit candidates with an uppercase `W/` prefix. Invalid lists and wildcard candidates return `false`. `Evaluate` handles standalone wildcard conditions.
- `SelectRange(HttpRequest request, long length, string entityTag, DateTimeOffset lastModified)` returns an `HttpRepresentationRangeSelection` with an `Outcome` of type `HttpRepresentationRangeKind`. An absent range or an invalid or mismatching `If-Range` returns `Full`. `If-Range` accepts one matching strong ETag or a date equal to the representation's last modification time at whole-second precision. Use an `If-Range` date only when the representation's last modification time is a strong validator. The `bytes` unit is case insensitive. One range can return `Partial` with a zero-based `Offset` and a byte `Length`. Malformed ranges, multiple ranges, and other units return `Unsupported`. A valid range that selects no bytes returns `Unsatisfiable`. Every outcome other than `Partial` has an offset of zero and a null length. Negative representation lengths throw `ArgumentOutOfRangeException`.

```csharp
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;

var request = new DefaultHttpContext().Request;
request.Headers.Range = "bytes=-5";
var entityTag = "\"report-v1\"";
var lastModified = new DateTimeOffset(2026, 2, 5, 10, 30, 20, TimeSpan.Zero);

var condition = HttpRepresentationHeaders.Evaluate(request, true, entityTag, lastModified, read: true);
if (condition == HttpRepresentationPreconditionOutcome.Proceed)
{
    var selection = HttpRepresentationHeaders.SelectRange(request, 20, entityTag, lastModified);
    Console.WriteLine(selection); // Partial, offset 15, length 5
}
```

Use `NotModified` for a conditional read that can return HTTP 304 and `Failed` for a precondition failure that can return HTTP 412. Your endpoint selects the representation, authorizes access, applies mutation conditions atomically, and writes the response.

The overloads above use `HttpRepresentationHeaderMode.Strict`, which accepts all three HTTP-date formats. The existing Presentation facade selects `HttpRepresentationHeaderMode.LegacyPresentation`. That mode retains raw comma splitting, RFC 1123 date parsing, matching `If-Range` dates at least as recent as the representation, and case-sensitive `bytes` matching. [HTTP date formats](https://www.rfc-editor.org/rfc/rfc9110.html#section-5.6.7), [Application Caching](./features-application-caching.md#apply-http-conditions)

If your timestamp cannot reliably identify a representation, pass `allowIfRangeDate: false`. A date in `If-Range` then selects the full response, and a matching strong ETag can still select a partial response. Requests without `If-Range` use normal range selection.

```csharp
var etagRange = HttpRepresentationHeaders.SelectRange(
    request, 20, entityTag, lastModified,
    mode: HttpRepresentationHeaderMode.Strict, allowIfRangeDate: false);
```

### Transport opaque version tokens in ETags

`OpaqueVersionETag` is in the `BridgingIT.DevKit.Common.Extensions.Web` package, under the `BridgingIT.DevKit.Common` namespace. Use it in ASP.NET Core endpoints that exchange opaque versions with a provider. `Write` sets one strong ETag. It wraps the UTF-8 version in quotes with a `bdk.` prefix and canonical unpadded Base64Url. For example, the version `"v1"` including its quotes becomes `"bdk.InYxIg"`. A blank version leaves the response header as it is.

```csharp
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;

var response = new DefaultHttpContext().Response;
OpaqueVersionETag.Write(response, "\"v1\"");
var condition = OpaqueVersionETag.ReadCondition(response.Headers.ETag, bodyETag: "\"v1\"");
if (condition.IsSuccess)
{
    Console.WriteLine(condition.Value); // "v1"
}
```

For an exact round-trip, use a nonblank provider version with well-formed UTF-16 and no control characters. UTF-8 encoding replaces unpaired surrogates. `ReadCondition` rejects decoded versions that contain control characters or become `*` after trimming surrounding double quotes.

`ReadCondition` preserves quotes and spaces in the decoded version. It also accepts raw versions, trimming surrounding whitespace and retaining the supplied quoting. Wildcards (`*` or `"*"`), header lists, and malformed transport tags produce a `ValidationError`. When both header and body supply a version, their decoded values must match ordinally. When both are missing or blank, the result is successful with a `null` value. The provider applies the version condition to the mutation.

## Practical guidance

- Use these helpers to reduce boilerplate, not to hide business logic.
- Prefer the narrowly named extension classes when you are browsing for behavior such as date, string, or configuration helpers.
- Expect many small general-purpose methods to live in the partial `Extensions` class.
- For the fluent null-safe and query-composition style, read [Extensions](./features-extensions.md) for the conceptual usage patterns and this page for the package layout.

## What this page does not cover

This page does not list every overload or method. The extensions folder is too broad for that to stay useful. Treat this page as a map of the available families of helpers, then go to the specific file when you need exact behavior.

## Related docs

- [Extensions](./features-extensions.md)
- [Common Infrastructure](./INDEX.md)
- [Common Options Builders](./common-options-builders.md)
- [Common Serialization](./common-serialization.md)
