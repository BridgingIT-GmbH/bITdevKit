# xUnit Test Patterns

## Contents
- Test Naming Convention
- Arrange-Act-Assert Pattern
- Theory Patterns
- Mocking Patterns
- Exception Testing
- Collection Assertions
- Anti-Patterns

---

## Test Naming Convention

**Pattern:** `MethodName_Scenario_ExpectedBehavior`

```csharp
// GOOD - Clear, descriptive names
public async Task ValidateAsync_ValidData_ReturnsValid() { }
public void Build_WithoutTitle_ThrowsInvalidOperationException() { }
public async Task CreateAsync_DuplicateKey_ReturnsFailure() { }

// BAD - Vague, unclear names
public void Test1() { }
public void ValidationTest() { }
public void ShouldWork() { }
```

---

## Arrange-Act-Assert Pattern

Every test in Sorcha follows strict AAA:

```csharp
[Fact]
public async Task ProcessAsync_ValidData_ReturnsSuccess()
{
    // Arrange
    var blueprint = CreateSimpleBlueprint();
    var context = new ExecutionContext
    {
        Blueprint = blueprint,
        Action = blueprint.Actions[0],
        ActionData = new Dictionary<string, object>
        {
            ["name"] = "Alice",
            ["age"] = 30
        }
    };

    // Act
    var result = await _processor.ProcessAsync(context);

    // Assert
    result.Success.Should().BeTrue();
    result.Validation.IsValid.Should().BeTrue();
    result.ProcessedData.Should().ContainKey("name");
}
```

---

## Theory Patterns

### InlineData for Simple Values

```csharp
[Theory]
[InlineData("")]
[InlineData("  ")]
[InlineData(null)]
public void Title_WithEmptyValue_ShouldFailValidation(string? title)
{
    var action = new Action { Id = 0, Title = title! };
    var context = new ValidationContext(action);
    var results = new List<ValidationResult>();

    var isValid = Validator.TryValidateObject(action, context, results, true);

    isValid.Should().BeFalse();
    results.Should().Contain(r => r.MemberNames.Contains("Title"));
}
```

### MemberData for Complex Objects

```csharp
public static IEnumerable<object[]> InvalidBlueprintData =>
    new List<object[]>
    {
        new object[] { new Blueprint { Title = "" }, "Title required" },
        new object[] { new Blueprint { Actions = new() }, "Actions required" },
    };

[Theory]
[MemberData(nameof(InvalidBlueprintData))]
public void Validate_InvalidBlueprint_ReturnsError(Blueprint blueprint, string expectedError)
{
    var result = _validator.Validate(blueprint);

    result.IsValid.Should().BeFalse();
    result.Errors.Should().Contain(e => e.Contains(expectedError));
}
```

---

## Mocking Patterns

See the **moq** skill for comprehensive mocking patterns.

### Basic Mock Setup

```csharp
public class WalletServiceTests
{
    private readonly Mock<IRepository<Wallet>> _mockRepository;
    private readonly Mock<ILogger<WalletService>> _mockLogger;
    private readonly WalletService _sut;

    public WalletServiceTests()
    {
        _mockRepository = new Mock<IRepository<Wallet>>();
        _mockLogger = new Mock<ILogger<WalletService>>();
        _sut = new WalletService(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetAsync_ExistingWallet_ReturnsWallet()
    {
        // Arrange
        var walletId = Guid.NewGuid();
        var wallet = new Wallet { Id = walletId, Name = "Test" };
        _mockRepository.Setup(r => r.GetByIdAsync(walletId)).ReturnsAsync(wallet);

        // Act
        var result = await _sut.GetAsync(walletId);

        // Assert
        result.Should().Be(wallet);
        _mockRepository.Verify(r => r.GetByIdAsync(walletId), Times.Once);
    }
}
```

### Callback Capture Pattern

```csharp
[Fact]
public async Task SetAsync_ValidEntry_StoresEncryptedData()
{
    string? capturedKey = null;
    string? capturedValue = null;

    _mockLocalStorage
        .Setup(x => x.SetItemAsStringAsync(It.IsAny<string>(), It.IsAny<string>()))
        .Callback<string, string, CancellationToken>((key, value, _) =>
        {
            capturedKey = key;
            capturedValue = value;
        })
        .Returns(ValueTask.CompletedTask);

    await _tokenCache.SetAsync("docker", entry);

    capturedKey.Should().Be("sorcha:tokens:docker");
    capturedValue.Should().NotBeNullOrEmpty();
}
```

