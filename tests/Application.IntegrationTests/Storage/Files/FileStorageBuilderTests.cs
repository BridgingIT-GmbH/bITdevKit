// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using Bogus;
using BridgingIT.DevKit.Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

[IntegrationTest("Storage")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public partial class FileStorageBuilderTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);
    private readonly ITestOutputHelper output = output;

    private IServiceProvider CreateServiceProvider()
    {
        return new ServiceCollection()
            .AddLogging()
            .AddMemoryCache()
            .BuildServiceProvider();
    }

    [Fact]
    public void Builder_CreatesInMemoryProvider_Succeeds()
    {
        // Arrange
        var services = this.CreateServiceProvider();
        var builder = new FileStorageBuilder(services);

        // Act
        var provider = builder.UseInMemory("TestInMemory").Build();

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<InMemoryFileStorageProvider>();
        provider.LocationName.ShouldBe("TestInMemory");
    }

    [Fact]
    public void Builder_CreatesLocalProvider_Succeeds()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), "TestStorage_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);
        var services = this.CreateServiceProvider();
        var builder = new FileStorageBuilder(services);

        // Act
        var provider = builder.UseLocal(tempPath, "TestLocal").Build();

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<LocalFileStorageProvider>();
        provider.LocationName.ShouldBe("TestLocal");
    }

    [Fact]
    public void Builder_WithLogging_AddsLoggingBehavior_Succeeds()
    {
        // Arrange
        var services = this.CreateServiceProvider();
        var builder = new FileStorageBuilder(services);

        // Act
        var provider = builder.UseInMemory("TestInMemory").WithLogging().Build();

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<LoggingFileStorageBehavior>();
        var loggingBehavior = provider as LoggingFileStorageBehavior;
        loggingBehavior.InnerProvider.ShouldBeOfType<InMemoryFileStorageProvider>();
    }

    [Fact]
    public void Builder_WithCaching_AddsCachingBehavior_Succeeds()
    {
        // Arrange
        var services = this.CreateServiceProvider();
        var builder = new FileStorageBuilder(services);

        // Act
        var provider = builder.UseInMemory("TestInMemory").WithCaching().Build();

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<CachingFileStorageBehavior>();
        var cachingBehavior = provider as CachingFileStorageBehavior;
        cachingBehavior.InnerProvider.ShouldBeOfType<InMemoryFileStorageProvider>();
    }

    [Fact]
    public void Builder_WithRetry_AddsRetryBehavior_Succeeds()
    {
        // Arrange
        var services = this.CreateServiceProvider();
        var builder = new FileStorageBuilder(services);

        // Act
        var provider = builder.UseInMemory("TestInMemory").WithRetry().Build();

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<RetryFileStorageBehavior>();
        var retryBehavior = provider as RetryFileStorageBehavior;
        retryBehavior.InnerProvider.ShouldBeOfType<InMemoryFileStorageProvider>();
    }

    [Fact]
    public void Builder_WithCustomBehavior_AddsCustomBehavior_Succeeds()
    {
        // Arrange
        var services = this.CreateServiceProvider();
        var builder = new FileStorageBuilder(services);

        // Act
        var provider = builder.UseInMemory("TestInMemory")
            .WithCustomBehavior(p => new CustomBehavior(p))
            .Build();

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<CustomBehavior>();
        var customBehavior = provider as CustomBehavior;
        customBehavior.InnerProvider.ShouldBeOfType<InMemoryFileStorageProvider>();
    }

    [Fact]
    public void Builder_WithLifetime_AppliesCorrectLifetime()
    {
        // Arrange
        var services = this.CreateServiceProvider();
        var builder = new FileStorageBuilder(services);

        // Act
        var provider = builder.UseInMemory("TestInMemory")
            .WithLifetime(ServiceLifetime.Transient)
            .Build();

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<InMemoryFileStorageProvider>();
        //builder.Lifetime.ShouldBe(ServiceLifetime.Transient);
    }

    [Fact]
    public void Builder_WithoutProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = this.CreateServiceProvider();
        var builder = new FileStorageBuilder(services);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => builder.Build())
            .Message.ShouldContain("No provider has been configured");
    }

    [Fact]
    public void Builder_UseCustomProviderViaType_ResolvesFromDI_Succeeds()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddLogging()
            .AddMemoryCache()
            .AddScoped<CustomFileStorageProvider>() // Register custom provider in DI
            .BuildServiceProvider();
        var builder = new FileStorageBuilder(services);

        // Act
        var provider = builder.Use<CustomFileStorageProvider>().Build();

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<CustomFileStorageProvider>();
        provider.LocationName.ShouldBe("CustomStorage");
    }

    [Fact]
    public void Builder_UseCustomProviderViaInstance_CreatesDirectly_Succeeds()
    {
        // Arrange
        var customProvider = new CustomFileStorageProvider();
        var services = this.CreateServiceProvider();
        var builder = new FileStorageBuilder(services);

        // Act
        var provider = builder.Use(customProvider).Build();

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeSameAs(customProvider);
        provider.LocationName.ShouldBe("CustomStorage");
    }

    [Fact]
    public void Builder_UseCustomProviderViaType_WithoutServiceProvider_Throws()
    {
        // Arrange
        var builder = new FileStorageBuilder();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => builder.Use<CustomFileStorageProvider>().Build())
            .Message.ShouldContain("Service provider is required to resolve a custom provider type");
    }

    [Fact]
    public void Builder_UseCustomProviderViaInstance_WithNullProvider_Throws()
    {
        // Arrange
        var services = this.CreateServiceProvider();
        var builder = new FileStorageBuilder(services);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => builder.Use(null).Build())
            .ParamName.ShouldBe("provider");
    }

    [Fact]
    public void Builder_WithMultipleBehaviors_AppliesInOrder()
    {
        // Arrange
        var services = this.CreateServiceProvider();
        var builder = new FileStorageBuilder(services);

        // Act
        var provider = builder.UseInMemory("TestInMemory")
            .WithLogging()
            .WithCaching()
            .WithRetry()
            .Build();

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<RetryFileStorageBehavior>();
        var retryBehavior = provider as RetryFileStorageBehavior;
        retryBehavior.InnerProvider.ShouldBeOfType<CachingFileStorageBehavior>();
        var cachingBehavior = retryBehavior.InnerProvider as CachingFileStorageBehavior;
        cachingBehavior.InnerProvider.ShouldBeOfType<LoggingFileStorageBehavior>();
        var loggingBehavior = cachingBehavior.InnerProvider as LoggingFileStorageBehavior;
        loggingBehavior.InnerProvider.ShouldBeOfType<InMemoryFileStorageProvider>();
    }
}