---
name: dotnet-testing-nsubstitute-mocking
description: Specialized skill for creating test doubles (Mock, Stub, Spy) using NSubstitute. Use when isolating external dependencies, simulating interface behavior, and verifying method calls. Covers complete guidance on Substitute.For, Returns, Received, Throws, etc.
---

# NSubstitute Mocking Skill

## Skill Overview

This skill focuses on using NSubstitute to create and manage test doubles, covering all five Test Double types, dependency isolation strategies, behavior setup, and verification best practices.

## Why Test Doubles?

Real-world code typically depends on external resources, making tests:

1. **Slow** - Requires actual database, file system, or network operations
2. **Unstable** - External service failures cause test failures
3. **Non-reproducible** - Time, random numbers cause inconsistent results
4. **Environment-dependent** - Requires specific external environment setup
5. **Development-blocking** - Must wait for external systems to be ready

Test doubles enable us to isolate these dependencies and focus on testing business logic.

## Prerequisites

### Package Installation

```xml
<PackageReference Include="NSubstitute" Version="5.3.0" />
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.0" />
<PackageReference Include="AwesomeAssertions" Version="9.1.0" />
```

### Basic using Statements

```csharp
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
```

## Five Test Double Types

Based on Gerard Meszaros' definitions in *xUnit Test Patterns*:

### 1. Dummy - Filler Object

Used only to satisfy method signatures, never actually used.

```csharp
public interface IEmailService
{
    void SendEmail(string to, string subject, string body, ILogger logger);
}

[Fact]
public void ProcessOrder_LoggerNotUsed_ShouldProcessOrderSuccessfully()
{
    // Dummy: Just to satisfy parameter requirements
    var dummyLogger = Substitute.For<ILogger>();

    var service = new OrderService();
    var result = service.ProcessOrder(order, dummyLogger);

    result.Success.Should().BeTrue();
    // Don't care if logger was called
}
```

### 2. Stub - Predefined Return Values

Provides predefined return values for testing specific scenarios.

```csharp
[Fact]
public void GetUser_ValidUserId_ShouldReturnUserData()
{
    // Arrange - Stub: Predefined return value
    var stubRepository = Substitute.For<IUserRepository>();
    stubRepository.GetById(123).Returns(new User { Id = 123, Name = "John" });

    var service = new UserService(stubRepository);

    // Act
    var actual = service.GetUser(123);

    // Assert
    actual.Name.Should().Be("John");
    // Don't care how many times GetById was called
}
```

### 3. Fake - Simplified Implementation

Has actual functionality but simplified, typically used for integration tests.

```csharp
public class FakeUserRepository : IUserRepository
{
    private readonly Dictionary<int, User> _users = new();

    public User GetById(int id) => _users.TryGetValue(id, out var user) ? user : null;
    public void Save(User user) => _users[user.Id] = user;
    public void Delete(int id) => _users.Remove(id);
}

[Fact]
public void CreateUser_CreateUser_ShouldSaveAndRetrieve()
{
    // Fake: Simplified implementation with real logic
    var fakeRepository = new FakeUserRepository();
    var service = new UserService(fakeRepository);

    service.CreateUser(new User { Id = 1, Name = "John" });
    var actual = service.GetUser(1);

    actual.Name.Should().Be("John");
}
```

### 4. Spy - Call Recording

Records how it's called, can verify calls afterward.

```csharp
[Fact]
public void CreateUser_CreateUser_ShouldLogCreationInfo()
{
    // Arrange
    var spyLogger = Substitute.For<ILogger<UserService>>();
    var repository = Substitute.For<IUserRepository>();
    var service = new UserService(repository, spyLogger);

    // Act
    service.CreateUser(new User { Name = "John" });

    // Assert - Spy: Verify call records
    spyLogger.Received(1).LogInformation("User created: {Name}", "John");
}
```

### 5. Mock - Behavior Verification

Expects specific interaction behavior, test fails if expectations aren't met.

```csharp
[Fact]
public void RegisterUser_RegisterUser_ShouldSendWelcomeEmail()
{
    // Arrange
    var mockEmailService = Substitute.For<IEmailService>();
    var repository = Substitute.For<IUserRepository>();
    var service = new UserService(repository, mockEmailService);

    // Act
    service.RegisterUser("john@example.com", "John");

    // Assert - Mock: Verify specific interaction behavior
    mockEmailService.Received(1).SendWelcomeEmail("john@example.com", "John");
}
```

