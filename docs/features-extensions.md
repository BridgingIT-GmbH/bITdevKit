# LINQ Extensions

> Apply null-aware value operations and cancellation-aware asynchronous sequence operators.

[TOC]

## Overview

The LINQ extensions provide focused operations for nullable values, fluent synchronous/asynchronous value chains, and `IAsyncEnumerable<T>` sequences. They live in `Common.Abstractions` under the `BridgingIT.DevKit.Common` namespace.

This page focuses on the LINQ-oriented extension families. For a broader package-level overview of the extension helpers available from `Common.Abstractions`, see [Common Extensions](./common-extensions.md).

## Challenges

Application code often mixes optional values, conditional transformations, asynchronous work, and streamed sequences. Repeated null branches and manual `await foreach` loops obscure the operation being performed. Standard LINQ also does not cover every project-specific null-safe or asynchronous-sequence convention.

These helpers must be used with care: not every fluent delegate can be translated by an `IQueryable<T>` provider, and some operators enumerate a source completely or keep state proportional to the number of distinct items.

## Solution

`LinqFluentExtensions` adds null-aware lookup, branching, transformation, side-effect, validation, matching, and fallback methods for values and tasks. `AsyncEnumerableExtensions` adds cancellation-aware querying and lazy transformation for `IAsyncEnumerable<T>`.

The extensions return the original value, a transformed value, a task, or another asynchronous sequence according to the operation. Callers choose when to materialize or otherwise consume a lazy sequence.

## Key Features

- Null-safe `Find`, `WhenNotNull`, `WhenNull`, `Match`, and `OrElse` operations.
- Conditional value transformations through `When` and `Unless`.
- Sync-to-async and task-to-async composition through `SelectAsync`, `DoAsync`, and related overloads.
- Explicit validation through `Throw` and `ThrowWhen`.
- Cancellation-aware query, filter, projection, partition, and deduplication for asynchronous sequences.
- Lazy async-sequence operators except for terminal operations such as `CountAsync`, `FirstAsync`, and `LastAsync`.

## Architecture

Both extension families are static classes in `Common.Abstractions`. `LinqFluentExtensions` operates on in-memory values, `IEnumerable<T>`, nullable structs, and `Task<T>`. `AsyncEnumerableExtensions` consumes `IAsyncEnumerable<T>` with `WithCancellation(...)` and returns either a `ValueTask<T>` terminal result or a lazy `IAsyncEnumerable<T>` pipeline.

They are in-memory operators. Calling them after `AsAsyncEnumerable()` moves subsequent work out of a database query provider.

## Use Cases

- Find an optional item and handle the present and absent branches.
- Apply a transformation only when a predicate matches.
- Add logging or auditing without changing the value flowing through a chain.
- Validate a value and throw an application-specific exception.
- Filter, page, concatenate, or deduplicate streamed results.
- Stop asynchronous enumeration through a propagated cancellation token.

## Basic Usage

Reference `Common.Abstractions`, import the namespace, and handle both lookup outcomes explicitly:

```csharp
using BridgingIT.DevKit.Common;

var message = users
    .Find(user => user.IsActive)
    .Match(
        some: user => $"Active user: {user.Name}",
        none: () => "No active user found");

Console.WriteLine(message);
```

For an active user named Ada, the visible result is `Active user: Ada`. `Find` returns `null` when the source, predicate, or matching item is absent, and `Match` selects the `none` branch in that case.

## Fluent extensions

The LINQ fluent extensions handle null values, conditional operations, and task/value composition. They complement standard LINQ with operations for optional values and fluent asynchronous transitions.

### Overview

#### Key benefits

1. **Null-aware chaining**: Run actions or select branches according to a value's null state.
2. **Functional composition**: Chain related operations through a fluent interface.
3. **Async/sync composition**: Continue from a `Task<T>` with synchronous or asynchronous work.
4. **Explicit side effects**: Keep logging and other non-transforming operations visible through `Do`.
5. **Reference and value-type support**: Use dedicated overloads for reference values and nullable structs.

