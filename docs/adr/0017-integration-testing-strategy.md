# ADR-0017: Integration Testing Strategy

## Status

Accepted

## Context

While unit tests verify individual components in isolation, production issues often arise from:

- **Integration Points**: Database queries, HTTP endpoints, middleware pipeline
- **Configuration**: Settings applied only in running application
- **Infrastructure**: EF Core migrations, database constraints, transactions
- **Request Pipeline**: Authentication, authorization, middleware ordering
- **Serialization**: JSON serialization/deserialization edge cases
- **Validation**: End-to-end validation across layers

Unit tests alone cannot verify:

- Actual HTTP request/response behavior
- Database queries execute correctly with real EF provider
- Configuration binds properly from `appsettings.json`
- Middleware pipeline processes requests in correct order
- Authentication/authorization work end-to-end
- OpenAPI schema generation matches endpoint definitions

The application needed an integration testing strategy that:

1. Tests **real HTTP endpoints** with full request pipeline
2. Uses **in-memory or test databases** for isolation
3. Supports **authentication** scenarios
4. Provides **fast feedback** (seconds, not minutes)
5. Enables **parallel execution** without conflicts
6. Integrates with **xUnit** and existing test infrastructure

## Decision

Adopt **WebApplicationFactory\<TProgram>** for integration testing with **test fixture pattern**, **in-memory authentication**, **test database per fixture**, and **endpoint-focused test organization**.

### Test Fixture Pattern

```csharp
public class CustomWebApplicationFactoryFixture<TProgram> : WebApplicationFactory<TProgram>
    where TProgram : class
{
    private readonly EndpointTestFixtureOptions options;

    public CustomWebApplicationFactoryFixture(EndpointTestFixtureOptions options = null)
    {
        this.options = options ?? new EndpointTestFixtureOptions();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTest");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override configuration for tests
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:Default"] = "Server=(localdb)\\mssqllocaldb;Database=TestDb;Trusted_Connection=True;",
                ["Authentication:Enabled"] = "false" // Disable for most tests
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace services for testing
            services.RemoveAll<DbContextOptions<CoreDbContext>>();
            services.AddDbContext<CoreDbContext>(options =>
                options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        });
    }
}
```

### Endpoint Test Fixture with Authentication

```csharp
public sealed class EndpointTestFixture : IAsyncLifetime
{
    private readonly CustomWebApplicationFactoryFixture<Program> factory;
    private readonly EndpointTestFixtureOptions options;
    private string bearerToken;

    public EndpointTestFixture(
        ITestOutputHelper output,
        EndpointTestFixtureOptions options = null)
    {
        this.Output = output;
        this.options = options ?? new EndpointTestFixtureOptions();
        this.factory = new CustomWebApplicationFactoryFixture<Program>(this.options);
    }

    public ITestOutputHelper Output { get; }

    public HttpClient CreateClient()
    {
        var client = this.factory.CreateClient();

        if (!string.IsNullOrEmpty(this.bearerToken))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", this.bearerToken);
        }

        return client;
    }

    public async Task InitializeAsync()
    {
        if (this.options.UseAuthentication)
        {
            // Acquire JWT token for tests
            using var client = this.factory.CreateClient();
            var tokenRequest = new
            {
                grant_type = "password",
                client_id = this.options.ClientId,
                username = this.options.Username,
                password = this.options.Password,
                scope = this.options.Scope
            };

            var response = await client.PostAsJsonAsync(
                this.options.TokenEndpoint,
                tokenRequest);

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
            this.bearerToken = tokenResponse.AccessToken;
        }
    }

    public async Task DisposeAsync()
    {
        await this.factory.DisposeAsync();
    }
}

public class EndpointTestFixtureOptions
{
    public bool UseAuthentication { get; set; }
    public string TokenEndpoint { get; set; } = "/api/_system/fake-identity/token";
    public string ClientId { get; set; } = "test_client";
    public string Username { get; set; } = "test@example.com";
    public string Password { get; set; } = "Test123!";
    public string Scope { get; set; } = "api";
}
```

### Integration Test Example

