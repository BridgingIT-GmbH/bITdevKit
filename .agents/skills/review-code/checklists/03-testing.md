# Testing Checklist

Use this checklist to evaluate test coverage and quality in C#/.NET projects using xUnit, Shouldly, and NSubstitute.

## Test Coverage (🟡 IMPORTANT)

- [ ] **Critical paths tested**: Business logic has > 80% code coverage
- [ ] **New features have tests**: Every new feature includes tests
- [ ] **Public APIs fully tested**: All public methods/properties covered
- [ ] **Edge cases tested**: Null, empty, boundary conditions covered
- [ ] **Error paths tested**: Exception and error handling verified
- [ ] **Integration tests exist**: Key workflows have end-to-end tests

### Coverage Goals

| Code Type | Minimum Coverage | Ideal Coverage |
|-----------|------------------|----------------|
| Domain logic (aggregates, value objects) | 90% | 95%+ |
| Application handlers (commands, queries) | 80% | 90% |
| API endpoints (integration tests) | 70% | 85% |
| Infrastructure (repositories, EF config) | 60% | 75% |
| Utilities and extensions | 80% | 90% |

**Why it matters**: High test coverage catches regressions early, enables confident refactoring, and documents expected behavior.

**How to check**:
```bash
# Run tests with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Generate coverage report
reportgenerator -reports:coverage.opencover.xml -targetdir:coveragereport
```

## Test Naming (🟡 IMPORTANT)

**Pattern**: Use descriptive names that explain what is being tested, under what conditions, and what the expected outcome is.

- [ ] **Recommended pattern**: `Should_ExpectedBehavior_When_Condition`
- [ ] **Alternative pattern**: `MethodName_Scenario_ExpectedOutcome`
- [ ] **Descriptive names**: Test name clearly describes what's being verified
- [ ] **No generic names**: Avoid `Test1`, `TestMethod`, `BasicTest`
- [ ] **Consistent across project**: Same naming pattern used throughout

### Example

```csharp
// 🔴 WRONG: Unclear test names
[Fact]
public void Test1() { }

[Fact]
public void TestCustomer() { }

[Fact]
public void ValidateEmail() { }

// ✅ CORRECT: Descriptive test names (Should pattern)
[Fact]
public void Should_ReturnCustomer_When_CustomerExists() { }

[Fact]
public void Should_ReturnFailure_When_EmailIsInvalid() { }

[Fact]
public void Should_ThrowException_When_CustomerIdIsEmpty() { }

// ✅ ALSO CORRECT: Method_Scenario_Outcome pattern
[Fact]
public void CreateCustomer_WithValidData_ReturnsSuccess() { }

[Fact]
public void CreateCustomer_WithInvalidEmail_ReturnsValidationError() { }

[Fact]
public void DeleteCustomer_WhenNotFound_ReturnsNotFoundError() { }
```

**Why it matters**: Good test names serve as documentation. You should understand what failed just by reading the test name.

## AAA Pattern (🟡 IMPORTANT)

**Pattern**: Arrange-Act-Assert - organize tests into three clear sections.

- [ ] **Arrange section**: Setup test data and dependencies
- [ ] **Act section**: Execute the operation being tested
- [ ] **Assert section**: Verify the outcome
- [ ] **Clear separation**: Blank lines between sections
- [ ] **Single act**: Only one action per test (usually)
- [ ] **Focused asserts**: Assert specific expected outcomes

### Example

