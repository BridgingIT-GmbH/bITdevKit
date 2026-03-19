# Testing Examples

This file contains WRONG vs CORRECT examples for testing patterns in C#/.NET using xUnit, Shouldly, and NSubstitute.

## Test Naming (🟡 IMPORTANT)

**Rule**: Test names should clearly describe what is being tested, the scenario, and the expected outcome.

### Example 1: Poor Test Names

```csharp
// 🔴 WRONG: Unclear test names
public class CustomerTests
{
    [Fact]
    public void Test1() { }
    
    [Fact]
    public void TestCustomer() { }
    
    [Fact]
    public void TestMethod() { }
    
    [Fact]
    public void Validate() { }
}
```

### Example 2: Descriptive Test Names (Should Pattern)

```csharp
// ✅ CORRECT: Should_ExpectedBehavior_When_Condition pattern
public class CustomerTests
{
    [Fact]
    public void Should_ReturnCustomer_When_CustomerExists() { }
    
    [Fact]
    public void Should_ReturnFailure_When_EmailIsInvalid() { }
    
    [Fact]
    public void Should_ThrowException_When_CustomerIdIsEmpty() { }
    
    [Fact]
    public void Should_CreateCustomer_When_ValidDataProvided() { }
}
```

### Example 3: Alternative Naming Pattern

```csharp
// ✅ ALSO CORRECT: MethodName_Scenario_ExpectedOutcome pattern
public class CustomerServiceTests
{
    [Fact]
    public void CreateCustomer_WithValidData_ReturnsSuccess() { }
    
    [Fact]
    public void CreateCustomer_WithInvalidEmail_ReturnsValidationError() { }
    
    [Fact]
    public void DeleteCustomer_WhenNotFound_ReturnsNotFoundError() { }
    
    [Fact]
    public void GetCustomer_WhenExists_ReturnsCustomerModel() { }
}
```

**Why it matters**: You should understand what failed just by reading the test name in the test runner output.

## AAA Pattern (🟡 IMPORTANT)

**Rule**: Organize tests into Arrange-Act-Assert sections with clear separation.

### Example 4: No Clear Structure

```csharp
// 🔴 WRONG: No clear AAA structure
[Fact]
public async Task TestCustomerCreation()
{
    var repository = Substitute.For<IGenericRepository<Customer>>();
    var mapper = Substitute.For<IMapper>();
    mapper.Map<CustomerModel>(Arg.Any<Customer>()).Returns(new CustomerModel { Id = Guid.NewGuid() });
    var handler = new CustomerCreateCommandHandler(repository, mapper);
    var command = new CustomerCreateCommand(new CustomerModel { FirstName = "John", LastName = "Doe", Email = "john@example.com" });
    var result = await handler.Handle(command, CancellationToken.None);
    result.IsSuccess.ShouldBeTrue();
    await repository.Received(1).InsertAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>());
}
```

### Example 5: Clear AAA Structure

```csharp
// ✅ CORRECT: Clear AAA structure
[Fact]
public async Task Should_CreateCustomer_When_ValidDataProvided()
{
    // Arrange
    var repository = Substitute.For<IGenericRepository<Customer>>();
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

**Why it matters**: AAA structure makes tests immediately readable. Anyone can understand what's being set up, what action is performed, and what's being verified.

## Test Independence (🔴 CRITICAL)

**Rule**: Each test must run independently without shared state or execution order dependencies.

### Example 6: Shared State (WRONG)

```csharp
// 🔴 CRITICAL: Tests share mutable state
public class CustomerServiceTests
{
    private static List<Customer> customers = new();
    
    [Fact]
    public void Test1_AddCustomer()
    {
        var customer = new Customer { Id = Guid.NewGuid(), FirstName = "John" };
        customers.Add(customer);
        customers.Count.ShouldBe(1);
    }
    
    [Fact]
    public void Test2_AddAnotherCustomer()
    {
        // This test depends on Test1 running first!
        var customer = new Customer { Id = Guid.NewGuid(), FirstName = "Jane" };
        customers.Add(customer);
        customers.Count.ShouldBe(2); // Fails if Test1 didn't run or runs after
    }
}
```

### Example 7: Independent Tests (CORRECT)

```csharp
// ✅ CORRECT: Each test has its own data
public class CustomerServiceTests
{
    [Fact]
    public void Should_AddCustomer_When_ValidData()
    {
        // Arrange - fresh data for this test only
        var customers = new List<Customer>();
        var customer = new Customer { Id = Guid.NewGuid(), FirstName = "John" };
        
        // Act
        customers.Add(customer);
        
        // Assert
        customers.Count.ShouldBe(1);
        customers[0].FirstName.ShouldBe("John");
    }
    