```csharp
public class CustomerEndpointsTests : IClassFixture<EndpointTestFixture>
{
    private readonly EndpointTestFixture fixture;
    private readonly HttpClient client;

    public CustomerEndpointsTests(EndpointTestFixture fixture, ITestOutputHelper output)
    {
        this.fixture = new EndpointTestFixture(output);
        this.client = this.fixture.CreateClient();
    }

    [Fact]
    public async Task CreateCustomer_WithValidData_ReturnsCreatedWithLocation()
    {
        // Arrange
        var command = new CustomerCreateCommand("John Doe", "john@example.com");

        // Act
        var response = await this.client.PostAsJsonAsync("/api/core/customers", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var customerId = await response.Content.ReadFromJsonAsync<CustomerId>();
        customerId.Should().NotBeNull();
        customerId.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateCustomer_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var command = new CustomerCreateCommand("John Doe", "invalid-email");

        // Act
        var response = await this.client.PostAsJsonAsync("/api/core/customers", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problemDetails.Errors.Should().ContainKey("Email");
    }

    [Fact]
    public async Task GetCustomers_ReturnsOkWithCustomers()
    {
        // Arrange - seed data
        var createCommand = new CustomerCreateCommand("Jane Doe", "jane@example.com");
        await this.client.PostAsJsonAsync("/api/core/customers", createCommand);

        // Act
        var response = await this.client.GetAsync("/api/core/customers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var customers = await response.Content.ReadFromJsonAsync<List<CustomerModel>>();
        customers.Should().NotBeEmpty();
        customers.Should().Contain(c => c.Email == "jane@example.com");
    }
}
```

### Test Data Seeding

```csharp
public static class TestDataFactory
{
    public static async Task<CustomerId> CreateCustomer(
        this HttpClient client,
        string name = "Test Customer",
        string email = "test@example.com")
    {
        var command = new CustomerCreateCommand(name, email);
        var response = await client.PostAsJsonAsync("/api/core/customers", command);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CustomerId>();
    }

    public static async Task<List<CustomerId>> CreateCustomers(
        this HttpClient client,
        int count)
    {
        var ids = new List<CustomerId>();
        for (int i = 0; i < count; i++)
        {
            var id = await client.CreateCustomer($"Customer {i}", $"customer{i}@example.com");
            ids.Add(id);
        }

        return ids;
    }
}
```

## Rationale

### Why WebApplicationFactory

1. **Real Request Pipeline**: Full ASP.NET Core pipeline including middleware, routing, model binding
2. **Configuration Override**: Easy to replace services and configuration for tests
3. **In-Process**: Runs in same process (fast, no network latency)
4. **HttpClient**: Tests use familiar `HttpClient` API
5. **Isolation**: Each test can have its own database/services
6. **First-Class Support**: Built into ASP.NET Core, well-documented

### Why In-Memory Database

1. **Speed**: 10-100x faster than real database
2. **Isolation**: Each test fixture gets unique database
3. **Cleanup**: Database automatically disposed after tests
4. **Parallelization**: Tests run in parallel without conflicts
5. **No Setup**: No database server required
6. **Deterministic**: No external dependencies

### Why Test Fixture Pattern

1. **Setup/Teardown**: `IAsyncLifetime` handles async initialization
2. **Resource Sharing**: Factory shared across test class
3. **Authentication**: Token acquired once per fixture
4. **Test Output**: `ITestOutputHelper` wired to Serilog sink
5. **Configuration**: Options pattern for flexible test setup

### Why Endpoint-Focused Organization

1. **Clear Scope**: Tests organized by API endpoint
2. **API Contract Verification**: Tests verify OpenAPI-generated client behavior
3. **User Perspective**: Tests mimic actual API usage
4. **Documentation**: Tests serve as usage examples
5. **Regression Detection**: Breaking API changes caught immediately

## Consequences

### Positive

