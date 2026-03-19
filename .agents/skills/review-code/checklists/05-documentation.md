# Documentation Checklist

Use this checklist to ensure code is properly documented with XML comments, inline comments, and supporting documentation.

## XML Documentation for Public APIs (🔴 CRITICAL for public APIs)

**Rule**: All public classes, methods, properties, and parameters **must** have XML documentation comments.

- [ ] **<summary> on all public members**: Classes, methods, properties have `<summary>` tags
- [ ] **<param> for all parameters**: Method parameters documented with `<param>` tags
- [ ] **<returns> for return values**: Methods returning values have `<returns>` tag
- [ ] **<exception> for thrown exceptions**: Exceptions documented with `<exception>` tag
- [ ] **<remarks> for additional details**: Complex behavior explained in `<remarks>`
- [ ] **<example> for complex APIs**: Usage examples provided for non-obvious APIs
- [ ] **<see cref> for references**: Links to related types/members using `<see cref="T"/>`

### Example

```csharp
// 🔴 CRITICAL: No XML documentation on public API
public class CustomerService
{
    public async Task<Result<Customer>> CreateCustomerAsync(string firstName, string lastName, string email)
    {
        // Implementation
    }
}

// ✅ CORRECT: Complete XML documentation
/// <summary>
/// Provides services for managing customer data and business operations.
/// </summary>
public class CustomerService
{
    /// <summary>
    /// Creates a new customer with the specified details after validation.
    /// </summary>
    /// <param name="firstName">The customer's first name. Must not be empty.</param>
    /// <param name="lastName">The customer's last name. Must not be empty.</param>
    /// <param name="email">The customer's email address. Must be a valid email format.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Result{Customer}"/> containing the created customer if successful,
    /// or validation errors if the input is invalid.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any required parameter is null.
    /// </exception>
    /// <remarks>
    /// This method performs the following validations:
    /// - Email format validation
    /// - First and last name must not be empty
    /// - Email must be unique in the system
    /// </remarks>
    /// <example>
    /// <code>
    /// var result = await customerService.CreateCustomerAsync(
    ///     "John", 
    ///     "Doe", 
    ///     "john@example.com",
    ///     cancellationToken
    /// );
    /// 
    /// if (result.IsSuccess)
    /// {
    ///     Console.WriteLine($"Customer created: {result.Value.Id}");
    /// }
    /// </code>
    /// </example>
    public async Task<Result<Customer>> CreateCustomerAsync(
        string firstName, 
        string lastName, 
        string email,
        CancellationToken cancellationToken = default)
    {
        // Implementation
    }
}
```

**Why it matters**: XML documentation:
- Enables IntelliSense in IDEs
- Generates API documentation automatically
- Helps other developers understand usage without reading implementation
- Serves as a contract for public APIs

**When to apply**:
- ✅ **Always**: Public classes, methods, properties, events
- ✅ **Usually**: Protected members (if API is extensible)
- ❌ **Rarely**: Private/internal members (unless complex)

## Property Documentation (🟡 IMPORTANT)

- [ ] **<summary> on public properties**: All public properties documented
- [ ] **Explain purpose**: What the property represents
- [ ] **Document constraints**: Valid ranges, required values, format
- [ ] **Nullable documentation**: Document when null is valid

### Example

```csharp
// 🟡 IMPORTANT: Public properties without documentation
public class Customer
{
    public Guid Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public EmailAddress Email { get; set; }
    public CustomerStatus Status { get; set; }
    public DateTime? LastOrderDate { get; set; }
}

// ✅ CORRECT: Documented properties
/// <summary>
/// Represents a customer aggregate root in the domain model.
/// </summary>
public class Customer
{
    /// <summary>
    /// Gets the unique identifier for this customer.
    /// </summary>
    public Guid Id { get; private set; }
    
    /// <summary>
    /// Gets the customer's first name.
    /// Must not be empty or whitespace.
    /// </summary>
    public string FirstName { get; private set; }
    
    /// <summary>
    /// Gets the customer's last name.
    /// Must not be empty or whitespace.
    /// </summary>
    public string LastName { get; private set; }
    
    /// <summary>
    /// Gets the customer's email address as a validated value object.
    /// Email format is validated when set.
    /// </summary>
    public EmailAddress Email { get; private set; }
    
    /// <summary>
    /// Gets the current status of the customer.
    /// Default value is <see cref="CustomerStatus.Lead"/>.
    /// </summary>
    public CustomerStatus Status { get; private set; }
    
    /// <summary>
    /// Gets the date and time of the customer's most recent order.
    /// Returns <c>null</c> if the customer has never placed an order.
    /// </summary>
    public DateTime? LastOrderDate { get; private set; }
}
```

