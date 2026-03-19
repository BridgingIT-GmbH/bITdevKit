# Performance Checklist

Use this checklist to identify performance issues and bottlenecks in C#/.NET code. Focus on async/await patterns, resource management, and efficient algorithms.

## Async/Await Usage (🟡 IMPORTANT)

- [ ] **Async for I/O-bound**: Use async for database, file, network, HTTP operations
- [ ] **Async suffix**: Async methods end with `Async` (`GetCustomerAsync`, `SaveAsync`)
- [ ] **Async propagation**: Async flows through call stack (no sync wrappers)
- [ ] **CancellationToken included**: Async methods accept `CancellationToken` parameter
- [ ] **ConfigureAwait considered**: Use `ConfigureAwait(false)` in libraries (not in ASP.NET Core)
- [ ] **Task returned**: Async methods return `Task` or `Task<T>` (not `async void` except event handlers)

### Example

```csharp
// 🔴 WRONG: Synchronous I/O (blocks thread)
public Customer GetCustomer(Guid id)
{
    using var connection = new SqlConnection(connectionString);
    connection.Open();
    var command = new SqlCommand("SELECT * FROM Customers WHERE Id = @Id", connection);
    command.Parameters.AddWithValue("@Id", id);
    using var reader = command.ExecuteReader();
    // ...
}

// ✅ CORRECT: Async I/O (non-blocking)
public async Task<Customer> GetCustomerAsync(Guid id, CancellationToken cancellationToken)
{
    return await context.Customers
        .Where(c => c.Id == id)
        .FirstOrDefaultAsync(cancellationToken);
}

// 🟡 IMPORTANT: Sync wrapper around async (anti-pattern)
public Customer GetCustomer(Guid id)
{
    return GetCustomerAsync(id, CancellationToken.None).Result; // WRONG! Can cause deadlocks
}

// ✅ CORRECT: Keep it async
public async Task<Customer> GetCustomerAsync(Guid id, CancellationToken cancellationToken)
{
    return await context.Customers
        .Where(c => c.Id == id)
        .FirstOrDefaultAsync(cancellationToken);
}
```

**Why it matters**: Async I/O improves scalability by freeing threads during I/O operations. Synchronous I/O blocks threads, reducing throughput under load.

## Blocking on Async Code (🔴 CRITICAL)

**Rule**: NEVER block on async code using `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`.

- [ ] **No .Result**: Code doesn't use `.Result` on Tasks
- [ ] **No .Wait()**: Code doesn't use `.Wait()` on Tasks
- [ ] **No .GetAwaiter().GetResult()**: Code doesn't use this pattern
- [ ] **Await instead**: Always `await` async methods
- [ ] **Check deadlock risk**: Especially risky in ASP.NET, WPF, WinForms contexts

### Example

```csharp
// 🔴 CRITICAL: Blocking on async code (can cause deadlocks)
public class CustomerController : ControllerBase
{
    private readonly ICustomerService service;
    
    public IActionResult GetCustomer(Guid id)
    {
        var customer = service.GetCustomerAsync(id, CancellationToken.None).Result; // DEADLOCK RISK!
        return Ok(customer);
    }
}

// 🔴 CRITICAL: Also wrong
public IActionResult GetCustomer(Guid id)
{
    service.GetCustomerAsync(id, CancellationToken.None).Wait(); // DEADLOCK RISK!
    return Ok();
}

// ✅ CORRECT: Make the method async and await
public class CustomerController : ControllerBase
{
    private readonly ICustomerService service;
    
    public async Task<IActionResult> GetCustomerAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await service.GetCustomerAsync(id, cancellationToken);
        return Ok(customer);
    }
}
```

**Why it matters**: Blocking on async code can cause deadlocks in UI and ASP.NET contexts. It also negates the scalability benefits of async.

**How to detect**: Search codebase for:
- `.Result`
- `.Wait()`
- `.GetAwaiter().GetResult()`