```csharp
// 🔴 WRONG: No clear structure
[Fact]
public async Task TestCustomerCreation()
{
    var repository = Substitute.For<IGenericRepository<Customer>>();
    var mapper = Substitute.For<IMapper>();
    var handler = new CustomerCreateCommandHandler(repository, mapper);
    var command = new CustomerCreateCommand(new CustomerModel 
    { 
        FirstName = "John", 
        LastName = "Doe", 
        Email = "john@example.com" 
    });
    var result = await handler.Handle(command, CancellationToken.None);
    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldNotBeNull();
    await repository.Received(1).InsertAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>());
}

// ✅ CORRECT: Clear AAA structure
[Fact]
public async Task Should_CreateCustomer_When_ValidDataProvided()
{
    // Arrange
    var repository = Substitute.For<IGenericRepository<Customer>>();
    var mapper = Substitute.For<IMapper>();
    mapper.Map<CustomerModel>(Arg.Any<Customer>())
        .Returns(new CustomerModel { Id = Guid.NewGuid(), FirstName = "John", LastName = "Doe" });
    
    var handler = new CustomerCreateCommandHandler(repository, mapper);
    var command = new CustomerCreateCommand(new CustomerModel 
    { 
        FirstName = "John", 
        LastName = "Doe", 
        Email = "john@example.com" 
    });
    
    // Act
    var result = await handler.Handle(command, CancellationToken.None);
    
    // Assert
    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldNotBeNull();
    result.Value.FirstName.ShouldBe("John");
    result.Value.LastName.ShouldBe("Doe");
    
    await repository.Received(1).InsertAsync(
        Arg.Is<Customer>(c => c.FirstName == "John" && c.LastName == "Doe"), 
        Arg.Any<CancellationToken>()
    );
}
```

**Why it matters**: AAA structure makes tests easy to read and understand. Readers immediately see what's being set up, what action is performed, and what's being verified.

## Test Independence (🔴 CRITICAL)

**Rule**: Each test must run independently without relying on other tests or shared state.

- [ ] **No shared mutable state**: Tests don't share static variables or fields
- [ ] **No test order dependencies**: Tests pass regardless of execution order
- [ ] **Fresh data per test**: Each test creates its own test data
- [ ] **Cleanup not required**: Tests don't need explicit cleanup (use `IDisposable` if needed)
- [ ] **Parallel execution safe**: Tests can run in parallel without conflicts

### Example

```csharp
// 🔴 CRITICAL: Shared state between tests (WRONG)
public class CustomerServiceTests
{
    private static List<Customer> customers = new();
    
    [Fact]
    public void Test1_AddCustomer()
    {
        var customer = new Customer { Id = Guid.NewGuid(), Name = "John" };
        customers.Add(customer);
        customers.Count.ShouldBe(1);
    }
    
    [Fact]
    public void Test2_CustomerCountIsTwo()
    {
        // This test depends on Test1 running first!
        var customer = new Customer { Id = Guid.NewGuid(), Name = "Jane" };
        customers.Add(customer);
        customers.Count.ShouldBe(2); // Fails if Test1 didn't run or if tests run in parallel
    }
}

// ✅ CORRECT: Independent tests
public class CustomerServiceTests
{
    [Fact]
    public void Should_AddCustomer_When_ValidData()
    {
        // Arrange - fresh data for this test
        var customers = new List<Customer>();
        var customer = new Customer { Id = Guid.NewGuid(), Name = "John" };
        
        // Act
        customers.Add(customer);
        
        // Assert
        customers.Count.ShouldBe(1);
        customers[0].Name.ShouldBe("John");
    }
    
    [Fact]
    public void Should_ReturnMultipleCustomers_When_MultipleAdded()
    {
        // Arrange - fresh data for this test (independent of previous test)
        var customers = new List<Customer>
        {
            new Customer { Id = Guid.NewGuid(), Name = "John" },
            new Customer { Id = Guid.NewGuid(), Name = "Jane" }
        };
        
        // Act & Assert
        customers.Count.ShouldBe(2);
    }
}

// ✅ ALSO CORRECT: Fixture with IDisposable for cleanup
public class DatabaseTests : IDisposable
{
    private readonly ApplicationDbContext context;
    
    public DatabaseTests()
    {
        // Fresh database context per test
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        this.context = new ApplicationDbContext(options);
    }
    
    [Fact]
    public async Task Should_SaveCustomer_When_Valid()
    {
        // Arrange
        var customer = new Customer { Name = "John" };
        
        // Act
        await this.context.Customers.AddAsync(customer);
        await this.context.SaveChangesAsync();
        
        // Assert
        var saved = await this.context.Customers.FirstOrDefaultAsync();
        saved.ShouldNotBeNull();
        saved.Name.ShouldBe("John");
    }
    
    public void Dispose()
    {
        this.context?.Dispose();
    }
}
```