#### Architecture

The extensions are organized into logical groups:

- **Find operations**: Null-returning lookup for reference types and nullable lookup for value types
- **Null Handling**: Conditional execution based on null state
- **String Checks**: Specialized null/empty validation for strings
- **Conditional Logic**: When/Unless for predicate-based operations
- **Transformations**: Select/Map for value transformations
- **Side Effects**: Do for logging and non-transforming operations (Tap)
- **Error Handling**: Throw/ThrowWhen for validation
- **Pattern Matching**: Match for both-case handling
- **Fallback Values**: OrElse for default factories

### Common usage patterns

#### Basic null checking

Replace traditional null checks with fluent null handling:

```csharp
// Traditional approach
var user = users.FirstOrDefault(u => u.IsActive);
if (user != null)
{
    await emailService.SendAsync(user.Email);
}

// Using extensions
await users
    .Find(u => u.IsActive)
    .WhenNotNullAsync(async u => await emailService.SendAsync(u.Email), cancellationToken);
```

#### Conditional LINQ chains

Apply filters and transformations conditionally:

```csharp
// Traditional approach
var query = orders.AsQueryable();
if (!string.IsNullOrEmpty(searchTerm))
    query = query.Where(o => o.Description.Contains(searchTerm));
if (minPrice.HasValue)
    query = query.Where(o => o.Total >= minPrice.Value);

var results = await query.ToListAsync();

// Using the single-branch When overload
var results = await orders
    .When(_ => !string.IsNullOrEmpty(searchTerm),
        q => q.Where(o => o.Description.Contains(searchTerm)))
    .When(_ => minPrice.HasValue,
        q => q.Where(o => o.Total >= minPrice.Value))
    .ToListAsync();

// Or with Unless for inverted conditions
var results = await orders
    .Unless(_ => string.IsNullOrEmpty(searchTerm),
        q => q.Where(o => o.Description.Contains(searchTerm)))
    .Unless(_ => !minPrice.HasValue,
        q => q.Where(o => o.Total >= minPrice.Value))
    .ToListAsync();
```

#### Validation and error handling

Chain validations with proper error propagation:

```csharp
// Traditional approach
var product = products.FirstOrDefault(p => p.Id == id);
if (product == null)
    throw new ProductNotFoundException($"Product {id} not found");

if (product.Stock == 0)
    throw new OutOfStockException();

// Using extensions
var product = products
    .Find(p => p.Id == id)
    .Throw(() => new ProductNotFoundException($"Product {id} not found"))
    .ThrowWhen(p => p.Stock == 0, p => new OutOfStockException());
```

#### Async/sync mixing

Seamlessly transition between async and sync operations:

```csharp
// Load async, then process sync, then transform async
var result = await users
    .FindAsync(async (u, ct) => await IsActiveAsync(u, ct), cancellationToken) // Async find
    .Select(u => u.Profile)                                  // Sync select
    .SelectAsync(async p => await enrichService.EnrichAsync(p), cancellationToken)  // Async select
    .DoAsync(p =>
    {
        logger.LogInformation("Processed: {Name}", p.Name);
        return Task.CompletedTask;
    }, cancellationToken)
    .DoAsync(async p => await cache.StoreAsync(p), cancellationToken);  // Async side effect
```

### Extension reference

#### Find operations

**Find** fluent alternatives to `FirstOrDefault`:

```csharp
// Find first matching element
var user = users.Find(u => u.IsAdmin);

// Find first element that satisfies an always-true predicate
var first = orders.Find(_ => true);

// Async find with async predicate
var product = await products.FindAsync(
    async (p, ct) => await IsInStockAsync(p, ct),
    cancellationToken);
```

#### Null handling

**WhenNotNull/WhenNull** execute operations based on null state:

```csharp
// Execute side effect if not null
await user
    .WhenNotNullAsync(async u => await LogUserAccessAsync(u.Id), cancellationToken);

// Execute if null (alternative path)
await user
    .WhenNullAsync(async ct => await CreateDefaultUserAsync(ct), cancellationToken);
```