- **High Confidence**: Tests verify actual HTTP behavior, not mocked interactions
- **Fast Feedback**: In-memory database keeps tests fast (milliseconds per test)
- **Parallel Execution**: Isolated databases enable parallel test runs
- **Realistic**: Full request pipeline catches middleware, serialization issues
- **Documentation**: Tests demonstrate actual API usage
- **Regression Prevention**: Breaking changes to endpoints caught immediately
- **Authentication Testing**: Fixture pattern simplifies auth scenarios
- **Easy Debugging**: In-process execution simplifies debugging

### Negative

- **Slower than Unit Tests**: 10-100x slower than unit tests (still fast enough)
- **Setup Complexity**: Factory and fixture setup more complex than unit tests
- **In-Memory Limitations**: Some EF/database features not supported (stored procedures, triggers)
- **Data Seeding**: Test data must be created via API or repository
- **Maintenance**: Changes to Program.cs/Startup may break tests

### Neutral

- **Test Database**: In-memory vs. real database trade-off (speed vs. realism)
- **Authentication**: Optional per fixture via `EndpointTestFixtureOptions`
- **Test Organization**: One test class per endpoint or grouped by feature

## Implementation Guidelines

### Test Class Template

```csharp
[Trait("Category", "Integration")]
public class MyEndpointsTests : IAsyncLifetime
{
    private readonly EndpointTestFixture fixture;
    private readonly HttpClient client;

    public MyEndpointsTests(ITestOutputHelper output)
    {
        this.fixture = new EndpointTestFixture(output, new EndpointTestFixtureOptions
        {
            UseAuthentication = true // Enable if endpoint requires auth
        });

        this.client = this.fixture.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await this.fixture.InitializeAsync();

        // Seed test data
        await this.client.CreateCustomer();
    }

    public async Task DisposeAsync()
    {
        await this.fixture.DisposeAsync();
    }

    [Fact]
    public async Task Endpoint_Scenario_ExpectedResult()
    {
        // Arrange

        // Act

        // Assert
    }
}
```

### Testing Validation Errors

```csharp
[Fact]
public async Task CreateCustomer_WithMissingName_ReturnsBadRequest()
{
    // Arrange
    var command = new CustomerCreateCommand(null, "test@example.com");

    // Act
    var response = await this.client.PostAsJsonAsync("/api/core/customers", command);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

    var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
    problemDetails.Should().NotBeNull();
    problemDetails.Errors.Should().ContainKey("Name");
}
```

### Testing Filtering/Pagination

```csharp
[Fact]
public async Task GetCustomers_WithFilter_ReturnsMatchingCustomers()
{
    // Arrange
    await this.client.CreateCustomer("Alice", "alice@example.com");
    await this.client.CreateCustomer("Bob", "bob@example.com");

    // Act
    var response = await this.client.GetAsync(
        "/api/core/customers?filter=Email:alice@example.com");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var customers = await response.Content.ReadFromJsonAsync<List<CustomerModel>>();
    customers.Should().HaveCount(1);
    customers[0].Name.Should().Be("Alice");
}
```

### Testing Authentication

```csharp
[Fact]
public async Task GetCustomers_WithoutAuthentication_ReturnsUnauthorized()
{
    // Arrange
    var clientWithoutAuth = this.factory.CreateClient();
    // Don't add bearer token

    // Act
    var response = await clientWithoutAuth.GetAsync("/api/core/customers");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

### Using Real Database for Integration Tests

```csharp
// When in-memory limitations are hit, use real database
builder.ConfigureServices(services =>
{
    services.RemoveAll<DbContextOptions<CoreDbContext>>();
    services.AddDbContext<CoreDbContext>(options =>
        options.UseSqlServer($"Server=(localdb)\\mssqllocaldb;Database=IntegrationTest_{Guid.NewGuid()};"));
});