These are almost always incorrect in ASP.NET Core applications.

## CancellationToken Propagation (🟡 IMPORTANT)

- [ ] **Parameter included**: Async methods accept `CancellationToken` parameter
- [ ] **Passed downstream**: CancellationToken passed to all async calls
- [ ] **Default parameter**: Use `CancellationToken cancellationToken = default` for optional
- [ ] **Checked in loops**: Long-running loops check `cancellationToken.IsCancellationRequested`
- [ ] **ThrowIfCancellationRequested**: Use for immediate cancellation

### Example

```csharp
// 🟡 IMPORTANT: Missing CancellationToken
public async Task<List<Customer>> GetAllCustomersAsync()
{
    return await context.Customers.ToListAsync();
}

public async Task ProcessCustomersAsync()
{
    var customers = await GetAllCustomersAsync();
    foreach (var customer in customers)
    {
        await ProcessCustomerAsync(customer);
    }
}

// ✅ CORRECT: CancellationToken propagated
public async Task<List<Customer>> GetAllCustomersAsync(CancellationToken cancellationToken)
{
    return await context.Customers.ToListAsync(cancellationToken);
}

public async Task ProcessCustomersAsync(CancellationToken cancellationToken)
{
    var customers = await GetAllCustomersAsync(cancellationToken);
    
    foreach (var customer in customers)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await ProcessCustomerAsync(customer, cancellationToken);
    }
}
```

**Why it matters**: CancellationToken allows graceful cancellation of long-running operations when users cancel requests or operations time out.

## Resource Disposal (🔴 CRITICAL)

**Rule**: All `IDisposable` objects **must** be disposed properly.