    [Fact]
    public void Should_HaveMultipleCustomers_When_MultipleAdded()
    {
        // Arrange - fresh data (independent of previous test)
        var customers = new List<Customer>
        {
            new Customer { Id = Guid.NewGuid(), FirstName = "John" },
            new Customer { Id = Guid.NewGuid(), FirstName = "Jane" }
        };
        
        // Act & Assert
        customers.Count.ShouldBe(2);
    }
}
```

### Example 8: Database Tests with Fixture

```csharp
// ✅ CORRECT: Fresh database per test with IDisposable
public class CustomerRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext context;
    
    public CustomerRepositoryTests()
    {
        // Fresh in-memory database per test (unique name ensures isolation)
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        
        this.context = new ApplicationDbContext(options);
    }
    
    [Fact]
    public async Task Should_SaveCustomer_When_Valid()
    {
        // Arrange
        var customer = new Customer { FirstName = "John", LastName = "Doe" };
        
        // Act
        await this.context.Customers.AddAsync(customer);
        await this.context.SaveChangesAsync();
        
        // Assert
        var saved = await this.context.Customers.FirstOrDefaultAsync();
        saved.ShouldNotBeNull();
        saved.FirstName.ShouldBe("John");
    }
    
    [Fact]
    public async Task Should_UpdateCustomer_When_Exists()
    {
        // Arrange - fresh database for this test
        var customer = new Customer { FirstName = "John", LastName = "Doe" };
        await this.context.Customers.AddAsync(customer);
        await this.context.SaveChangesAsync();
        
        // Act
        customer.FirstName = "Jane";
        await this.context.SaveChangesAsync();
        
        // Assert
        var updated = await this.context.Customers.FirstOrDefaultAsync();
        updated.FirstName.ShouldBe("Jane");
    }
    
    public void Dispose()
    {
        this.context?.Dispose();
    }
}
```

**Why it matters**: Tests with shared state fail intermittently, are difficult to debug, and cannot run in parallel.

## Edge Cases (🟡 IMPORTANT)

**Rule**: Test null inputs, empty collections, boundary values, and invalid formats.

### Example 9: Comprehensive Edge Case Testing

```csharp
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
    [InlineData("user@.com")]
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
    
    [Fact]
    public void Should_NormalizeEmail_When_Creating()
    {
        // Act
        var result = EmailAddress.Create("  USER@EXAMPLE.COM  ");
        
        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe("user@example.com"); // Trimmed and lowercase
    }
}
```

### Example 10: Boundary Value Testing

```csharp
public class AgeValidationTests
{
    [Theory]
    [InlineData(-1, false)]     // Below minimum
    [InlineData(0, true)]       // Minimum boundary
    [InlineData(1, true)]       // Just above minimum
    [InlineData(75, true)]      // Normal value
    [InlineData(149, true)]     // Just below maximum
    [InlineData(150, true)]     // Maximum boundary
    [InlineData(151, false)]    // Above maximum
    [InlineData(999, false)]    // Way above maximum
    public void Should_ValidateAge_AccordingToBoundaries(int age, bool expectedValid)
    {
        // Act
        var result = ValidateAge(age);
        
        // Assert
        if (expectedValid)
        {
            result.IsSuccess.ShouldBeTrue();
        }
        else
        {
            result.IsFailure.ShouldBeTrue();
            result.Errors.ShouldContain(e => e.Message.Contains("age", StringComparison.OrdinalIgnoreCase));
        }
    }
}
```

## Mocking with NSubstitute (🟢 SUGGESTION)

### Example 11: Mocking Dependencies

```csharp
// ✅ CORRECT: Mocking with NSubstitute
public class CustomerCreateCommandHandlerTests
{
    [Fact]
    public async Task Should_InsertCustomer_When_ValidationPasses()
    {
        // Arrange - Mock repository
        var repository = Substitute.For<IGenericRepository<Customer>>();
        repository.InsertAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        
        // Arrange - Mock mapper
        var mapper = Substitute.For<IMapper>();
        mapper.Map<CustomerModel>(Arg.Any<Customer>())
            .Returns(call =>
            {
                var customer = call.Arg<Customer>();
                return new CustomerModel
                {
                    Id = customer.Id,
                    FirstName = customer.FirstName,
                    LastName = customer.LastName,
                    Email = customer.Email.Value
                };
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
        result.Value.LastName.ShouldBe("Doe");
        
        // Assert - Verify repository was called correctly
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
    public async Task Should_NotCallRepository_When_ValidationFails()
    {
        // Arrange
        var repository = Substitute.For<IGenericRepository<Customer>>();
        var mapper = Substitute.For<IMapper>();
        var handler = new CustomerCreateCommandHandler(repository, mapper);
        
        var command = new CustomerCreateCommand(new CustomerModel
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "invalid-email" // Invalid email format
        });
        
        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.IsFailure.ShouldBeTrue();
        
        // Verify repository was NOT called (validation failed)
        await repository.DidNotReceive().InsertAsync(
            Arg.Any<Customer>(),
            Arg.Any<CancellationToken>()
        );
    }
}
```

### Example 12: Mocking Exceptions

```csharp
[Fact]
public async Task Should_ReturnFailure_When_RepositoryThrowsException()
{
    // Arrange
    var repository = Substitute.For<IGenericRepository<Customer>>();
    repository.InsertAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>())
        .Throws(new DbUpdateException("Database error"));
    
    var mapper = Substitute.For<IMapper>();
    var handler = new CustomerCreateCommandHandler(repository, mapper);
    
    var command = new CustomerCreateCommand(new CustomerModel
    {
        FirstName = "John",
        LastName = "Doe",
        Email = "john@example.com"
    });
    
    // Act & Assert
    await Should.ThrowAsync<DbUpdateException>(async () =>
        await handler.Handle(command, CancellationToken.None)
    );
}
```

## xUnit Patterns (🟢 SUGGESTION)

### Example 13: [Theory] with [InlineData]

```csharp
// ✅ CORRECT: [Theory] for parameterized tests
[Theory]
[InlineData("user@example.com", true)]
[InlineData("another@test.org", true)]
[InlineData("name@company.co.uk", true)]
[InlineData("invalid", false)]
[InlineData("@example.com", false)]
[InlineData("user@", false)]
[InlineData("", false)]
[InlineData(null, false)]
public void Should_ValidateEmailFormat_Correctly(string email, bool expectedValid)
{
    // Act
    var result = EmailAddress.Create(email);
    
    // Assert
    if (expectedValid)
    {
        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(email.Trim().ToLowerInvariant());
    }
    else
    {
        result.IsFailure.ShouldBeTrue();
    }
}
```

### Example 14: [MemberData] for Complex Test Cases

```csharp
public class CustomerValidationTests
{
    public static IEnumerable<object[]> ValidCustomerData => new List<object[]>
    {
        new object[] { "John", "Doe", "john@example.com", true },
        new object[] { "Jane", "Smith", "jane@test.com", true },
        new object[] { "", "Doe", "john@example.com", false },       // Empty first name
        new object[] { "John", "", "john@example.com", false },      // Empty last name
        new object[] { "John", "Doe", "invalid-email", false },      // Invalid email
        new object[] { "John", "Doe", "", false },                   // Empty email
    };
    