// Cleanup in fixture disposal
public async Task DisposeAsync()
{
    // Drop test database
    await using var scope = this.factory.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    await dbContext.Database.EnsureDeletedAsync();

    await this.factory.DisposeAsync();
}
```

### Testing Background Jobs

```csharp
[Fact]
public async Task CustomerExportJob_CreatesExportFile()
{
    // Arrange
    await this.client.CreateCustomers(5);

    // Trigger job manually
    var scheduler = this.factory.Services.GetRequiredService<IScheduler>();
    var job = new JobKey(nameof(CustomerExportJob));
    await scheduler.TriggerJob(job);

    // Wait for job completion
    await Task.Delay(TimeSpan.FromSeconds(2));

    // Assert
    // Verify export file or side effects
}
```

## Alternatives Considered

### Alternative 1: Manual Test Server Setup

```csharp
var builder = WebApplication.CreateBuilder();
// Manual service registration
var app = builder.Build();
var server = new TestServer(app);
```

**Rejected because**:

- More boilerplate than `WebApplicationFactory`
- Doesn't leverage existing `Program.cs`
- Harder to maintain as application evolves
- `WebApplicationFactory` is standard ASP.NET Core pattern

### Alternative 2: Postman/REST Client Tests

**Rejected because**:

- Not automated in CI pipeline
- No assertions or validation
- Slower to run (external process)
- No integration with xUnit test runner
- Can't easily seed test data

### Alternative 3: Separate Integration Test Project with Real Database

**Rejected because**:

- Slower (database I/O)
- Parallel execution difficult (shared database state)
- Requires database server setup
- More complex CI/CD configuration
- In-memory sufficient for most scenarios

### Alternative 4: BDD Framework (SpecFlow)

**Rejected because**:

- Additional learning curve (Gherkin syntax)
- Overkill for technical integration tests
- xUnit provides sufficient readability with proper naming
- SpecFlow better suited for acceptance tests with non-technical stakeholders

## Related Decisions

- [ADR-0013](0013-unit-testing-high-coverage-strategy.md): Integration tests complement unit tests
- [ADR-0014](0014-minimal-api-endpoints-dto-exposure.md): Tests verify endpoint contracts
- [ADR-0016](0016-logging-observability-strategy.md): `ITestOutputHelper` captures logs in tests
- [ADR-0018](0018-dependency-injection-service-lifetimes.md): Service replacement pattern in tests

## References

- [Integration Tests in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
- [WebApplicationFactory Documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.testing.webapplicationfactory-1)
- [xUnit Documentation](https://xunit.net/docs/getting-started/netcore/cmdline)

## Notes

### Test Organization

```text
tests/
  Modules/
    CoreModule/
      CoreModule.IntegrationTests/
        Presentation/
          Web/
            EndpointTestFixture.cs
            CustomerEndpointsTests.cs
            OrderEndpointsTests.cs
        Infrastructure/
          EntityFramework/
            CoreDbContextTests.cs  // Repository integration tests
```

### Running Integration Tests

```powershell
# Run all integration tests
dotnet test --filter "Category=Integration"

# Run specific test class
dotnet test --filter "FullyQualifiedName~CustomerEndpointsTests"

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### CI/CD Considerations

```yaml
# GitHub Actions example
- name: Run Integration Tests
  run: dotnet test --filter "Category=IntegrationTest" --no-build --verbosity normal
  env:
    ASPNETCORE_ENVIRONMENT: IntegrationTest
```

### Debugging Integration Tests

1. Set breakpoint in test or endpoint code
2. Debug test via Test Explorer or `dotnet test`
3. Full request pipeline executes in same process
4. Inspect `ITestOutputHelper` output for logs

### Common Pitfalls

WRONG **Sharing state between tests**:

```csharp
// WRONG - shared database leads to flaky tests
private static readonly CoreDbContext SharedDbContext;
```

CORRECT **Isolate per fixture**:

```csharp
// CORRECT - each fixture gets unique in-memory database
services.AddDbContext<CoreDbContext>(options =>
    options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
```

WRONG **Not disposing HttpClient**:

```csharp
// WRONG - resource leak
var client = this.factory.CreateClient();
// No disposal
```

CORRECT **Use using statement**:

```csharp
// CORRECT
using var client = this.fixture.CreateClient();
```

### Test Data Strategies

1. **Inline Creation**: Create data directly in test via API calls
2. **Factory Methods**: Reusable `TestDataFactory` extension methods
3. **Fixture Seeding**: Seed common data in `InitializeAsync()`
4. **Test-Specific**: Create unique data per test to avoid conflicts