## NSubstitute Core Features

### Basic Substitution Syntax

```csharp
// Create interface substitute
var substitute = Substitute.For<IUserRepository>();

// Create class substitute (needs virtual members)
var classSubstitute = Substitute.For<BaseService>();

// Create multiple interface substitute
var multiSubstitute = Substitute.For<IService, IDisposable>();
```

### Return Value Setup

#### Basic Return Values

```csharp
// Exact parameter matching
_repository.GetById(1).Returns(new User { Id = 1, Name = "John" });

// Any parameter matching
_service.Process(Arg.Any<string>()).Returns("processed");

// Return sequence values
_generator.GetNext().Returns(1, 2, 3, 4, 5);
```

#### Conditional Return Values

```csharp
// Use delegate to calculate return value
_calculator.Add(Arg.Any<int>(), Arg.Any<int>())
           .Returns(x => (int)x[0] + (int)x[1]);

// Conditional matching
_service.Process(Arg.Is<string>(x => x.StartsWith("test")))
        .Returns("test-result");
```

#### Throw Exceptions

```csharp
// Synchronous method throws exception
_service.RiskyOperation()
        .Throws(new InvalidOperationException("Something went wrong"));

// Async method throws exception
_service.RiskyOperationAsync()
        .Throws(new InvalidOperationException("Async operation failed"));
```

### Argument Matchers

```csharp
// Any value
_service.Process(Arg.Any<string>()).Returns("result");

// Specific condition
_service.Process(Arg.Is<string>(x => x.Length > 5)).Returns("long-result");

// Argument capture
string capturedArg = null;
_service.Process(Arg.Do<string>(x => capturedArg = x)).Returns("result");
_service.Process("test");
capturedArg.Should().Be("test");

// Argument validation
_service.Process(Arg.Is<string>(x =>
{
    x.Should().StartWith("prefix");
    return true;
})).Returns("result");
```

### Call Verification

```csharp
// Verify called (at least once)
_service.Received().Process("test");

// Verify call count
_service.Received(2).Process(Arg.Any<string>());

// Verify not called
_service.DidNotReceive().Delete(Arg.Any<int>());

// Verify called with any args
_service.ReceivedWithAnyArgs().Process(default);

// Verify call order
Received.InOrder(() =>
{
    _service.Start();
    _service.Process();
    _service.Stop();
});
```

## Real-World Patterns

### Pattern 1: Dependency Injection & Test Setup

#### System Under Test

```csharp
public class FileBackupService
{
    private readonly IFileSystem _fileSystem;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IBackupRepository _backupRepository;
    private readonly ILogger<FileBackupService> _logger;

    public FileBackupService(
        IFileSystem fileSystem,
        IDateTimeProvider dateTimeProvider,
        IBackupRepository backupRepository,
        ILogger<FileBackupService> logger)
    {
        _fileSystem = fileSystem;
        _dateTimeProvider = dateTimeProvider;
        _backupRepository = backupRepository;
        _logger = logger;
    }

    public async Task<BackupResult> BackupFileAsync(string sourcePath, string destinationPath)
    {
        if (!_fileSystem.FileExists(sourcePath))
        {
            _logger.LogWarning("Source file not found: {Path}", sourcePath);
            return new BackupResult { Success = false, Message = "Source file not found" };
        }

        var fileInfo = _fileSystem.GetFileInfo(sourcePath);
        if (fileInfo.Length > 100 * 1024 * 1024)
        {
            return new BackupResult { Success = false, Message = "File too large" };
        }

        var timestamp = _dateTimeProvider.Now.ToString("yyyyMMdd_HHmmss");
        var backupFileName = $"{Path.GetFileNameWithoutExtension(sourcePath)}_{timestamp}{Path.GetExtension(sourcePath)}";
        var fullBackupPath = Path.Combine(destinationPath, backupFileName);

        _fileSystem.CopyFile(sourcePath, fullBackupPath);
        await _backupRepository.SaveBackupHistory(sourcePath, fullBackupPath, _dateTimeProvider.Now);

        _logger.LogInformation("Backup completed: {Path}", fullBackupPath);

        return new BackupResult { Success = true, BackupPath = fullBackupPath };
    }
}
```

#### Test Class Setup