#### String checks

**String-specific checks** for empty/whitespace:

```csharp
// Check for empty string
email.WhenNotNullOrEmpty(e => SendEmail(e));

// Check for whitespace
searchTerm.WhenNotNullOrWhiteSpaceAsync(
    async (term, ct) => await SearchAsync(term, ct),
    cancellationToken);

// Alternative paths
input
    .WhenNotNullOrWhiteSpace(ProcessInput)
    .WhenNullOrWhiteSpace(() => UseDefaultValue());
```

#### Conditional logic

**When** applies operations based on predicates. Use the single-branch overload when you only want to transform if the condition is true:

```csharp
// Single-branch When - only applies transformation when condition is true
var filtered = items
    .When(items => items.Any(),
        i => i.Where(x => x.IsActive));

// Both-branch When - choose between two transformations
var filtered = items
    .When(items => items.Any(),
        i => i.Where(x => x.IsActive),           // then
        i => Enumerable.Empty<Item>());          // else

// Practical example - filtering on conditions
var results = orders
    .When(_ => !string.IsNullOrEmpty(searchTerm),
        q => q.Where(o => o.Description.Contains(searchTerm)))
    .When(_ => minPrice.HasValue,
        q => q.Where(o => o.Total >= minPrice.Value))
    .ToListAsync();

// Conditional async
await order
    .WhenAsync(
        async (o, ct) => await IsHighValueAsync(o, ct),
        async (o, ct) => await ApplyPremiumBenefitAsync(o, ct),
        cancellationToken);
```

**Unless** provides clearer negation when the "then" action applies to the false case:

```csharp
// Unless - clearer when negating conditions
var result = users
    .Unless(u => u.IsDeleted, u => ProcessUser(u));

// Practical example - skip filters on exclusion conditions
var results = orders
    .Unless(_ => string.IsNullOrEmpty(searchTerm),
        q => q.Where(o => o.Description.Contains(searchTerm)))
    .Unless(_ => !minPrice.HasValue,
        q => q.Where(o => o.Total >= minPrice.Value))
    .ToListAsync();
```

#### Transformations

`Select` continues a `Task<T>` with a synchronous transformation. `SelectAsync` applies an asynchronous transformation to a value or task:

```csharp
// Synchronous transformation after a task
var profile = await userService
    .GetUserAsync(userId, cancellationToken)
    .Select(user => user.Profile, cancellationToken);

// Async transformation
var enriched = await user
    .SelectAsync(async (u, ct) => await LoadProfileAsync(u, ct), cancellationToken);

// Bridge sync to async
var result = await users
    .FindAsync(async (u, ct) => await IsActiveAsync(u, ct), cancellationToken)
    .Select(u => u.Profile)                    // Sync after async
    .SelectAsync(async p => await EnrichAsync(p), cancellationToken);
```

#### Side effects

**Do** execute operations without changing the value (Tap):

```csharp
// Log without changing value
var user = repository
    .Find(u => u.Id == id)
    .Do(u => logger.LogInformation("Found: {Name}", u.Name))
    .Do(u => auditService.Log(u.Id));

// Async side effects
await order
    .DoAsync(async (o, ct) => await cache.StoreAsync(o, ct), cancellationToken)
    .DoAsync(async (o, ct) => await analytics.TrackAsync(o.Id, ct), cancellationToken);
```

#### Error handling

**Throw/ThrowWhen** validate and throw conditionally:

```csharp
// Throw if null
var product = products
    .Find(p => p.Id == id)
    .Throw(() => new NotFoundException("Product not found"));

// Throw if condition true
var order = orders
    .Find(o => o.Id == id)
    .ThrowWhen(o => o.IsDeleted, o => new InvalidOperationException("Order deleted"));

// Async validation
await user
    .ThrowWhenAsync(
        async (u, ct) => await IsBlockedAsync(u, ct),
        async (u, ct) => new UnauthorizedAccessException($"User {u.Id} blocked"),
        cancellationToken);
```