**Why it matters**: Tests with shared state or execution order dependencies are unreliable, difficult to debug, and fail intermittently (especially in parallel execution).

## Edge Cases (🟡 IMPORTANT)

- [ ] **Null inputs tested**: Methods handle null arguments appropriately
- [ ] **Empty collections tested**: Methods work with empty arrays/lists
- [ ] **Empty strings tested**: String parameters handle "", " ", null
- [ ] **Guid.Empty tested**: GUID parameters checked for Guid.Empty
- [ ] **Boundary values tested**: Min/max values, zero, negative numbers
- [ ] **Invalid formats tested**: Malformed emails, dates, URLs
- [ ] **Large inputs tested**: Very long strings, large collections

### Example

```csharp
// ✅ CORRECT: Comprehensive edge case testing
public class EmailAddressTests
{
    [Fact]
    public void Should_ReturnFailure_When_EmailIsNull()
    {
        // Act
        var result = EmailAddress.Create(null);
        
        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Messages.ShouldContain(m => m.Contains("email", StringComparison.OrdinalIgnoreCase));
    }
    
    [Fact]
    public void Should_ReturnFailure_When_EmailIsEmpty()
    {
        // Act
        var result = EmailAddress.Create("");
        
        // Assert
        result.IsFailure.ShouldBeTrue();
    }
    
    [Fact]
    public void Should_ReturnFailure_When_EmailIsWhitespace()
    {
        // Act
        var result = EmailAddress.Create("   ");
        
        // Assert
        result.IsFailure.ShouldBeTrue();
    }
    
    [Theory]
    [InlineData("notanemail")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("user @example.com")]
    public void Should_ReturnFailure_When_EmailFormatIsInvalid(string email)
    {
        // Act
        var result = EmailAddress.Create(email);
        
        // Assert
        result.IsFailure.ShouldBeTrue();
    }
    
    [Fact]
    public void Should_ReturnSuccess_When_EmailIsValid()
    {
        // Act
        var result = EmailAddress.Create("user@example.com");
        
        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe("user@example.com");
    }
}
```

**Why it matters**: Edge cases are where bugs hide. Thorough edge case testing prevents production issues.

## Mocking with NSubstitute (🟢 SUGGESTION)

- [ ] **Dependencies mocked**: External dependencies (repositories, services) are mocked
- [ ] **NSubstitute syntax**: Use `Substitute.For<T>()` for mocks
- [ ] **Return values configured**: Mock methods return appropriate values
- [ ] **Interactions verified**: Verify method calls with `.Received()` when appropriate
- [ ] **Argument matching**: Use `Arg.Any<T>()` or `Arg.Is<T>()` for argument validation
- [ ] **Mock behavior realistic**: Mocks simulate real behavior (return realistic data)

### Example

```csharp
// ✅ CORRECT: Mocking with NSubstitute
public class CustomerCreateCommandHandlerTests
{
    [Fact]
    public async Task Should_CreateCustomer_When_ValidDataProvided()
    {
        // Arrange - Mock repository
        var repository = Substitute.For<IGenericRepository<Customer>>();
        repository.InsertAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        
        // Arrange - Mock mapper
        var mapper = Substitute.For<IMapper>();
        mapper.Map<CustomerModel>(Arg.Any<Customer>())
            .Returns(new CustomerModel 
            { 
                Id = Guid.NewGuid(), 
                FirstName = "John", 
                LastName = "Doe",
                Email = "john@example.com"
            });
        
        var handler = new CustomerCreateCommandHandler(repository, mapper);
        var command = new CustomerCreateCommand(new CustomerModel
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com"
        });
        
        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        
        // Assert - Verify result
        result.IsSuccess.ShouldBeTrue();
        result.Value.FirstName.ShouldBe("John");
        
        // Assert - Verify repository interaction
        await repository.Received(1).InsertAsync(
            Arg.Is<Customer>(c => 
                c.FirstName == "John" && 
                c.LastName == "Doe" && 
                c.Email.Value == "john@example.com"
            ),
            Arg.Any<CancellationToken>()
        );
    }
    
    [Fact]
    public async Task Should_ReturnFailure_When_EmailIsInvalid()
    {
        // Arrange
        var repository = Substitute.For<IGenericRepository<Customer>>();
        var mapper = Substitute.For<IMapper>();
        var handler = new CustomerCreateCommandHandler(repository, mapper);
        var command = new CustomerCreateCommand(new CustomerModel
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "invalid-email"
        });
        
        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.IsFailure.ShouldBeTrue();
        
        // Verify repository was NOT called (because validation failed)
        await repository.DidNotReceive().InsertAsync(
            Arg.Any<Customer>(),
            Arg.Any<CancellationToken>()
        );
    }
}
```