- [ ] **Using statements**: `IDisposable` objects wrapped in `using` statements
- [ ] **Using declarations**: Use `using var` syntax (C# 8+) for cleaner code
- [ ] **Async disposal**: Use `await using` for `IAsyncDisposable` objects
- [ ] **No manual Dispose**: Prefer `using` over manual `try/finally` with `Dispose()`
- [ ] **HttpClient properly managed**: Use `IHttpClientFactory`, not `new HttpClient()` in loops

### Example

```csharp
// 🔴 CRITICAL: IDisposable not disposed (memory/connection leak)
public void ProcessFile(string path)
{
    var stream = File.OpenRead(path);
    var reader = new StreamReader(stream);
    var content = reader.ReadToEnd();
    // stream and reader never disposed!
}

// 🔴 CRITICAL: HttpClient created in loop (socket exhaustion)
public async Task<List<string>> FetchDataAsync(List<string> urls)
{
    var results = new List<string>();
    foreach (var url in urls)
    {
        var client = new HttpClient(); // WRONG! Exhausts sockets
        var response = await client.GetStringAsync(url);
        results.Add(response);
    }
    return results;
}

// ✅ CORRECT: Using statement (traditional)
public void ProcessFile(string path)
{
    using (var stream = File.OpenRead(path))
    using (var reader = new StreamReader(stream))
    {
        var content = reader.ReadToEnd();
        // stream and reader automatically disposed
    }
}

// ✅ BETTER: Using declaration (C# 8+)
public void ProcessFile(string path)
{
    using var stream = File.OpenRead(path);
    using var reader = new StreamReader(stream);
    var content = reader.ReadToEnd();
    // Disposed at end of method scope
}

// ✅ CORRECT: Async disposal
public async Task ProcessFileAsync(string path)
{
    await using var stream = File.OpenRead(path);
    using var reader = new StreamReader(stream);
    var content = await reader.ReadToEndAsync();
}

// ✅ CORRECT: IHttpClientFactory for HttpClient
public class MyService
{
    private readonly IHttpClientFactory httpClientFactory;
    
    public MyService(IHttpClientFactory httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory;
    }
    
    public async Task<string> FetchDataAsync(string url)
    {
        var client = this.httpClientFactory.CreateClient();
        return await client.GetStringAsync(url);
    }
}
```

**Why it matters**: Undisposed resources cause memory leaks, connection pool exhaustion, and file handle exhaustion.

**How to detect**: Look for:
- `Stream`, `StreamReader`, `StreamWriter` without `using`
- `SqlConnection`, `DbContext` without `using`
- `HttpClient` created with `new` (should use `IHttpClientFactory`)
- Any class implementing `IDisposable` not in `using` statement

## String Operations (🟢 SUGGESTION)

- [ ] **StringBuilder for concatenation**: Use `StringBuilder` for string concatenation in loops
- [ ] **String interpolation**: Use `$"..."` for simple string formatting
- [ ] **Span<T> for parsing**: Use `Span<T>` or `ReadOnlySpan<T>` for high-performance parsing
- [ ] **String.Create**: Use `String.Create` for optimized string building
- [ ] **Avoid allocations**: Minimize string allocations in hot paths

### Example

```csharp
// 🟢 SUGGESTION: String concatenation in loop (allocates many strings)
public string BuildCsv(List<Customer> customers)
{
    var csv = "Id,Name,Email\n";
    foreach (var customer in customers)
    {
        csv += $"{customer.Id},{customer.Name},{customer.Email}\n"; // Allocates new string each iteration
    }
    return csv;
}

// ✅ CORRECT: StringBuilder for concatenation
public string BuildCsv(List<Customer> customers)
{
    var sb = new StringBuilder();
    sb.AppendLine("Id,Name,Email");
    foreach (var customer in customers)
    {
        sb.AppendLine($"{customer.Id},{customer.Name},{customer.Email}");
    }
    return sb.ToString();
}

// 🟢 SUGGESTION: Could use Span<T> for parsing
public int ParseNumber(string input)
{
    return int.Parse(input.Substring(0, 5)); // Allocates substring
}

// ✅ CORRECT: Span<T> for zero-allocation parsing
public int ParseNumber(string input)
{
    return int.Parse(input.AsSpan(0, 5)); // No allocation
}
```

**Why it matters**: String operations can create many allocations in performance-critical code. `StringBuilder` and `Span<T>` reduce allocations.

## Collection Choices (🟢 SUGGESTION)

- [ ] **List<T> for ordered collections**: Use `List<T>` for simple lists
- [ ] **Dictionary<TKey, TValue> for lookups**: Use for key-based access (O(1))
- [ ] **HashSet<T> for uniqueness**: Use for unique items with fast contains checks
- [ ] **IReadOnlyList<T> for immutability**: Use for returning collections that shouldn't be modified
- [ ] **Array for fixed size**: Use arrays when size is known and fixed
- [ ] **Capacity hints**: Specify initial capacity if size is known (`new List<T>(capacity)`)

### Example

```csharp
// 🟢 SUGGESTION: Using List when Dictionary would be better
public class CustomerService
{
    private readonly List<Customer> customers = new();
    
    public Customer FindById(Guid id)
    {
        return customers.FirstOrDefault(c => c.Id == id); // O(n) search
    }
}

// ✅ CORRECT: Dictionary for O(1) lookups
public class CustomerService
{
    private readonly Dictionary<Guid, Customer> customers = new();
    
    public Customer FindById(Guid id)
    {
        return customers.TryGetValue(id, out var customer) ? customer : null; // O(1) lookup
    }
}

// 🟢 SUGGESTION: No initial capacity hint
public List<Customer> GetCustomers()
{
    var customers = new List<Customer>();
    for (int i = 0; i < 1000; i++)
    {
        customers.Add(new Customer()); // Multiple internal array resizes
    }
    return customers;
}

// ✅ CORRECT: Initial capacity specified
public List<Customer> GetCustomers()
{
    var customers = new List<Customer>(1000); // Single allocation, no resizes
    for (int i = 0; i < 1000; i++)
    {
        customers.Add(new Customer());
    }
    return customers;
}
```

**Why it matters**: Choosing the right collection type impacts performance significantly. Dictionary lookups are O(1) vs O(n) for List searches.

## LINQ Efficiency (🟡 IMPORTANT)

- [ ] **Avoid multiple enumerations**: Call `.ToList()` if enumerating multiple times
- [ ] **Use appropriate methods**: `.Any()` instead of `.Count() > 0`, `.First()` instead of `.Where().First()`
- [ ] **Avoid Select followed by Count**: Use `.Count()` directly if only counting
- [ ] **Deferred execution understood**: Know when LINQ queries execute
- [ ] **AsNoTracking for read-only**: Use `.AsNoTracking()` for EF Core queries that don't need change tracking

### Example

```csharp
// 🟡 IMPORTANT: Multiple enumerations (queries database multiple times)
public void ProcessCustomers()
{
    var query = context.Customers.Where(c => c.IsActive);
    
    var count = query.Count(); // Query 1
    var first = query.FirstOrDefault(); // Query 2
    var list = query.ToList(); // Query 3
}

// ✅ CORRECT: Single enumeration
public void ProcessCustomers()
{
    var customers = context.Customers
        .Where(c => c.IsActive)
        .ToList(); // Single query
    
    var count = customers.Count;
    var first = customers.FirstOrDefault();
}

// 🟢 SUGGESTION: Inefficient existence check
public bool HasActiveCustomers()
{
    return context.Customers.Where(c => c.IsActive).Count() > 0; // Counts ALL
}

// ✅ CORRECT: Use .Any() for existence check
public bool HasActiveCustomers()
{
    return context.Customers.Any(c => c.IsActive); // Stops at first match
}

// 🟡 IMPORTANT: Missing AsNoTracking for read-only
public async Task<List<CustomerModel>> GetAllCustomersAsync(CancellationToken cancellationToken)
{
    return await context.Customers
        .Select(c => new CustomerModel { /* ... */ })
        .ToListAsync(cancellationToken); // Tracked even though read-only
}

// ✅ CORRECT: AsNoTracking for read-only queries
public async Task<List<CustomerModel>> GetAllCustomersAsync(CancellationToken cancellationToken)
{
    return await context.Customers
        .AsNoTracking()
        .Select(c => new CustomerModel { /* ... */ })
        .ToListAsync(cancellationToken); // Not tracked, faster
}
```

**Why it matters**: Inefficient LINQ patterns can cause multiple database queries, track unnecessary entities, or perform more work than needed.

## Database Query Optimization (🟡 IMPORTANT)

- [ ] **No N+1 queries**: Related entities loaded with `.Include()` or projections
- [ ] **Projections used**: Select only needed columns (`.Select()`)
- [ ] **Pagination implemented**: Use `.Skip()` and `.Take()` for large result sets
- [ ] **Indexes appropriate**: Database indexes exist for filtered/sorted columns
- [ ] **Avoid Select N+1**: Don't query in loops

### Example

```csharp
// 🔴 CRITICAL: N+1 query problem
public async Task<List<CustomerViewModel>> GetCustomersAsync()
{
    var customers = await context.Customers.ToListAsync();
    
    var viewModels = new List<CustomerViewModel>();
    foreach (var customer in customers)
    {
        var orders = await context.Orders
            .Where(o => o.CustomerId == customer.Id)
            .ToListAsync(); // Separate query for EACH customer!
        
        viewModels.Add(new CustomerViewModel 
        { 
            Customer = customer, 
            OrderCount = orders.Count 
        });
    }
    return viewModels;
}

// ✅ CORRECT: Single query with Include
public async Task<List<CustomerViewModel>> GetCustomersAsync()
{
    return await context.Customers
        .Include(c => c.Orders) // Single query with JOIN
        .Select(c => new CustomerViewModel
        {
            Customer = c,
            OrderCount = c.Orders.Count
        })
        .ToListAsync();
}

// 🟡 IMPORTANT: Loading entire entities when only need few columns
public async Task<List<CustomerListItem>> GetCustomerListAsync()
{
    var customers = await context.Customers.ToListAsync(); // Loads ALL columns
    return customers.Select(c => new CustomerListItem
    {
        Id = c.Id,
        Name = c.Name
    }).ToList();
}

// ✅ CORRECT: Projection to load only needed columns
public async Task<List<CustomerListItem>> GetCustomerListAsync()
{
    return await context.Customers
        .Select(c => new CustomerListItem
        {
            Id = c.Id,
            Name = c.Name
        })
        .ToListAsync(); // Only loads Id and Name columns
}
```

**Why it matters**: N+1 queries cause severe performance degradation. A page with 100 customers would execute 101 database queries instead of 1.

## Caching (🟢 SUGGESTION)

- [ ] **Expensive operations cached**: Repeated expensive calculations cached
- [ ] **Cache invalidation strategy**: Clear cache when data changes
- [ ] **Appropriate cache duration**: TTL set based on data volatility
- [ ] **IMemoryCache or IDistributedCache**: Use ASP.NET Core caching abstractions
- [ ] **Cache keys unique**: Avoid key collisions

### Example

```csharp
// 🟢 SUGGESTION: Repeated expensive calculation
public class ReportService
{
    public async Task<decimal> GetTotalRevenueAsync()
    {
        return await context.Orders
            .Where(o => o.Status == OrderStatus.Completed)
            .SumAsync(o => o.Total); // Expensive query every time
    }
}

// ✅ CORRECT: Cached with IMemoryCache
public class ReportService
{
    private readonly IMemoryCache cache;
    private readonly ApplicationDbContext context;
    
    public ReportService(IMemoryCache cache, ApplicationDbContext context)
    {
        this.cache = cache;
        this.context = context;
    }
    
    public async Task<decimal> GetTotalRevenueAsync()
    {
        var cacheKey = "TotalRevenue";
        
        if (this.cache.TryGetValue(cacheKey, out decimal cachedRevenue))
        {
            return cachedRevenue;
        }
        
        var revenue = await context.Orders
            .Where(o => o.Status == OrderStatus.Completed)
            .SumAsync(o => o.Total);
        
        this.cache.Set(cacheKey, revenue, TimeSpan.FromMinutes(5));
        
        return revenue;
    }
}
```

**Why it matters**: Caching reduces database load and improves response times for frequently accessed data.

## Summary

Performance checklist ensures efficient, scalable code:

✅ **Async for I/O** (🟡 IMPORTANT - database, file, network operations)  
✅ **No blocking on async** (🔴 CRITICAL - avoid `.Result`, `.Wait()`)  
✅ **CancellationToken propagated** (🟡 IMPORTANT - enable cancellation)  
✅ **Resources disposed** (🔴 CRITICAL - `using` statements for IDisposable)  
✅ **Efficient string operations** (🟢 SUGGESTION - StringBuilder, Span<T>)  
✅ **Appropriate collections** (🟢 SUGGESTION - Dictionary for lookups, HashSet for uniqueness)  
✅ **Efficient LINQ** (🟡 IMPORTANT - .Any() vs .Count(), AsNoTracking)  
✅ **Optimized database queries** (🟡 IMPORTANT - Include to prevent N+1, projections)  
✅ **Caching where appropriate** (🟢 SUGGESTION - IMemoryCache for expensive operations)  

**Quick performance check**:
- Are async methods awaited (not .Result/.Wait())? ✅
- Are IDisposable objects in using statements? ✅
- Are there N+1 query patterns? ❌
- Is StringBuilder used for string concatenation in loops? ✅

**Reference**: See `examples/clean-code-examples.md` for additional performance patterns.