```csharp
public class FileBackupServiceTests
{
    private readonly IFileSystem _fileSystem;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IBackupRepository _backupRepository;
    private readonly ILogger<FileBackupService> _logger;
    private readonly FileBackupService _sut; // System Under Test

    public FileBackupServiceTests()
    {
        _fileSystem = Substitute.For<IFileSystem>();
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _backupRepository = Substitute.For<IBackupRepository>();
        _logger = Substitute.For<ILogger<FileBackupService>>();

        _sut = new FileBackupService(_fileSystem, _dateTimeProvider, _backupRepository, _logger);
    }

    [Fact]
    public async Task BackupFileAsync_FileExistsAndSizeReasonable_ShouldBackupSuccessfully()
    {
        // Arrange
        var sourcePath = @"C:\source\test.txt";
        var destinationPath = @"C:\backup";
        var testTime = new DateTime(2024, 1, 1, 12, 0, 0);

        _fileSystem.FileExists(sourcePath).Returns(true);
        _fileSystem.GetFileInfo(sourcePath).Returns(new FileInfo { Length = 1024 });
        _dateTimeProvider.Now.Returns(testTime);

        // Act
        var result = await _sut.BackupFileAsync(sourcePath, destinationPath);

        // Assert
        result.Success.Should().BeTrue();
        result.BackupPath.Should().Be(@"C:\backup\test_20240101_120000.txt");

        _fileSystem.Received(1).CopyFile(sourcePath, result.BackupPath);
        await _backupRepository.Received(1).SaveBackupHistory(
            sourcePath, result.BackupPath, testTime);
    }
}
```

### Pattern 2: Mock vs Stub Real-World Differences

#### Stub: Focus on State

```csharp
[Fact]
public void CalculateDiscount_PremiumMember_ShouldReturn20Discount()
{
    // Stub: Only care about return value, used to set test scenario
    var stubCustomerService = Substitute.For<ICustomerService>();
    stubCustomerService.GetCustomerType(123).Returns(CustomerType.Premium);

    var service = new PricingService(stubCustomerService);

    // Act
    var discount = service.CalculateDiscount(123, 1000);

    // Assert - Only verify result state
    discount.Should().Be(200); // 20% of 1000
}
```

#### Mock: Focus on Behavior

```csharp
[Fact]
public void ProcessPayment_SuccessfulPayment_ShouldLogTransactionInfo()
{
    // Mock: Care about correct interactions
    var mockLogger = Substitute.For<ILogger<PaymentService>>();
    var stubPaymentGateway = Substitute.For<IPaymentGateway>();
    stubPaymentGateway.ProcessPayment(Arg.Any<decimal>()).Returns(PaymentResult.Success);

    var service = new PaymentService(stubPaymentGateway, mockLogger);

    // Act
    service.ProcessPayment(100);

    // Assert - Verify interaction behavior
    mockLogger.Received(1).LogInformation(
        "Payment processed: {Amount} - Result: {Result}",
        100,
        PaymentResult.Success);
}
```

### Pattern 3: Async Method Testing

```csharp
[Fact]
public async Task GetUserAsync_UserExists_ShouldReturnUserData()
{
    // Arrange
    var repository = Substitute.For<IUserRepository>();
    repository.GetByIdAsync(123).Returns(Task.FromResult(
        new User { Id = 123, Name = "John" }));

    var service = new UserService(repository);

    // Act
    var result = await service.GetUserAsync(123);

    // Assert
    result.Name.Should().Be("John");
    await repository.Received(1).GetByIdAsync(123);
}

[Fact]
public async Task SaveUserAsync_DatabaseError_ShouldThrowException()
{
    // Arrange
    var repository = Substitute.For<IUserRepository>();
    repository.SaveAsync(Arg.Any<User>())
              .Throws(new InvalidOperationException("Database error"));

    var service = new UserService(repository);

    // Act & Assert
    await service.SaveUserAsync(new User { Name = "John" })
                .Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Database error");
}
```

### Pattern 4: ILogger Verification

Due to ILogger's extension method nature, verify the underlying Log method:

```csharp
[Fact]
public async Task BackupFileAsync_FileNotExists_ShouldLogWarning()
{
    // Arrange
    var sourcePath = @"C:\nonexistent\test.txt";
    _fileSystem.FileExists(sourcePath).Returns(false);

    // Act
    var result = await _sut.BackupFileAsync(sourcePath, @"C:\backup");

    // Assert
    result.Success.Should().BeFalse();

    // Verify ILogger.Log method was called correctly
    _logger.Received(1).Log(
        LogLevel.Warning,
        Arg.Any<EventId>(),
        Arg.Is<object>(v => v.ToString().Contains("Source file not found")),
        null,
        Arg.Any<Func<object, Exception, string>>());
}
```

