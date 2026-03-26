# LINQ Extensions Feature Documentation

## Overview

The LINQ Extensions provide a comprehensive set of methods to work with sequences, async enumerables, and optional values. They are organized into functional groups based on their purpose and context of use.

This page focuses on the LINQ-oriented extension families. For a broader package-level overview of the extension helpers available from `Common.Abstractions`, see [Common Extensions](./common-extensions.md).

---

# Fluent Extensions

The LINQ Fluent Extensions provide a fluent, functional approach to handling null values, conditional operations, and async/sync mixing in C# applications. These extensions complement standard LINQ by enabling cleaner, more readable code when dealing with optional values and complex filtering scenarios.

## Overview

### Key Benefits

1. **Null-Safe Chaining**: Seamlessly work with nullable values without null checks
2. **Functional Composition**: Chain operations elegantly using a fluent interface
3. **Async/Sync Flexibility**: Mix synchronous and asynchronous operations in a single chain
4. **Readable Intent**: Code reads like natural language describing the operation flow
5. **Error Prevention**: Compile-time type safety eliminates common null-reference exceptions

### Architecture

The extensions are organized into logical groups:

- **Find Operations**: More fluent alternatives to `FirstOrDefault/First/Last`
- **Null Handling**: Conditional execution based on null state
- **String Checks**: Specialized null/empty validation for strings
- **Conditional Logic**: When/Unless for predicate-based operations
- **Transformations**: Select/Map for value transformations
- **Side Effects**: Do for logging and non-transforming operations (Tap)
- **Error Handling**: Throw/ThrowWhen for validation
- **Pattern Matching**: Match for both-case handling
- **Fallback Values**: OrElse for default factories

## Common Usage Patterns

### Basic Null Checking

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

### Conditional LINQ Chains

Apply filters and transformations conditionally:

```csharp
// Traditional approach
var query = orders.AsQueryable();
if (!string.IsNullOrEmpty(searchTerm))
    query = query.Where(o => o.Description.Contains(searchTerm));
if (minPrice.HasValue)
    query = query.Where(o => o.Total >= minPrice.Value);

var results = await query.ToListAsync();

// Using extensions - cleaner with single-branch When
var results = await orders
    .When(!string.IsNullOrEmpty(searchTerm),
        q => q.Where(o => o.Description.Contains(searchTerm)))
    .When(minPrice.HasValue,
        q => q.Where(o => o.Total >= minPrice.Value))
    .ToListAsync();

// Or with Unless for inverted conditions
var results = await orders
    .Unless(string.IsNullOrEmpty(searchTerm),
        q => q.Where(o => o.Description.Contains(searchTerm)))
    .Unless(!minPrice.HasValue,
        q => q.Where(o => o.Total >= minPrice.Value))
    .ToListAsync();
```

### Validation and Error Handling

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

### Async/Sync Mixing

Seamlessly transition between async and sync operations:

```csharp
// Load async, then process sync, then transform async
var result = await users
    .FindAsync(u => u.IsActive, cancellationToken)           // Async find
    .Select(u => u.Profile)                                  // Sync select
    .SelectAsync(async p => await enrichService.EnrichAsync(p), cancellationToken)  // Async select
    .Do(p => logger.LogInfo($"Processed: {p.Name}"))        // Sync side effect
    .DoAsync(async p => await cache.StoreAsync(p), cancellationToken);  // Async side effect
```

## Extension Reference

### Find Operations

**Find** fluent alternatives to `FirstOrDefault`:

```csharp
// Find first matching element
var user = users.Find(u => u.IsAdmin);

// Find first element (any)
var first = orders.FindFirst();

// Async find with async predicate
var product = await products.FindAsync(
    async (p, ct) => await IsInStockAsync(p, ct),
    cancellationToken);
```

### Null Handling

**WhenNotNull/WhenNull** execute operations based on null state:

```csharp
// Execute side effect if not null
await user
    .WhenNotNullAsync(async u => await LogUserAccessAsync(u.Id), cancellationToken);

// Execute if null (alternative path)
await user
    .WhenNullAsync(async ct => await CreateDefaultUserAsync(ct), cancellationToken);
```

### String Checks

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

### Conditional Logic

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
    .When(!string.IsNullOrEmpty(searchTerm),
        q => q.Where(o => o.Description.Contains(searchTerm)))
    .When(minPrice.HasValue,
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
    .Unless(string.IsNullOrEmpty(searchTerm),
        q => q.Where(o => o.Description.Contains(searchTerm)))
    .Unless(!minPrice.HasValue,
        q => q.Where(o => o.Total >= minPrice.Value))
    .ToListAsync();
```

### Transformations

**Select/SelectAsync** transform values fluently:

```csharp
// Sync transformation
var emails = users
    .Find(u => u.IsActive)
    .Select(u => u.Email);

// Async transformation
var enriched = await user
    .SelectAsync(async (u, ct) => await LoadProfileAsync(u, ct), cancellationToken);

// Bridge sync to async
var result = await users
    .FindAsync(u => u.IsActive, cancellationToken)
    .Select(u => u.Profile)                    // Sync after async
    .SelectAsync(async p => await EnrichAsync(p), cancellationToken);
```

### Side Effects

**Do** execute operations without changing the value (Tap):

```csharp
// Log without changing value
var user = repository
    .Find(u => u.Id == id)
    .Do(u => logger.LogInfo($"Found: {u.Name}"))
    .Do(u => auditService.Log(u.Id));

// Async side effects
await order
    .DoAsync(async (o, ct) => await cache.StoreAsync(o, ct), cancellationToken)
    .DoAsync(async (o, ct) => await analytics.TrackAsync(o.Id, ct), cancellationToken);
```

### Error Handling

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

### Pattern Matching

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

### Fallback Values

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

## Common Scenarios

### API Request Processing

```csharp
app.MapGet("/api/users/{id}", async Task<IResult>
    (int id, IUserRepository repository, ILogger<Program> logger, CancellationToken ct) =>
{
    return await repository
        .FindAsync(u => u.Id == id, ct)
        .DoAsync(async u => await logger.LogAccessAsync(u.Id, ct), ct)
        .MatchAsync(
            some: async (user, c) => TypedResults.Ok(user),
            none: async _ => TypedResults.NotFound(),
            cancellationToken: ct);
});
```

### Data Validation Pipeline

```csharp
var validatedData = await inputData
    .When(data => !string.IsNullOrEmpty(data.Email),
        d => NormalizeEmail(d))
    .SelectAsync(async d => await ValidateAsync(d, ct), ct)
    .ThrowWhenAsync(
        async (d, c) => !(await IsUniqueAsync(d, c)),
        d => new ValidationError("Email already exists"),
        ct);
```

### Conditional Query Building

```csharp
var results = await orders
    .When(filterCriteria.HasCategory,
        q => q.Where(o => o.Category == filterCriteria.Category))
    .When(!filterCriteria.IncludeArchived,
        q => q.Where(o => !o.IsArchived))
    .When(filterCriteria.MinPrice.HasValue,
        q => q.Where(o => o.Total >= filterCriteria.MinPrice.Value))
    .OrderBy(o => o.CreatedDate)
    .ToListAsync();
```

### Multi-Step Processing

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

## Best Practices

### 1. Choose the Right Conditional Method

Use `When` for positive conditions and `Unless` for negative conditions:

```csharp
// Clear with When
items.When(isActive, q => q.Where(i => i.Status == "active"))

// Clear with Unless
items.Unless(isArchived, q => q.Where(i => i.Status != "archived"))

// Avoid double negation
items.Unless(!isArchived, q => ...)  // Hard to read
```

### 2. Use Single-Branch When for Filters

Only use the both-branch overload when you actually need two different transformations:

```csharp
// Good - single branch, simple filtering
items.When(hasFilter, q => q.Where(...))

// Good - both branches needed for different transformations
items.When(sortAsc,
    q => q.OrderBy(x => x.Date),      // then
    q => q.OrderByDescending(x => x.Date))  // else

// Avoid - unnecessary both-branch when else does nothing
items.When(condition,
    q => q.Where(...),
    q => q)  // Redundant
```

### 3. Mix Sync and Async Naturally

Use `Select` to bridge from async to sync operations:

```csharp
// Natural flow: async -> sync -> async
await orders
    .FindAsync(o => o.IsPending, ct)      // Async
    .Select(o => o.Items)                  // Sync
    .SelectAsync(async i => await EnrichAsync(i), ct);  // Async
```

### 4. Use Do for Observability

Keep side effects explicit without changing flow:

```csharp
var result = data
    .Do(d => logger.LogInfo($"Processing: {d.Id}"))
    .Do(d => metrics.Increment("processed"))
    .Select(d => Transform(d));
```

### 5. Combine Operations Meaningfully

Chain operations that form a complete workflow:

```csharp
var finalResult = await initial
    .SelectAsync(async x => await ValidateAsync(x, ct), ct)
    .ThrowWhenAsync(
        async (x, c) => await IsInvalidAsync(x, c),
        x => new ValidationException(x.ToString()),
        ct)
    .DoAsync(async (x, c) => await LogSuccessAsync(x, c), ct)
    .SelectAsync(async (x, c) => await SaveAsync(x, c), ct);
```

## Performance Considerations

1. **Lazy Evaluation**: Operations are executed only when needed in async chains
2. **Minimal Allocations**: Extensions use value types where possible
3. **Efficient Null Checks**: Direct null comparisons, no reflection
4. **Cancellation Support**: All async operations respect cancellation tokens

## Limitations and Gotchas

### QueryProvider Compatibility

When using `When/Unless` with LINQ-to-Database providers (EF Core, LINQ to SQL), ensure the entire chain remains translatable:

```csharp
// Works - filter is translatable
var results = await context.Orders
    .When(minPrice.HasValue,
        q => q.Where(o => o.Total >= minPrice.Value))
    .ToListAsync();

// Won't work - filtering happens in memory
var results = await context.Orders
    .ToList()  // Materializes to memory
    .When(minPrice.HasValue,
        q => q.Where(o => o.Total >= minPrice.Value))
    .ToListAsync();
```

### Async Context

Always maintain the async context properly:

```csharp
// Correct - maintains async context
await value.SelectAsync(async v => await ProcessAsync(v), ct);

// Problematic - blocks the thread
value.SelectAsync(async v => await ProcessAsync(v), ct).Result;
```

---

# Async Enumerable Extensions

The Async Enumerable Extensions provide LINQ-like operations for `IAsyncEnumerable<T>` sequences, enabling efficient async iteration and transformation of sequences with proper cancellation support.

## Overview

These extensions enable working with asynchronous sequences in a familiar LINQ style while maintaining proper async/await semantics and cancellation support. They are particularly useful when working with database queries, API streams, and other async data sources.

### Key Benefits

1. **Familiar API**: LINQ-like methods you already know
2. **Async-Aware**: Built for async scenarios with cancellation support
3. **Efficient**: Lazy evaluation and streaming where appropriate
4. **Memory-Friendly**: Process large sequences without materializing to memory

## Extension Reference

### Querying Operations

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

### Filtering Operations

**WhereAsync** - Filter elements asynchronously:

```csharp
// Filter active items
var active = items
    .WhereAsync(i => i.IsActive, cancellationToken)
    .Select(i => i.Name);
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

### Selection Operations

**SelectAsync** - Transform elements asynchronously:

```csharp
// Transform each item
var names = users
    .SelectAsync(u => u.Name, cancellationToken);

// With async transformation
var enriched = users
    .SelectAsync(
        async (u, ct) => await LoadDetailsAsync(u, ct),
        cancellationToken);
```

### Aggregation Operations

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

### Partitioning Operations

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
    .SkipAsync((pageNumber - 1) * pageSize, cancellationToken)
    .TakeAsync(pageSize, cancellationToken);
```

### Deduplication Operations

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

### Concatenation

**ConcatAsync** - Combine two async sequences:

```csharp
// Combine results from multiple sources
var combined = source1
    .ConcatAsync(source2, cancellationToken)
    .ConcatAsync(source3, cancellationToken);
```

## Common Scenarios

### Streaming Results

```csharp
// Process large result set without materializing
await database.GetOrdersAsync(cancellationToken)
    .WhereAsync(o => o.Total > 100, cancellationToken)
    .SelectAsync(async o => await EnrichAsync(o, cancellationToken), cancellationToken)
    .ForEachAsync(o => logger.LogInfo($"Order {o.Id}"), cancellationToken);
```

### Pagination

```csharp
// Implement pagination without loading entire set
public async IAsyncEnumerable<Item> GetPagedItemsAsync(
    int pageNumber,
    int pageSize,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    var skip = (pageNumber - 1) * pageSize;
    
    await foreach (var item in database
        .GetItemsAsync(cancellationToken)
        .SkipAsync(skip, cancellationToken)
        .TakeAsync(pageSize, cancellationToken)
        .WithCancellation(cancellationToken))
    {
        yield return item;
    }
}
```

### Filtering and Validation

```csharp
// Chain multiple filters
var validItems = await source
    .WhereNotNull(cancellationToken)
    .WhereAsync(i => await ValidateAsync(i, cancellationToken), cancellationToken)
    .WhereAsync(i => i.IsActive, cancellationToken)
    .ToListAsync(cancellationToken);
```

### Deduplication

```csharp
// Remove duplicates by category and keep first occurrence
var uniqueByCategory = items
    .DistinctByAsync(i => i.Category, cancellationToken);

// Further filter and deduplicate
var filtered = items
    .WhereAsync(i => i.IsValid, cancellationToken)
    .DistinctAsync(cancellationToken);
```

## Best Practices

### 1. Use Lazy Evaluation

Leverage lazy evaluation for large sequences:

```csharp
// Good - operations are lazy
var processed = source
    .WhereAsync(i => i.IsActive, cancellationToken)
    .SelectAsync(i => Transform(i), cancellationToken)
    .TakeAsync(100, cancellationToken);

// Then materialize when needed
var results = await processed.ToListAsync(cancellationToken);
```

### 2. Chain Efficiently

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

### 3. Respect Cancellation

Always pass cancellation tokens:

```csharp
// Good - cancellation is respected
var results = await items
    .WhereAsync(i => i.IsActive, cancellationToken)
    .ToListAsync(cancellationToken);

// Avoid - no cancellation support
var results = await items
    .WhereAsync(i => i.IsActive)
    .ToListAsync();
```

### 4. Handle Large Sequences

Process in batches to avoid memory issues:

```csharp
// Process in chunks
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

## Performance Considerations

1. **Lazy Evaluation**: Methods return `IAsyncEnumerable<T>` for lazy processing
2. **Memory Efficiency**: Only materialized when explicitly called (`.ToListAsync()`)
3. **Cancellation**: Full cancellation support throughout the chain
4. **No Double Enumeration**: Be careful not to enumerate multiple times

## Limitations

### Database Query Providers

Some async enumerable operations may not translate to database queries:

```csharp
// May not translate - use LINQ-to-Entities instead
var results = await dbContext.Orders
    .AsAsyncEnumerable()
    .DistinctByAsync(o => o.CustomerId, cancellationToken)
    .ToListAsync(cancellationToken);

// Better - use LINQ-to-Entities
var results = await dbContext.Orders
    .GroupBy(o => o.CustomerId)
    .Select(g => g.First())
    .ToListAsync(cancellationToken);
```