**Why it matters**: Property documentation explains what the property represents, its constraints, and when it can be null.

## Constructor Documentation (🟡 IMPORTANT)

- [ ] **<summary> on public constructors**: Purpose of constructor documented
- [ ] **<param> for all parameters**: Each constructor parameter explained
- [ ] **Factory method preferred**: Document why if using factory instead of public constructor

### Example

```csharp
// 🟡 IMPORTANT: Constructor without documentation
public class Customer
{
    private Customer() { }
    
    private Customer(string firstName, string lastName, EmailAddress email)
    {
        this.FirstName = firstName;
        this.LastName = lastName;
        this.Email = email;
    }
}

// ✅ CORRECT: Documented constructors
/// <summary>
/// Represents a customer aggregate root in the domain model.
/// </summary>
public class Customer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Customer"/> class.
    /// Private parameterless constructor for ORM use only.
    /// Use <see cref="Create"/> factory method to create new customers.
    /// </summary>
    private Customer() { }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="Customer"/> class
    /// with the specified personal details and email address.
    /// </summary>
    /// <param name="firstName">The customer's first name. Must not be empty.</param>
    /// <param name="lastName">The customer's last name. Must not be empty.</param>
    /// <param name="email">The validated email address value object.</param>
    private Customer(string firstName, string lastName, EmailAddress email)
    {
        this.FirstName = firstName;
        this.LastName = lastName;
        this.Email = email;
    }
    
    /// <summary>
    /// Creates a new <see cref="Customer"/> instance with validation.
    /// </summary>
    /// <param name="firstName">The customer's first name.</param>
    /// <param name="lastName">The customer's last name.</param>
    /// <param name="email">The email address string to validate and convert.</param>
    /// <returns>
    /// A <see cref="Result{Customer}"/> containing the created customer if valid,
    /// or validation errors if any input is invalid.
    /// </returns>
    public static Result<Customer> Create(string firstName, string lastName, string email)
    {
        // Implementation
    }
}
```

**Why it matters**: Constructor documentation explains how to create instances and why certain constructors are private.

## Exception Documentation (🟡 IMPORTANT)

- [ ] **<exception> tags for thrown exceptions**: Document all exceptions that can be thrown
- [ ] **Explain when thrown**: Conditions that trigger the exception
- [ ] **Include exception type**: Use `<exception cref="ExceptionType">`

### Example

```csharp
// 🟡 IMPORTANT: Exceptions not documented
public class CustomerRepository
{
    public async Task<Customer> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Customer ID cannot be empty", nameof(id));
        
        var customer = await context.Customers.FindAsync(new object[] { id }, cancellationToken);
        
        if (customer == null)
            throw new NotFoundException($"Customer with ID {id} not found");
        
        return customer;
    }
}

// ✅ CORRECT: Exceptions documented
/// <summary>
/// Repository for managing customer data persistence.
/// </summary>
public class CustomerRepository
{
    /// <summary>
    /// Retrieves a customer by their unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the customer to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The customer with the specified ID.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="id"/> is <see cref="Guid.Empty"/>.
    /// </exception>
    /// <exception cref="NotFoundException">
    /// Thrown when no customer exists with the specified ID.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    public async Task<Customer> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Customer ID cannot be empty", nameof(id));
        
        var customer = await context.Customers.FindAsync(new object[] { id }, cancellationToken);
        
        if (customer == null)
            throw new NotFoundException($"Customer with ID {id} not found");
        
        return customer;
    }
}
```

**Why it matters**: Exception documentation helps callers understand what can go wrong and how to handle errors.

## Complex Logic Comments (🟡 IMPORTANT)

**Rule**: Explain WHY, not WHAT. Code should be self-documenting for WHAT it does.