### Pattern 5: Complex Setup Management

Use base test class to manage shared setup:

```csharp
public class OrderServiceTestsBase
{
    protected readonly IOrderRepository Repository;
    protected readonly IEmailService EmailService;
    protected readonly ILogger<OrderService> Logger;
    protected readonly OrderService Sut;

    protected OrderServiceTestsBase()
    {
        Repository = Substitute.For<IOrderRepository>();
        EmailService = Substitute.For<IEmailService>();
        Logger = Substitute.For<ILogger<OrderService>>();
        Sut = new OrderService(Repository, EmailService, Logger);
    }

    protected void SetupValidOrder(int orderId = 1)
    {
        Repository.GetById(orderId).Returns(
            new Order { Id = orderId, Status = OrderStatus.Pending });
    }

    protected void SetupEmailServiceSuccess()
    {
        EmailService.SendConfirmation(Arg.Any<string>()).Returns(true);
    }
}

public class OrderServiceTests : OrderServiceTestsBase
{
    [Fact]
    public void ProcessOrder_ValidOrder_ShouldProcessSuccessfully()
    {
        // Arrange
        SetupValidOrder();
        SetupEmailServiceSuccess();

        // Act
        var result = Sut.ProcessOrder(1);

        // Assert
        result.Success.Should().BeTrue();
    }
}
```

## Advanced Argument Matching Techniques

### Complex Object Matching

```csharp
[Fact]
public void CreateOrder_CreateOrder_ShouldSaveCorrectOrderData()
{
    var repository = Substitute.For<IOrderRepository>();
    var service = new OrderService(repository);

    service.CreateOrder("Product A", 5, 100);

    // Verify object properties
    repository.Received(1).Save(Arg.Is<Order>(o =>
        o.ProductName == "Product A" &&
        o.Quantity == 5 &&
        o.Price == 100));
}
```

### Argument Capture & Verification

```csharp
[Fact]
public void RegisterUser_RegisterUser_ShouldGenerateCorrectPasswordHash()
{
    var repository = Substitute.For<IUserRepository>();
    var service = new UserService(repository);

    User capturedUser = null;
    repository.Save(Arg.Do<User>(u => capturedUser = u));

    service.RegisterUser("john@example.com", "password123");

    capturedUser.Should().NotBeNull();
    capturedUser.Email.Should().Be("john@example.com");
    capturedUser.PasswordHash.Should().NotBe("password123"); // Should be hashed
    capturedUser.PasswordHash.Length.Should().BeGreaterThan(20);
}
```

## Common Pitfalls & Best Practices

### ✅ Recommended Practices

1. **Create Substitute for Interfaces, Not Implementations**

    ```csharp
    // ✅ Correct: Target interface
    var repository = Substitute.For<IUserRepository>();

    // ❌ Wrong: Target concrete class (unless virtual members)
    var repository = Substitute.For<UserRepository>();
    ```

2. **Use Meaningful Test Data**

    ```csharp
    // ✅ Correct: Clearly expresses intent
    var user = new User { Id = 123, Name = "John Doe", Email = "john@example.com" };

    // ❌ Wrong: Meaningless data
    var user = new User { Id = 1, Name = "test", Email = "a@b.c" };
    ```

3. **Avoid Over-Verification**

    ```csharp
    // ✅ Correct: Only verify important behavior
    _emailService.Received(1).SendWelcomeEmail(Arg.Any<string>());

    // ❌ Wrong: Verify all internal implementation details
    _repository.Received(1).GetById(123);
    _repository.Received(1).Update(Arg.Any<User>());
    _validator.Received(1).Validate(Arg.Any<User>());
    ```

4. **Clear Distinction Between Mock and Stub**

    ```csharp
    // ✅ Correct: Stub for setting scenario, Mock for verifying behavior
    var stubRepository = Substitute.For<IUserRepository>(); // Stub
    var mockLogger = Substitute.For<ILogger>(); // Mock

    stubRepository.GetById(123).Returns(user);
    service.ProcessUser(123);
    mockLogger.Received(1).LogInformation(Arg.Any<string>());
    ```

### ❌ Practices to Avoid

1. **Avoid Mocking Value Types**

    ```csharp
    // ❌ Wrong: DateTime is value type
    var badDate = Substitute.For<DateTime>();

    // ✅ Correct: Abstract time provider
    var dateTimeProvider = Substitute.For<IDateTimeProvider>();
    dateTimeProvider.Now.Returns(new DateTime(2024, 1, 1));
    ```

