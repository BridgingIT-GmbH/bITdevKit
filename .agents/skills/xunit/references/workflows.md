# xUnit Test Workflows

## Contents
- Project Setup
- GlobalUsings Pattern
- Class Fixtures
- Collection Fixtures
- Integration Test Setup
- WebApplicationFactory
- Test Configuration

---

## Project Setup

### Test Project File

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <!-- Suppress nullable warnings in test code -->
    <NoWarn>$(NoWarn);CS8600;CS8602;CS8604;CS8620</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="FluentAssertions" Version="8.8.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.1" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Services\Sorcha.Wallet.Service\Sorcha.Wallet.Service.csproj" />
  </ItemGroup>

</Project>
```

---

## GlobalUsings Pattern

Every test project has `GlobalUsings.cs`:

```csharp
// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Sorcha Contributors

global using Xunit;
global using FluentAssertions;
global using Moq;
global using Sorcha.Wallet.Core;
global using Sorcha.Wallet.Core.Models;
```

**Why:** Eliminates redundant imports across all test files, ensures consistency.

---

## Class Fixtures

For expensive setup shared across tests in ONE class:

```csharp
public class DatabaseFixture : IAsyncLifetime
{
    public TenantDbContext Context { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        Context = new TenantDbContext(options, "public");
        await Context.Database.EnsureCreatedAsync();
        await SeedTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        await Context.DisposeAsync();
    }

    private async Task SeedTestDataAsync()
    {
        Context.Organizations.Add(new Organization
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Test Organization",
            Status = OrganizationStatus.Active
        });
        await Context.SaveChangesAsync();
    }
}

public class OrganizationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public OrganizationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetByIdAsync_ExistingOrg_ReturnsOrg()
    {
        var org = await _fixture.Context.Organizations.FindAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"));

        org.Should().NotBeNull();
        org!.Name.Should().Be("Test Organization");
    }
}
```

---

## Collection Fixtures

For expensive setup shared across MULTIPLE classes:

```csharp
// 1. Define the fixture
public class TenantServiceWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;

    public async Task InitializeAsync()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithDatabase("TenantTests")
            .WithUsername("sorcha_test")
            .WithPassword("sorcha_test_pass")
            .WithCleanUp(true)
            .Build();

        await _postgresContainer.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        if (_postgresContainer != null)
            await _postgresContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace with test database
            var connectionString = _postgresContainer!.GetConnectionString();
            services.AddDbContext<TenantDbContext>(opts =>
                opts.UseNpgsql(connectionString));
        });
    }
}

// 2. Define the collection
[CollectionDefinition("TenantService")]
public class TenantServiceCollection : ICollectionFixture<TenantServiceWebApplicationFactory>
{
}

// 3. Use in test classes
[Collection("TenantService")]
public class TenantApiTests
{
    private readonly TenantServiceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TenantApiTests(TenantServiceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetTenants_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/tenants");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

---

## Integration Test Setup

### WebApplicationFactory Pattern

```csharp
public class BlueprintServiceFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace external services with mocks
            var mockDatabase = new Mock<IDatabase>();
            mockDatabase.Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            var mockMultiplexer = new Mock<IConnectionMultiplexer>();
            mockMultiplexer.Setup(m => m.IsConnected).Returns(true);
            mockMultiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockDatabase.Object);

            services.AddSingleton(mockMultiplexer.Object);
        });
    }
}
```

### Test Authentication Handler

```csharp
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestScheme";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return Task.FromResult(AuthenticateResult.NoResult());

        var isAdmin = Request.Headers.TryGetValue("X-Test-Role", out var role) &&
                      role.ToString().Contains("Administrator");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, isAdmin ? "Admin" : "User"),
            new(ClaimTypes.Role, isAdmin ? "Administrator" : "Member")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

---

## Test Configuration

### Skip Long-Running Tests

```csharp
[Fact(Skip = "Requires running Docker services")]
public async Task CompleteWorkflow_ShouldSucceed()
{
    // Integration test requiring infrastructure
}
```

### Test Traits

```csharp
[Trait("Category", "Integration")]
[Trait("Requires", "Docker")]
public class MongoRepositoryTests
{
    // Tests grouped by traits
}
```

### Test Helpers

```csharp
public static class TestHelpers
{
    public static async Task<bool> WaitForConditionAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        TimeSpan? pollingInterval = null)
    {
        pollingInterval ??= TimeSpan.FromMilliseconds(100);
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            if (await condition())
                return true;
            await Task.Delay(pollingInterval.Value);
        }

        return false;
    }

    public static StringContent ToJsonContent<T>(this T obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}
```

---

## Workflow Checklists

### Adding a New Test Project

Copy this checklist and track progress:
- [ ] Create `tests/Sorcha.{Feature}.Tests/` directory
- [ ] Add `.csproj` with standard test package references
- [ ] Add `GlobalUsings.cs` with common imports
- [ ] Add project reference to source project
- [ ] Create first test class with license header
- [ ] Verify tests run: `dotnet test tests/Sorcha.{Feature}.Tests`

### Writing Integration Tests

Copy this checklist and track progress:
- [ ] Create WebApplicationFactory subclass
- [ ] Configure mock external services (Redis, HTTP clients)
- [ ] Add TestAuthHandler if auth required
- [ ] Create collection definition if sharing fixtures
- [ ] Add `[Collection("Name")]` to test classes
- [ ] Mark infrastructure-dependent tests with `Skip` reason

### Test Feedback Loop

1. Write failing test
2. Run: `dotnet test --filter "FullyQualifiedName~MethodName"`
3. If test fails with wrong reason, fix test setup and repeat step 2
4. Implement minimum code to pass
5. Run test again - if fails, fix implementation and repeat
6. Only proceed when test passes
7. Refactor if needed, run tests after each change