    [Theory]
    [MemberData(nameof(ValidCustomerData))]
    public void Should_ValidateCustomer_AccordingToRules(
        string firstName,
        string lastName,
        string email,
        bool expectedValid)
    {
        // Act
        var result = Customer.Create(firstName, lastName, email);
        
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
}
```

## Shouldly Assertions (🟢 SUGGESTION)

### Example 15: Shouldly vs xUnit Assert

```csharp
// 🔴 WRONG: xUnit Assert (less readable failures)
[Fact]
public void Test_With_XUnit_Asserts()
{
    var customer = GetCustomer();
    
    Assert.NotNull(customer);
    Assert.Equal("John", customer.FirstName);
    Assert.Equal("Doe", customer.LastName);
    Assert.True(customer.IsActive);
    Assert.Contains(customer.Addresses, a => a.IsPrimary);
}

// When this fails, you get: "Assert.Equal() Failure\nExpected: John\nActual:   Jane"

// ✅ CORRECT: Shouldly assertions (readable failure messages)
[Fact]
public void Should_ReturnCustomerWithCorrectDetails()
{
    // Act
    var customer = GetCustomer();
    
    // Assert
    customer.ShouldNotBeNull();
    customer.FirstName.ShouldBe("John");
    customer.LastName.ShouldBe("Doe");
    customer.IsActive.ShouldBeTrue();
    customer.Addresses.ShouldContain(a => a.IsPrimary);
}

// When this fails, you get: "customer.FirstName should be 'John' but was 'Jane'"
```

### Example 16: Collection Assertions

```csharp
[Fact]
public void Should_ReturnCustomersInCorrectOrder()
{
    // Act
    var customers = GetCustomers();
    
    // Assert
    customers.ShouldNotBeEmpty();
    customers.Count.ShouldBe(3);
    customers.ShouldContain(c => c.FirstName == "John");
    customers.ShouldAllBe(c => c.IsActive == true);
    customers.ShouldBeInOrder(SortDirection.Ascending);
}
```

### Example 17: Exception Assertions

```csharp
[Fact]
public void Should_ThrowException_When_IdIsEmpty()
{
    // Act & Assert
    Should.Throw<ArgumentException>(() =>
    {
        var customer = new Customer(Guid.Empty, "John", "Doe");
    });
}

[Fact]
public async Task Should_ThrowException_When_CustomerNotFound()
{
    // Arrange
    var repository = Substitute.For<IGenericRepository<Customer>>();
    repository.FindOneAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
        .Returns((Customer)null);
    
    // Act & Assert
    await Should.ThrowAsync<NotFoundException>(async () =>
        await repository.FindOneAsync(Guid.NewGuid(), CancellationToken.None)
    );
}
```

## Integration Tests (🟡 IMPORTANT)

### Example 18: WebApplicationFactory Integration Test

```csharp
// ✅ CORRECT: Integration test with WebApplicationFactory
public class CustomerEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;
    