2. **Avoid Tight Coupling Between Tests and Implementation**

    ```csharp
    // ❌ Wrong: Test implementation details
    _repository.Received(1).Query(Arg.Any<string>());
    _repository.Received(1).Filter(Arg.Any<Expression<Func<User, bool>>>());

    // ✅ Correct: Test behavior results
    var users = service.GetActiveUsers();
    users.Should().HaveCount(2);
    ```

3. **Avoid Overly Complex Setup**

    ```csharp
    // ❌ Wrong: Too many Substitutes (may violate SRP)
    var sub1 = Substitute.For<IService1>();
    var sub2 = Substitute.For<IService2>();
    var sub3 = Substitute.For<IService3>();
    var sub4 = Substitute.For<IService4>();

    // ✅ Correct: Reconsider class responsibilities
    // Consider if violating Single Responsibility Principle, needs refactoring
    ```

## Identifying Dependencies to Substitute

### Should Substitute

- ✅ External API calls (IHttpClient, IApiClient)
- ✅ Database operations (IRepository, IDbContext)
- ✅ File system operations (IFileSystem)
- ✅ Network communication (IEmailService, IMessageQueue)
- ✅ Time dependencies (IDateTimeProvider, TimeProvider)
- ✅ Random number generation (IRandom)
- ✅ Expensive calculations (IComplexCalculator)
- ✅ Logging services (ILogger<T>)

### Should Not Substitute

- ❌ Value objects (DateTime, string, int)
- ❌ Simple data transfer objects (DTO)
- ❌ Pure function utilities (like AutoMapper's IMapper, consider real instance)
- ❌ Framework core classes (unless specific need)

## Troubleshooting

### Q1: How to test classes without interfaces?

**A:** Ensure members to mock are `virtual`:

```csharp
public class BaseService
{
    public virtual string GetData() => "real data";
}

var substitute = Substitute.For<BaseService>();
substitute.GetData().Returns("test data");
```

### Q2: How to verify method call order?

**A:** Use `Received.InOrder()`:

```csharp
Received.InOrder(() =>
{
    _service.Start();
    _service.Process();
    _service.Stop();
});
```

### Q3: How to handle out parameters?

**A:** Use `Returns()` with delegate:

```csharp
_service.TryGetValue("key", out Arg.Any<string>())
        .Returns(x =>
        {
            x[1] = "value";
            return true;
        });
```

### Q4: NSubstitute vs Moq - Which to choose?

**A:** NSubstitute advantages:

- More concise, intuitive syntax
- Gentler learning curve
- No privacy concerns
- Sufficient for most testing scenarios

Choose NSubstitute unless:

- Project already uses Moq
- Need Moq-specific advanced features
- Team already familiar with Moq syntax

## Integration with Other Skills

This skill can be combined with:

- **unit-test-fundamentals**: Unit testing basics and 3A pattern
- **dependency-injection-testing**: Dependency injection testing strategies
- **test-naming-conventions**: Test naming conventions
- **test-output-logging**: ITestOutputHelper and ILogger integration
- **datetime-testing-timeprovider**: TimeProvider for abstracting time dependencies
- **filesystem-testing-abstractions**: File system dependency abstraction

## Template Files Reference

This skill provides these template files:

- `templates/mock-patterns.cs`: Complete Mock/Stub/Spy pattern examples
- `templates/verification-examples.cs`: Behavior verification and argument matching examples

## Reference Resources

### Original Articles

Content distilled from "Old-School Software Engineer's Testing Practice - 30 Day Challenge" series:

- **Day 07 - Dependency Substitution Introduction: Using NSubstitute**
  - Ironman article: https://ithelp.ithome.com.tw/articles/10374593
  - Sample code: https://github.com/kevintsengtw/30Days_in_Testing_Samples/tree/main/day07

### NSubstitute Official

- [NSubstitute Official Website](https://nsubstitute.github.io/)
- [NSubstitute GitHub](https://github.com/nsubstitute/NSubstitute)
- [NSubstitute NuGet](https://www.nuget.org/packages/NSubstitute/)

### Test Double Theory

- [XUnit Test Patterns](http://xunitpatterns.com/Test%20Double.html)
- [Martin Fowler - Test Double](https://martinfowler.com/bliki/TestDouble.html)