- [ ] **Business rules explained**: Document business logic reasoning
- [ ] **Non-obvious algorithms**: Explain complex algorithms or calculations
- [ ] **Performance optimizations**: Document why optimization was needed
- [ ] **Workarounds documented**: Explain hacks or workarounds with references
- [ ] **TODOs tracked**: TODO comments reference issue tracker

### Example

```csharp
// 🔴 WRONG: Comments explain WHAT the code does (redundant)
public decimal CalculateDiscount(Customer customer)
{
    // Get the customer's order count
    var orderCount = customer.OrderCount;
    
    // Check if order count is greater than 10
    if (orderCount > 10)
    {
        // Return 15% discount
        return 0.15m;
    }
    
    // Return 5% discount
    return 0.05m;
}

// ✅ CORRECT: Comments explain WHY (business context)
public decimal CalculateDiscount(Customer customer)
{
    // Business rule: Customers with more than 10 orders are considered "loyal customers"
    // and receive a higher discount rate per marketing policy dated 2024-01-15.
    // See: https://company.com/policies/customer-loyalty
    const int LOYAL_CUSTOMER_THRESHOLD = 10;
    const decimal LOYAL_DISCOUNT_RATE = 0.15m;
    const decimal STANDARD_DISCOUNT_RATE = 0.05m;
    
    return customer.OrderCount > LOYAL_CUSTOMER_THRESHOLD 
        ? LOYAL_DISCOUNT_RATE 
        : STANDARD_DISCOUNT_RATE;
}

// ✅ CORRECT: Performance optimization documented
public async Task<List<Customer>> GetActiveCustomersAsync()
{
    // Performance: Use AsNoTracking because we're only reading data for display.
    // This reduces memory usage by ~40% and query time by ~25% for large result sets
    // based on profiling data. See performance report: PERF-2024-001
    return await context.Customers
        .AsNoTracking()
        .Where(c => c.Status == CustomerStatus.Active)
        .ToListAsync();
}

// ✅ CORRECT: Workaround documented with issue reference
public string FormatPhoneNumber(string phone)
{
    // WORKAROUND: PhoneNumberUtil doesn't handle legacy format correctly.
    // Issue tracked: https://github.com/company/project/issues/1234
    // TODO: Remove this once PhoneNumberUtil is updated to v2.0
    if (phone.StartsWith("00"))
    {
        phone = "+" + phone.Substring(2);
    }
    
    return PhoneNumberUtil.Format(phone);
}
```

**Why it matters**: Good comments explain the reasoning behind decisions. The code itself shows what it does.

## README Updates (🟡 IMPORTANT)

- [ ] **New features documented**: README includes new features
- [ ] **Setup instructions current**: Installation/configuration steps up to date
- [ ] **Examples provided**: Common usage examples included
- [ ] **Breaking changes noted**: API changes documented with migration guide
- [ ] **Links working**: All links in README are valid

### What to Document in README

**Essential sections**:
- Overview (what the project does)
- Prerequisites (required tools/versions)
- Installation/Setup
- Quick Start (basic usage example)
- Configuration
- Contributing guidelines
- License

**When to update README**:
- Adding new features
- Changing setup/configuration
- Adding/removing dependencies
- Breaking API changes
- New examples or tutorials

## API Documentation (🟢 SUGGESTION)

- [ ] **OpenAPI/Swagger summaries**: Endpoints have `.WithSummary()` and `.WithDescription()`
- [ ] **Request examples**: Example request bodies provided
- [ ] **Response examples**: Example responses documented
- [ ] **Status codes documented**: All possible status codes listed with `.Produces()`
- [ ] **Authentication noted**: Security requirements documented

### Example