    public CustomerEndpointsTests(WebApplicationFactory<Program> factory)
    {
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
        var response = await this.client.PostAsJsonAsync("/api/core/customers", request);
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        
        var customer = await response.Content.ReadFromJsonAsync<CustomerModel>();
        customer.ShouldNotBeNull();
        customer.FirstName.ShouldBe("John");
        customer.LastName.ShouldBe("Doe");
        customer.Email.ShouldBe("john@example.com");
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
        var response = await this.client.PostAsJsonAsync("/api/core/customers", request);
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldContain("email", Case.Insensitive);
    }
    
    [Fact]
    public async Task Should_GetCustomer_When_Exists()
    {
        // Arrange - Create a customer first
        var createRequest = new
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com"
        };
        var createResponse = await this.client.PostAsJsonAsync("/api/core/customers", createRequest);
        var createdCustomer = await createResponse.Content.ReadFromJsonAsync<CustomerModel>();
        
        // Act - Get the customer
        var response = await this.client.GetAsync($"/api/core/customers/{createdCustomer.Id}");
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var customer = await response.Content.ReadFromJsonAsync<CustomerModel>();
        customer.ShouldNotBeNull();
        customer.Id.ShouldBe(createdCustomer.Id);
        customer.FirstName.ShouldBe("John");
    }
}
```

## Summary

Testing examples demonstrate best practices:

✅ **Clear naming** (🟡 IMPORTANT - `Should_ExpectedBehavior_When_Condition`)  
✅ **AAA structure** (🟡 IMPORTANT - Arrange-Act-Assert with clear separation)  
✅ **Test independence** (🔴 CRITICAL - no shared state, fresh data per test)  
✅ **Edge cases** (🟡 IMPORTANT - null, empty, boundary, invalid inputs)  
✅ **Mocking** (🟢 SUGGESTION - NSubstitute for dependencies, verify interactions)  
✅ **xUnit patterns** (🟢 SUGGESTION - [Fact], [Theory], [InlineData], [MemberData])  
✅ **Shouldly assertions** (🟢 SUGGESTION - readable failure messages)  
✅ **Integration tests** (🟡 IMPORTANT - WebApplicationFactory for API tests)  

**Quick test quality check**:
- Can you understand what's tested by the name? ✅
- Are Arrange-Act-Assert sections clearly separated? ✅
- Can tests run in any order? ✅
- Are edge cases (null, empty, boundary) covered? ✅
- Are assertions specific (not generic)? ✅

**Reference**: See `checklists/03-testing.md` for complete testing checklist.