**Why it matters**: Mocking isolates the unit under test from external dependencies, making tests fast, reliable, and focused.

## xUnit Patterns (🟢 SUGGESTION)

- [ ] **[Fact] for single tests**: Use `[Fact]` for tests with no parameters
- [ ] **[Theory] for parameterized tests**: Use `[Theory]` with `[InlineData]` for multiple similar cases
- [ ] **IClassFixture for shared context**: Use `IClassFixture<T>` for expensive setup shared across tests
- [ ] **ICollectionFixture for shared context across classes**: Use for database context, etc.
- [ ] **IDisposable for cleanup**: Implement `IDisposable` for test cleanup

### Example

```csharp
// ✅ CORRECT: [Fact] for single test
[Fact]
public void Should_ReturnSuccess_When_EmailIsValid()
{
    var result = EmailAddress.Create("user@example.com");
    result.IsSuccess.ShouldBeTrue();
}

// ✅ CORRECT: [Theory] for parameterized tests
[Theory]
[InlineData("user@example.com", true)]
[InlineData("another@test.org", true)]
[InlineData("invalid", false)]
[InlineData("@example.com", false)]
[InlineData("user@", false)]
public void Should_ValidateEmailFormat_Correctly(string email, bool expectedValid)
{
    // Act
    var result = EmailAddress.Create(email);
    
    // Assert
    if (expectedValid)
    {
        result.IsSuccess.ShouldBeTrue();
    }
    else
    {
        result.IsFailure.ShouldBeTrue();
    }
}

// ✅ CORRECT: IClassFixture for shared setup
public class DatabaseFixture : IDisposable
{
    public ApplicationDbContext Context { get; }
    
    public DatabaseFixture()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        this.Context = new ApplicationDbContext(options);
    }
    
    public void Dispose()
    {
        this.Context?.Dispose();
    }
}

public class CustomerRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly ApplicationDbContext context;
    
    public CustomerRepositoryTests(DatabaseFixture fixture)
    {
        this.context = fixture.Context;
    }
    
    [Fact]
    public async Task Should_SaveCustomer_When_Valid()
    {
        // Test uses shared context from fixture
    }
}
```

**Why it matters**: Using xUnit patterns appropriately makes tests more maintainable and avoids repeating setup code.

## Shouldly Assertions (🟢 SUGGESTION)

- [ ] **Shouldly used over Assert**: Use Shouldly's fluent assertions
- [ ] **Descriptive assertions**: Use specific assertions (`.ShouldBe()`, `.ShouldNotBeNull()`)
- [ ] **Custom messages**: Add custom messages for complex assertions
- [ ] **Collection assertions**: Use `.ShouldContain()`, `.ShouldBeEmpty()`, `.ShouldNotBeEmpty()`
- [ ] **Exception assertions**: Use `.ShouldThrow<T>()` for exception testing

### Example