#### Pattern matching

**Match** handle both success and failure cases:

```csharp
// Sync pattern matching
var message = user.Match(
    some: u => $"Hello, {u.Name}",
    none: () => "User not found");

// Async pattern matching
var result = await order
    .MatchAsync(
        some: async (o, ct) => await ProcessOrderAsync(o, ct),
        none: async ct => await LogNotFoundAsync(ct),
        cancellationToken);
```

#### Fallback values

**OrElse** provide default factories:

```csharp
// Simple fallback
var user = cachedUser
    .OrElse(() => repository.FindById(userId));

// Async fallback
var config = await cachedConfig
    .OrElseAsync(
        async ct => await configService.LoadAsync(ct),
        cancellationToken);
```

### Common scenarios

#### API request processing

```csharp
app.MapGet("/api/users/{id}", async Task<Microsoft.AspNetCore.Http.IResult>
    (int id, IUserRepository repository, ILogger<Program> logger, CancellationToken ct) =>
{
    return await repository
        .FindAsync(u => u.Id == id, ct)
        .DoAsync(async u => await logger.LogAccessAsync(u.Id, ct), ct)
        .MatchAsync(
            some: (user, _) => Task.FromResult<Microsoft.AspNetCore.Http.IResult>(TypedResults.Ok(user)),
            none: _ => Task.FromResult<Microsoft.AspNetCore.Http.IResult>(TypedResults.NotFound()),
            cancellationToken: ct);
});
```

#### Data validation pipeline

```csharp
var validatedData = await inputData
    .When(data => !string.IsNullOrEmpty(data.Email),
        d => NormalizeEmail(d))
    .SelectAsync(async d => await ValidateAsync(d, ct), ct)
    .ThrowWhenAsync(
        async (d, c) => !(await IsUniqueAsync(d, c)),
        (d, _) => Task.FromResult<Exception>(
            new ValidationException("Email already exists")),
        ct);
```

#### Conditional query building

```csharp
var results = await orders
    .When(_ => filterCriteria.HasCategory,
        q => q.Where(o => o.Category == filterCriteria.Category))
    .When(_ => !filterCriteria.IncludeArchived,
        q => q.Where(o => !o.IsArchived))
    .When(_ => filterCriteria.MinPrice.HasValue,
        q => q.Where(o => o.Total >= filterCriteria.MinPrice.Value))
    .OrderBy(o => o.CreatedDate)
    .ToListAsync();
```

#### Multi-step processing

```csharp
var processed = await users
    .Find(u => u.IsActive)
    .Throw(() => new InvalidOperationException("No active users"))
    .SelectAsync(async (u, ct) => await EnrichUserDataAsync(u, ct), ct)
    .DoAsync(async (u, ct) => await LogProcessingAsync(u, ct), ct)
    .UnlessAsync(
        async (u, ct) => await IsBlacklistedAsync(u, ct),
        async (u, ct) => await ApplyAccessRulesAsync(u, ct),
        ct);
```

### Best practices

#### 1. Choose the right conditional method

Use `When` for positive conditions and `Unless` for negative conditions:

```csharp
// Clear with When
items.When(_ => isActive, q => q.Where(i => i.Status == "active"))

// Clear with Unless
items.Unless(_ => isArchived, q => q.Where(i => i.Status != "archived"))

// Avoid double negation
items.Unless(_ => !isArchived, q => ...)  // Hard to read
```

#### 2. Use single-branch `When` for filters

Only use the both-branch overload when you actually need two different transformations:

```csharp
// Good - single branch, simple filtering
items.When(_ => hasFilter, q => q.Where(...))

// Good - both branches needed for different transformations
items.When(_ => sortAsc,
    q => q.OrderBy(x => x.Date),      // then
    q => q.OrderByDescending(x => x.Date))  // else

// Avoid - unnecessary both-branch when else does nothing
items.When(_ => condition,
    q => q.Where(...),
    q => q)  // Redundant
```

#### 3. Mix sync and async naturally

Use `Select` to bridge from async to sync operations:

```csharp
// Natural flow: async -> sync -> async
await orders
    .FindAsync(async (o, token) => await IsPendingAsync(o, token), ct) // Async
    .Select(o => o.Items)                  // Sync
    .SelectAsync(async i => await EnrichAsync(i), ct);  // Async
```

#### 4. Use `Do` for observability

Keep side effects explicit without changing flow:

```csharp
var observed = data
    .Do(d => logger.LogInformation("Processing: {Id}", d.Id))
    .Do(d => metrics.Increment("processed"));

var result = Transform(observed);
```

#### 5. Combine operations meaningfully

Chain operations that form a complete workflow:

```csharp
var finalResult = await initial
    .SelectAsync(async x => await ValidateAsync(x, ct), ct)
    .ThrowWhenAsync(
        async (x, c) => await IsInvalidAsync(x, c),
        (x, _) => Task.FromResult<Exception>(
            new ValidationException(x.ToString())),
        ct)
    .DoAsync(async (x, c) => await LogSuccessAsync(x, c), ct)
    .SelectAsync(async (x, c) => await SaveAsync(x, c), ct);
```

### Performance considerations

1. **Execution timing**: Value and task extensions execute when called; any `IEnumerable<T>` returned by a transformation retains that sequence's normal lazy behavior.
2. **Async predicates**: `FindAsync` checks items sequentially and stops at the first match.
3. **Null checks**: The null-aware methods use direct null or `HasValue` checks.
4. **Cancellation**: Async overloads accept cancellation tokens, but supplied delegates must also observe the token when appropriate.

### Limitations and gotchas

#### QueryProvider compatibility

`When` and `Unless` invoke a delegate that returns the next value in the chain. They can return an `IQueryable<T>`, but any expression inside that query must still be translatable by the database provider:

```csharp
// Works - filter is translatable
var results = await context.Orders
    .When(_ => minPrice.HasValue,
        q => q.Where(o => o.Total >= minPrice.Value))
    .ToListAsync();

// Won't work - filtering happens in memory
var results = await context.Orders
    .ToList()  // Materializes to memory
    .When(_ => minPrice.HasValue,
        q => q.Where(o => o.Total >= minPrice.Value))
    .ToList();
```

#### Async context

Always maintain the async context properly:

```csharp
// Correct - maintains async context
await value.SelectAsync(async v => await ProcessAsync(v), ct);

// Problematic - blocks the thread
value.SelectAsync(async v => await ProcessAsync(v), ct).Result;
```

---

## Async enumerable extensions

The async enumerable extensions provide LINQ-like operations for `IAsyncEnumerable<T>` sequences with cancellation-aware iteration and transformation.

### Overview

These extensions enable working with asynchronous sequences in a familiar LINQ style while maintaining proper async/await semantics and cancellation support. They are particularly useful when working with database queries, API streams, and other async data sources.

#### Key benefits

1. **Familiar API**: LINQ-like methods you already know
2. **Async-Aware**: Built for async scenarios with cancellation support
3. **Efficient**: Lazy evaluation and streaming where appropriate
4. **Memory-Friendly**: Process large sequences without materializing to memory

### Extension reference

#### Querying operations

**AnyAsync** - Check if any elements match a condition:

```csharp
// Check if any active users exist
bool hasActive = await users.AnyAsync(u => u.IsActive, cancellationToken);
```

**ContainsAsync** - Check if sequence contains a specific value:

```csharp
// Check if user exists in collection
bool exists = await users.ContainsAsync(targetUser, cancellationToken);

// With custom equality comparer
bool exists = await users.ContainsAsync(targetUser, userComparer, cancellationToken);
```

**CountAsync** - Count elements matching a condition:

```csharp
// Count all items
int total = await items.CountAsync(cancellationToken);

// Count matching condition
int activeCount = await items.CountAsync(i => i.IsActive, cancellationToken);
```

#### Filtering operations

**WhereAsync** - Lazily filter an asynchronous sequence with a synchronous predicate:

```csharp
// Filter active items
var active = items
    .WhereAsync(i => i.IsActive, cancellationToken)
    .SelectAsync(i => i.Name, cancellationToken);
```

**WhereNotNull** - Filter out null values:

```csharp
// Remove null entries
var valid = items.WhereNotNull(cancellationToken);
```

**WhereNotNullOrEmpty** - Filter out null/empty strings:

```csharp
// Keep only non-empty strings
var populated = strings.WhereNotNullOrEmpty(cancellationToken);
```

**WhereNotNullOrWhiteSpace** - Filter out null/whitespace strings:

```csharp
// Keep meaningful strings
var meaningful = strings.WhereNotNullOrWhiteSpace(cancellationToken);
```

#### Selection operations

**SelectAsync** - Lazily transform elements with a synchronous selector:

```csharp
// Transform each item
var names = users
    .SelectAsync(u => u.Name, cancellationToken);

```

This `IAsyncEnumerable<T>` extension does not accept an asynchronous selector. Use `await foreach` when each projection needs asynchronous work.

#### Aggregation operations

**FirstAsync** - Get first element or matching element:

```csharp
// Get first element
var first = await items.FirstAsync(cancellationToken);

// Get first matching
var active = await items.FirstAsync(i => i.IsActive, cancellationToken);
```

**FirstOrDefaultAsync** - Get first matching element or default:

```csharp
// Get first match or null
var active = await items.FirstOrDefaultAsync(i => i.IsActive, cancellationToken);
```

**LastAsync** - Get last element or matching element:

```csharp
// Get last element
var last = await items.LastAsync(cancellationToken);

// Get last matching
var lastActive = await items.LastAsync(i => i.IsActive, cancellationToken);
```

**LastOrDefaultAsync** - Get last matching element or default:

```csharp
// Get last match or null
var lastActive = await items.LastOrDefaultAsync(i => i.IsActive, cancellationToken);
```

#### Partitioning operations

**TakeAsync** - Take first N elements:

```csharp
// Get first 10 items
var first10 = items.TakeAsync(10, cancellationToken);

// Take while condition is true
var batch = items
    .TakeAsync(100, cancellationToken)
    .WhereAsync(i => i.IsValid, cancellationToken);
```

**SkipAsync** - Skip first N elements:

```csharp
// Skip first 20, get rest
var remaining = items.SkipAsync(20, cancellationToken);

// Pagination pattern
var page = items
    .TakeAsync(pageSize, cancellationToken);

if (pageNumber > 1)
{
    page = items
        .SkipAsync((pageNumber - 1) * pageSize, cancellationToken)
        .TakeAsync(pageSize, cancellationToken);
}
```

#### Deduplication operations

**DistinctAsync** - Remove duplicate elements:

```csharp
// Remove duplicates
var unique = items.DistinctAsync(cancellationToken);

// With custom comparer
var unique = items.DistinctAsync(comparer, cancellationToken);
```

**DistinctByAsync** - Remove duplicates by key:

```csharp
// Remove users with duplicate IDs
var uniqueUsers = users
    .DistinctByAsync(u => u.Id, cancellationToken);

// With custom comparer
var unique = items
    .DistinctByAsync(i => i.Category, categoryComparer, cancellationToken);
```

#### Concatenation

**ConcatAsync** - Combine two async sequences:

```csharp
// Combine results from multiple sources
var combined = source1
    .ConcatAsync(source2, cancellationToken)
    .ConcatAsync(source3, cancellationToken);
```

### Common scenarios

#### Streaming results

```csharp
// Process large result set without materializing
await foreach (var order in database
    .GetOrdersAsync(cancellationToken)
    .WhereAsync(o => o.Total > 100, cancellationToken)
    .WithCancellation(cancellationToken))
{
    var enriched = await EnrichAsync(order, cancellationToken);
    logger.LogInformation("Order {OrderId}", enriched.Id);
}
```

#### Pagination

