// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using Application.Storage;
using DotNet.Testcontainers.Containers;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class CosmosDocumentStoreCacheProviderTests
{
    private readonly TestEnvironmentFixture fixture;
    private readonly ICacheProvider sut;
    private readonly string key = $"testKey{DateTime.UtcNow.Ticks}";
    private readonly string value = $"testValue{DateTime.UtcNow.Ticks}";

    public CosmosDocumentStoreCacheProviderTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        this.fixture = fixture.WithOutput(output);
        if (this.fixture.CosmosContainer.State == TestcontainersStates.Running)
        {
            var client = new LoggingDocumentStoreClientBehavior<CacheDocument>(XunitLoggerFactory.Create(this.fixture.Output),
                new DocumentStoreClient<CacheDocument>(this.fixture.EnsureCosmosDocumentStoreProvider()));

            this.sut = new DocumentStoreCacheProvider(XunitLoggerFactory.Create(this.fixture.Output),
                new DocumentStoreCache(client),
                client);

            var slidingExpiration = TimeSpan.FromMinutes(30);
            var absoluteExpiration = DateTimeOffset.UtcNow.AddHours(1);
            this.sut.Set(this.key, this.value, slidingExpiration, absoluteExpiration);
        }
        else
        {
            this.fixture.Output?.WriteLine("skipped test: container not running");
        }
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public void Get_WithValidKey_ShouldReturnValue()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        // Act
        var result = this.sut.Get<string>(this.key);

        // Assert
        result.ShouldBe(this.value);
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task GetAsync_WithValidKey_ShouldReturnValue()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        // Act
        var result = await this.sut.GetAsync<string>(this.key);

        // Assert
        result.ShouldBe(this.value);
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public void Get_WithInvalidKey_ShouldNotReturnValue()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        // Act
        var result = this.sut.Get<string>(this.key + "INVALID");

        // Assert
        result.ShouldBe(null);
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task GetAsync_WithInvalidKey_ShouldNotReturnValue()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        // Act
        var result = await this.sut.GetAsync<string>(this.key + "INVALID");

        // Assert
        result.ShouldBe(null);
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public void TryGet_WithValidKey_ShouldReturnTrue()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        // Act
        var result = this.sut.TryGet<string>(this.key, out var value);

        // Assert
        result.ShouldBeTrue();
        value.ShouldBe(this.value);
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task TryGetAsync_WithValidKey_ShouldReturnTrue()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        // Act
        var result = await this.sut.TryGetAsync<string>(this.key, out var value);

        // Assert
        result.ShouldBeTrue();
        value.ShouldBe(this.value);
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public void TryGet_WithInvalidKey_ShouldReturnFalse()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        // Act
        var result = this.sut.TryGet<string>(this.key + "INVALID", out var value);

        // Assert
        result.ShouldBeFalse();
        value.ShouldBe(null);
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task TryGetAsync_WithInvalidKey_ShouldReturnFalse()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        // Act
        var result = await this.sut.TryGetAsync<string>(this.key + "INVALID", out var value);

        // Assert
        result.ShouldBeFalse();
        value.ShouldBe(null);
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public void TryGetKeys_WithExistingEntries_ShouldReturnKeys()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        // Act
        var result = this.sut.GetKeys();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldContain(this.key);
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task TryGetKeysAsync_WithExistingEntries_ShouldReturnKeys()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        // Act
        var result = await this.sut.GetKeysAsync();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldContain(this.key);
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public void Remove_WithValidKey_ShouldRemoveValue()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        // Act
        this.sut.Remove(this.key);

        // Assert
        this.sut.TryGet<string>(this.key, out _)
            .ShouldBeFalse();
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task RemoveAsync_WithValidKey_ShouldRemoveValue()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        // Act
        await this.sut.RemoveAsync(this.key);

        // Assert
        this.sut.TryGet<string>(this.key, out _)
            .ShouldBeFalse();
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public void RemoveStartsWith_WithValidKey_ShouldRemoveValue()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        const string key = "test";

        // Act
        this.sut.RemoveStartsWith(key);

        // Assert
        this.sut.TryGet<string>(this.key, out _)
            .ShouldBeFalse();
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task RemoveStartsWithAsync_WithValidKey_ShouldRemoveValue()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        const string key = "test";

        // Act
        await this.sut.RemoveStartsWithAsync(key);

        // Assert
        this.sut.TryGet<string>(this.key, out _)
            .ShouldBeFalse();
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public void Set_WithValidDataNoExpiration_ShouldReturnValue()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var key = "testKey" + ticks;
        var value = "testValue" + ticks;

        // Act
        this.sut.Set(key, value);

        // Assert
        this.sut.TryGet<string>(key, out _)
            .ShouldBeTrue();
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public void Set_WithValidData_ShouldReturnValue()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var key = "testKey" + ticks;
        var value = "testValue" + ticks;
        var slidingExpiration = TimeSpan.FromMinutes(30);
        var absoluteExpiration = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        this.sut.Set(key, value, slidingExpiration, absoluteExpiration);

        // Assert
        this.sut.TryGet<string>(key, out _)
            .ShouldBeTrue();
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task SetAsync_WithValidData_ShouldReturnValue()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var key = "testKey" + ticks;
        var value = "testValue" + ticks;
        var slidingExpiration = TimeSpan.FromMinutes(30);
        var absoluteExpiration = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        await this.sut.SetAsync(key, value, slidingExpiration, absoluteExpiration);

        // Assert
        this.sut.TryGet<string>(key, out _)
            .ShouldBeTrue();
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public void Set_WithValidButExpiredData_ShouldNotReturnValue()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var key = "testKey" + ticks;
        var value = "testValue" + ticks;
        var slidingExpiration = TimeSpan.FromMinutes(30);
        var absoluteExpiration = DateTimeOffset.UtcNow.AddHours(-1);

        // Act
        this.sut.Set(key, value, slidingExpiration, absoluteExpiration);

        // Assert
        this.sut.TryGet<string>(key, out _)
            .ShouldBeFalse();
    }
}