```csharp
// 🟢 SUGGESTION: Minimal endpoint documentation
app.MapPost("/api/customers", async (CustomerModel model, IRequester requester) =>
{
    var command = new CustomerCreateCommand(model);
    var result = await requester.SendAsync(command);
    return result.MapHttpCreated($"/api/customers/{result.Value.Id}");
});

// ✅ CORRECT: Fully documented endpoint
app.MapPost("/api/customers", async (
    CustomerModel model, 
    IRequester requester,
    CancellationToken cancellationToken) =>
{
    var command = new CustomerCreateCommand(model);
    var result = await requester.SendAsync(command, cancellationToken);
    return result.MapHttpCreated($"/api/customers/{result.Value.Id}");
})
.WithName("CreateCustomer")
.WithSummary("Creates a new customer")
.WithDescription(@"
    Creates a new customer with the provided details.
    
    **Business Rules**:
    - Email must be unique
    - First and last name are required
    - Email format must be valid
    
    **Example Request**:
    ```json
    {
      ""firstName"": ""John"",
      ""lastName"": ""Doe"",
      ""email"": ""john@example.com""
    }
    ```
")
.WithTags("Customers")
.Produces<CustomerModel>(StatusCodes.Status201Created)
.ProducesProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status409Conflict)
.RequireAuthorization();
```

**Why it matters**: Good API documentation helps consumers understand how to use your endpoints without reading code.

## TODO and FIXME Comments (🟢 SUGGESTION)

- [ ] **Clear description**: TODO explains what needs to be done
- [ ] **Issue reference**: Links to issue tracker or includes owner
- [ ] **Priority indicated**: Use FIXME for urgent, TODO for future
- [ ] **No stale TODOs**: Old TODOs are addressed or removed

### Example

```csharp
// 🔴 WRONG: Vague TODO with no context
public void ProcessOrder(Order order)
{
    // TODO: fix this
    var total = order.Items.Sum(i => i.Price);
}

// ✅ CORRECT: Clear TODO with context and reference
public void ProcessOrder(Order order)
{
    // TODO: Apply tax calculation based on customer's region.
    // Currently calculating without tax. See issue #1234 for tax requirements.
    // Priority: High (required for MVP)
    // Owner: @john-doe
    var total = order.Items.Sum(i => i.Price);
}

// ✅ CORRECT: FIXME for urgent issues
public void CalculateShipping(Order order)
{
    // FIXME: This calculation is incorrect for international orders.
    // Temporary workaround until shipping API is integrated (ETA: 2024-02-01).
    // See urgent bug report: BUG-5678
    var shipping = order.Weight * 2.5m;
}
```

**Why it matters**: Good TODO comments ensure technical debt is tracked and addressed. Vague TODOs are forgotten.

## Code Self-Documentation (🟢 SUGGESTION)

- [ ] **Descriptive names**: Variable/method names explain purpose
- [ ] **Small methods**: Methods do one thing (comments less needed)
- [ ] **Extract constants**: Magic numbers replaced with named constants
- [ ] **Guard clauses**: Early returns make intent clear
- [ ] **Pattern matching**: Use modern C# for clarity

**Best practice**: Write code so clear that comments are rarely needed. When comments are needed, they explain WHY, not WHAT.

## Summary

Documentation checklist ensures code is understandable:

✅ **XML documentation on public APIs** (🔴 CRITICAL - `<summary>`, `<param>`, `<returns>`, `<exception>`)  
✅ **Properties documented** (🟡 IMPORTANT - purpose, constraints, nullable)  
✅ **Constructors documented** (🟡 IMPORTANT - especially private/factory patterns)  
✅ **Exceptions documented** (🟡 IMPORTANT - when thrown, conditions)  
✅ **Complex logic explained** (🟡 IMPORTANT - WHY not WHAT, business rules)  
✅ **README current** (🟡 IMPORTANT - features, setup, examples)  
✅ **API endpoints documented** (🟢 SUGGESTION - OpenAPI summaries, examples)  
✅ **TODOs tracked** (🟢 SUGGESTION - clear, referenced, prioritized)  
✅ **Self-documenting code** (🟢 SUGGESTION - descriptive names, small methods)  

**Quick documentation check**:
- Do public APIs have XML comments? ✅
- Do comments explain WHY (not WHAT)? ✅
- Is README up to date? ✅
- Are TODOs tracked? ✅

**XML Documentation Standards**:
- Always: `<summary>` on public members
- Always: `<param>` for all parameters
- Always: `<returns>` for return values
- Important: `<exception>` for thrown exceptions
- Helpful: `<example>` for complex APIs
- Optional: `<remarks>` for additional context

**Reference**: See project README and existing documented code for examples.