```csharp
// 🔴 WRONG: xUnit Assert (less readable failures)
[Fact]
public void Test()
{
    var customer = GetCustomer();
    Assert.NotNull(customer);
    Assert.Equal("John", customer.FirstName);
    Assert.True(customer.IsActive);
    Assert.Contains(customer.Addresses, a => a.IsPrimary);
}

// ✅ CORRECT: Shouldly assertions (readable failure messages)
[Fact]
public void Should_ReturnActiveCustomer_When_CustomerExists()
{
    // Act
    var customer = GetCustomer();
    
    // Assert
    customer.ShouldNotBeNull();
    customer.FirstName.ShouldBe("John");
    customer.IsActive.ShouldBeTrue();
    customer.Addresses.ShouldContain(a => a.IsPrimary);
}

// ✅ CORRECT: Shouldly with custom messages
[Fact]
public void Should_CalculateTotalCorrectly()
{
    // Act
    var total = CalculateOrderTotal(order);
    
    // Assert
    total.ShouldBe(150.00m, "Order total should be sum of items plus tax");
}

// ✅ CORRECT: Exception assertions
[Fact]
public void Should_ThrowException_When_CustomerIdIsEmpty()
{
    // Act & Assert
    Should.Throw<ArgumentException>(() => 
        new Customer(Guid.Empty, "John", "Doe")
    );
}
```

**Why it matters**: Shouldly provides more readable failure messages, making it easier to understand what went wrong when a test fails.

## Integration Tests (🟡 IMPORTANT)

- [ ] **Key workflows tested**: End-to-end tests for critical user journeys
- [ ] **WebApplicationFactory used**: Use `WebApplicationFactory<T>` for API tests
- [ ] **Database isolation**: Each test uses isolated database (in-memory or separate instance)
- [ ] **Realistic scenarios**: Tests simulate actual usage patterns
- [ ] **HTTP requests tested**: Test actual HTTP endpoints, not just handlers

### Example

```csharp
// ✅ CORRECT: Integration test with WebApplicationFactory
public class CustomerEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient client;
    
    public CustomerEndpointsTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        this.client = factory.CreateClient();
    }
    
    [Fact]
    public async Task Should_CreateCustomer_When_ValidRequest()
    {
        // Arrange
        var request = new 
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com"
        };
        
        // Act
        var response = await this.client.PostAsJsonAsync("/api/customers", request);
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        
        var customer = await response.Content.ReadFromJsonAsync<CustomerModel>();
        customer.ShouldNotBeNull();
        customer.FirstName.ShouldBe("John");
        customer.LastName.ShouldBe("Doe");
        customer.Email.ShouldBe("john@example.com");
        
        response.Headers.Location.ShouldNotBeNull();
    }
    
    [Fact]
    public async Task Should_ReturnBadRequest_When_EmailIsInvalid()
    {
        // Arrange
        var request = new 
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "invalid-email"
        };
        
        // Act
        var response = await this.client.PostAsJsonAsync("/api/customers", request);
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
```

**Why it matters**: Integration tests catch issues that unit tests miss, such as routing problems, serialization issues, and database configuration errors.

## Summary

Testing checklist ensures comprehensive, maintainable tests:

✅ **Adequate coverage** (🟡 IMPORTANT - > 80% for critical paths, new features tested)  
✅ **Clear naming** (🟡 IMPORTANT - `Should_ExpectedBehavior_When_Condition`)  
✅ **AAA structure** (🟡 IMPORTANT - Arrange-Act-Assert with clear separation)  
✅ **Test independence** (🔴 CRITICAL - no shared state, can run in any order)  
✅ **Edge cases covered** (🟡 IMPORTANT - null, empty, boundary, invalid inputs)  
✅ **Dependencies mocked** (🟢 SUGGESTION - NSubstitute for isolation)  
✅ **xUnit patterns** (🟢 SUGGESTION - Fact, Theory, fixtures)  
✅ **Shouldly assertions** (🟢 SUGGESTION - readable, fluent assertions)  
✅ **Integration tests** (🟡 IMPORTANT - WebApplicationFactory for API tests)  

**Quick test quality check**:
- Can you understand what's tested by reading the name? ✅
- Can tests run in any order? ✅
- Are assertions specific and clear? ✅
- Are edge cases covered? ✅

**Reference**: See `examples/testing-examples.md` for detailed WRONG vs CORRECT examples.