---

## Exception Testing

### Sync Exception Testing

```csharp
[Fact]
public void Constructor_NullSchemaValidator_ThrowsArgumentNullException()
{
    var act = () => new ActionProcessor(null!, _jsonLogic, _disclosure, _routing);

    act.Should().Throw<ArgumentNullException>()
        .WithParameterName("schemaValidator");
}
```

### Async Exception Testing

```csharp
[Fact]
public async Task ExecuteAsync_NonExistentInstance_ThrowsInvalidOperationException()
{
    var exception = await Assert.ThrowsAsync<InvalidOperationException>(
        () => _service.ExecuteAsync(Guid.NewGuid()));

    exception.Message.Should().Contain("Instance not found");
}

// Alternative with FluentAssertions
[Fact]
public async Task ProcessAsync_EncryptionFailure_ThrowsWithContext()
{
    _mockEncryption
        .Setup(x => x.EncryptAsync(It.IsAny<string>()))
        .ThrowsAsync(new InvalidOperationException("Module not loaded"));

    var act = async () => await _tokenCache.SetAsync("docker", entry);

    await act.Should().ThrowAsync<InvalidOperationException>()
        .WithMessage("*Failed to store token*");
}
```

---

## Collection Assertions

See the **fluent-assertions** skill for complete assertion patterns.

```csharp
// Count assertions
blueprint.Participants.Should().HaveCount(2);
result.Value!.Split(' ').Should().HaveCount(12);

// Contains assertions
result.ProcessedData.Should().ContainKey("name");
blueprint.Actions.Should().Contain(p => p.Principal == "p2");

// Range assertions
items.Should().HaveCountGreaterThanOrEqualTo(2);
validation.GetProperty("count").GetInt32().Should().BeGreaterThanOrEqualTo(3);
```

---

## Anti-Patterns

### WARNING: Missing Arrange-Act-Assert Sections

**The Problem:**

```csharp
// BAD - No structure, hard to understand
[Fact]
public async Task TestWallet()
{
    var wallet = new Wallet { Name = "Test" };
    _mockRepo.Setup(r => r.AddAsync(wallet)).ReturnsAsync(wallet);
    var result = await _sut.CreateAsync(wallet);
    result.IsSuccess.Should().BeTrue();
    _mockRepo.Verify(r => r.AddAsync(wallet));
}
```

**The Fix:**

```csharp
// GOOD - Clear sections
[Fact]
public async Task CreateAsync_ValidWallet_ReturnsSuccessAndPersists()
{
    // Arrange
    var wallet = new Wallet { Name = "Test" };
    _mockRepo.Setup(r => r.AddAsync(wallet)).ReturnsAsync(wallet);

    // Act
    var result = await _sut.CreateAsync(wallet);

    // Assert
    result.IsSuccess.Should().BeTrue();
    _mockRepo.Verify(r => r.AddAsync(wallet), Times.Once);
}
```

### WARNING: Testing Implementation Details

**The Problem:**

```csharp
// BAD - Tests internal state, breaks on refactor
[Fact]
public void Process_SetsInternalFlag()
{
    _processor.Process(data);
    _processor._internalProcessed.Should().BeTrue(); // Private field!
}
```

**The Fix:**

```csharp
// GOOD - Tests observable behavior
[Fact]
public void Process_ValidData_ProducesExpectedOutput()
{
    var result = _processor.Process(data);
    result.Output.Should().NotBeNull();
}
```

### WARNING: Not Isolating Tests

**The Problem:**

```csharp
// BAD - Shared state between tests
private static Wallet _sharedWallet = new();

[Fact]
public void Test1() { _sharedWallet.Name = "A"; }

[Fact]
public void Test2() { _sharedWallet.Name.Should().BeNull(); } // FAILS!
```

**The Fix:**

```csharp
// GOOD - Each test creates its own state
[Fact]
public void CreateAsync_ValidWallet_Succeeds()
{
    var wallet = new Wallet { Name = "Test" };
    // ... test with fresh instance
}