```csharp
// Implement pagination without loading entire set
public async IAsyncEnumerable<Item> GetPagedItemsAsync(
    int pageNumber,
    int pageSize,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    var skip = (pageNumber - 1) * pageSize;
    
    var source = database.GetItemsAsync(cancellationToken);
    if (skip > 0)
    {
        source = source.SkipAsync(skip, cancellationToken);
    }

    await foreach (var item in source
        .TakeAsync(pageSize, cancellationToken)
        .WithCancellation(cancellationToken))
    {
        yield return item;
    }
}
```

`SkipAsync(0)` currently produces an empty sequence. Bypass `SkipAsync` when the calculated skip count is zero, as the example does.

#### Filtering and validation

```csharp
// Combine synchronous stream filtering with asynchronous validation
var validItems = new List<Item>();
await foreach (var item in source
    .WhereNotNull(cancellationToken)
    .WhereAsync(i => i.IsActive, cancellationToken)
    .WithCancellation(cancellationToken))
{
    if (await ValidateAsync(item, cancellationToken))
    {
        validItems.Add(item);
    }
}
```

#### Deduplication

```csharp
// Remove duplicates by category and keep first occurrence
var uniqueByCategory = items
    .DistinctByAsync(i => i.Category, cancellationToken);

// Further filter and deduplicate
var filtered = items
    .WhereAsync(i => i.IsValid, cancellationToken)
    .DistinctAsync(cancellationToken);
```

### Best practices

#### 1. Use lazy evaluation

Leverage lazy evaluation for large sequences:

```csharp
// Good - operations are lazy
var processed = source
    .WhereAsync(i => i.IsActive, cancellationToken)
    .SelectAsync(i => Transform(i), cancellationToken)
    .TakeAsync(100, cancellationToken);

// Then consume when needed
var results = new List<Item>();
await foreach (var item in processed.WithCancellation(cancellationToken))
{
    results.Add(item);
}
```

#### 2. Chain efficiently

Order operations to filter early:

```csharp
// Good - filter before transform
var results = items
    .WhereAsync(i => i.IsValid, cancellationToken)
    .SelectAsync(i => Transform(i), cancellationToken);

// Avoid - transform then filter
var results = items
    .SelectAsync(i => Transform(i), cancellationToken)
    .WhereAsync(i => i.IsValid, cancellationToken);
```

#### 3. Respect cancellation

Always pass cancellation tokens:

```csharp
// Good - cancellation is respected
await foreach (var item in items
    .WhereAsync(i => i.IsActive, cancellationToken)
    .WithCancellation(cancellationToken))
{
    await ProcessAsync(item, cancellationToken);
}

// Avoid - no cancellation support
await foreach (var item in items.WhereAsync(i => i.IsActive))
{
    await ProcessAsync(item);
}
```

#### 4. Handle large sequences

Cap a single pass when only an initial segment is needed:

```csharp
// Process at most one segment
const int batchSize = 1000;
var processed = 0;

await foreach (var item in items
    .TakeAsync(batchSize, cancellationToken)
    .WithCancellation(cancellationToken))
{
    await ProcessAsync(item, cancellationToken);
    processed++;
}
```

### Performance considerations

1. **Lazy evaluation**: Sequence-returning methods defer enumeration.
2. **Memory use**: Lazy operators stream items, while `DistinctAsync` and `DistinctByAsync` retain a set of seen values or keys.
3. **Cancellation**: Operators pass the supplied token into source enumeration; caller-provided work must observe its token separately.
4. **Repeated enumeration**: Enumerating the same pipeline again reruns its source and operators.

### Limitations

#### Database query providers

Some async enumerable operations may not translate to database queries:

```csharp
// Runs client-side after AsAsyncEnumerable
var distinctCount = await dbContext.Orders
    .AsAsyncEnumerable()
    .DistinctByAsync(o => o.CustomerId, cancellationToken)
    .CountAsync(cancellationToken);

// Better - use LINQ-to-Entities
var serverResults = await dbContext.Orders
    .GroupBy(o => o.CustomerId)
    .Select(g => g.First())
    .ToListAsync(cancellationToken);
```
