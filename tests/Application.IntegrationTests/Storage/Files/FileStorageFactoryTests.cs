// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using BridgingIT.DevKit.Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

[IntegrationTest("Storage")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class FileStorageFactoryTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
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
    public void AddFileStorage_RegistersMultipleProviders_Succeeds()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddFileStorage(c => c
            .WithProvider("inMemory", builder =>
                {
                    builder.UseInMemoryProvider("TestInMemory")
                        .WithLoggingBehavior() // behavior
                        .WithBehavior(p => new CustomBehavior(p)) // behavior
                        .WithLifetime(ServiceLifetime.Transient);
                })
            .WithProvider("local", builder =>
            {
                builder.UseLocalProvider(
                    Path.Combine(Path.GetTempPath(), "TestStorage_" + Guid.NewGuid().ToString()),
                    "TestLocal")
                    .WithLoggingBehavior() // behavior
                    .WithLifetime(ServiceLifetime.Singleton);
            }, ServiceLifetime.Singleton));

        var services = serviceCollection.BuildServiceProvider(); ;
        var factory = services.GetRequiredService<IFileStorageFactory>();

        // Act & Assert - InMemory (Transient)
        var inMemoryProvider = factory.CreateProvider("inMemory");
        inMemoryProvider.ShouldNotBeNull();
        //inMemoryProvider.ShouldBeOfType<InMemoryFileStorageProvider>();
        inMemoryProvider.LocationName.ShouldBe("TestInMemory");

        // Act & Assert - Local (Singleton)
        var localProvider = factory.CreateProvider("local");
        localProvider.ShouldNotBeNull();
        //localProvider.ShouldBeOfType<LocalFileStorageProvider>();
        localProvider.LocationName.ShouldBe("TestLocal");
    }

    [Fact]
    public void Factory_RegistersMultipleProviders_Succeeds()
    {
        // Arrange
        var services = this.CreateServiceProvider();
        var factory = new FileStorageFactory(services);

        factory.WithProvider("inMemory", builder =>
        {
            builder.UseInMemoryProvider("TestInMemory")
                .WithLifetime(ServiceLifetime.Transient);
        }, ServiceLifetime.Transient);

        factory.WithProvider("local", builder =>
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "TestStorage_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);
            builder.UseLocalProvider(tempPath, "TestLocal")
                .WithLifetime(ServiceLifetime.Singleton);
        }, ServiceLifetime.Singleton);

        // Act & Assert - InMemory (Transient)
        var inMemoryProvider = factory.CreateProvider("inMemory");
        inMemoryProvider.ShouldNotBeNull();
        inMemoryProvider.ShouldBeOfType<InMemoryFileStorageProvider>();
        inMemoryProvider.LocationName.ShouldBe("TestInMemory");

        // Act & Assert - Local (Singleton)
        var localProvider = factory.CreateProvider("local");
        localProvider.ShouldNotBeNull();
        localProvider.ShouldBeOfType<LocalFileStorageProvider>();
        localProvider.LocationName.ShouldBe("TestLocal");
    }

    [Fact]
    public void Factory_RegistersProviderWithBehaviors_Succeeds()
    {
        // Arrange
        var services = this.CreateServiceProvider();
        var factory = new FileStorageFactory(services);

        factory.WithProvider("inMemory", builder =>
        {
            builder.UseInMemoryProvider("TestInMemory")
                .WithLoggingBehavior()
                .WithCachingBehavior()
                .WithRetryBehavior()
                .WithLifetime(ServiceLifetime.Transient);
        }, ServiceLifetime.Transient);

        // Act
        var provider = factory.CreateProvider("inMemory");

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<RetryFileStorageBehavior>(); // Last behavior applied
        var retryBehavior = provider as RetryFileStorageBehavior;
        retryBehavior.InnerProvider.ShouldBeOfType<CachingFileStorageBehavior>();
        var cachingBehavior = retryBehavior.InnerProvider as CachingFileStorageBehavior;
        cachingBehavior.InnerProvider.ShouldBeOfType<LoggingFileStorageBehavior>();
        var loggingBehavior = cachingBehavior.InnerProvider as LoggingFileStorageBehavior;
        loggingBehavior.InnerProvider.ShouldBeOfType<InMemoryFileStorageProvider>();
    }

    [Fact]
    public void Factory_RegistersCustomBehavior_Succeeds()
    {
        // Arrange
        var services = this.CreateServiceProvider();
        var factory = new FileStorageFactory(services);

        factory.WithProvider("inMemory", builder =>
        {
            builder.UseInMemoryProvider("TestInMemory")
                .WithLifetime(ServiceLifetime.Transient);
        }, ServiceLifetime.Transient);

        // Act
        var initialProvider = factory.CreateProvider("inMemory");
        factory.WithBehavior("inMemory", (p, sp) => new CustomBehavior(p));
        var providerWithCustomBehavior = factory.CreateProvider("inMemory");

        // Assert
        initialProvider.ShouldNotBeNull();
        initialProvider.ShouldBeOfType<InMemoryFileStorageProvider>();
        providerWithCustomBehavior.ShouldNotBeNull();
        providerWithCustomBehavior.ShouldBeOfType<CustomBehavior>();
        var customBehavior = providerWithCustomBehavior as CustomBehavior;
        customBehavior.InnerProvider.ShouldBeOfType<InMemoryFileStorageProvider>();
    }

    [Fact]
    public void Factory_CreateProviderByType_Succeeds()
    {
        // Arrange
        var services = this.CreateServiceProvider();
        var factory = new FileStorageFactory(services);

        factory.WithProvider("inMemory", builder =>
        {
            builder.UseInMemoryProvider("InMemoryTest")
                .WithLifetime(ServiceLifetime.Transient);
        }, ServiceLifetime.Transient);

        // Act
        var provider = factory.CreateProvider<InMemoryFileStorageProvider>();

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<InMemoryFileStorageProvider>();
        provider.LocationName.ShouldBe("InMemoryTest");
    }

    [Fact]
    public void Factory_CreateProviderByType_WithMultipleProviders_Throws()
    {
        // Arrange
        var services = this.CreateServiceProvider();
        var factory = new FileStorageFactory(services);

        factory.WithProvider("inMemory1", builder =>
        {
            builder.UseInMemoryProvider("InMemory1")
                .WithLifetime(ServiceLifetime.Transient);
        }, ServiceLifetime.Transient);

        factory.WithProvider("inMemory2", builder =>
        {
            builder.UseInMemoryProvider("InMemory2")
                .WithLifetime(ServiceLifetime.Transient);
        }, ServiceLifetime.Transient);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => factory.CreateProvider<InMemoryFileStorageProvider>())
            .Message.ShouldContain("Multiple file storage providers of type InMemoryFileStorageProvider are registered with names: ");
    }

    [Fact]
    public void Factory_WithLifetime_AppliesCorrectLifetime()
    {
        // Arrange
        var services = this.CreateServiceProvider();
        var factory = new FileStorageFactory(services);

        factory.WithProvider("inMemory", builder =>
        {
            builder.UseInMemoryProvider("TestInMemory")
                .WithLifetime(ServiceLifetime.Singleton);
        }, ServiceLifetime.Singleton);

        // Act
        var provider1 = factory.CreateProvider("inMemory");
        var provider2 = factory.CreateProvider("inMemory");

        // Assert
        provider1.ShouldNotBeNull();
        provider1.ShouldBeOfType<InMemoryFileStorageProvider>();
        provider2.ShouldBeSameAs(provider1); // Singleton ensures same instance
    }

    [Fact]
    public void Factory_WithoutRegisteredProvider_ThrowsKeyNotFoundException()
    {
        // Arrange
        var services = this.CreateServiceProvider();
        var factory = new FileStorageFactory(services);

        // Act & Assert
        Should.Throw<KeyNotFoundException>(() => factory.CreateProvider("nonexistent"))
            .Message.ShouldContain("No file storage provider registered with name 'nonexistent'");
    }

    [Fact]
    public void Factory_RegisterProvider_WithDuplicateName_Throws()
    {
        // Arrange
        var services = this.CreateServiceProvider();
        var factory = new FileStorageFactory(services);

        factory.WithProvider("inMemory", builder =>
        {
            builder.UseInMemoryProvider("TestInMemory")
                .WithLifetime(ServiceLifetime.Transient);
        }, ServiceLifetime.Transient);

        // Act & Assert
        Should.Throw<ArgumentException>(() => factory.WithProvider("inMemory", builder =>
        {
            builder.UseInMemoryProvider("AnotherInMemory")
                .WithLifetime(ServiceLifetime.Transient);
        }, ServiceLifetime.Transient))
            .Message.ShouldContain("A provider with name 'inMemory' is already registered");
    }

    [Fact]
    public void Factory_RegisterProvider_WithNullName_Throws()
    {
        // Arrange
        var services = this.CreateServiceProvider();
        var factory = new FileStorageFactory(services);

        // Act & Assert
        Should.Throw<ArgumentException>(() => factory.WithProvider(null, builder =>
        {
            builder.UseInMemoryProvider("TestInMemory")
                .WithLifetime(ServiceLifetime.Transient);
        }, ServiceLifetime.Transient))
            .Message.ShouldContain("Provider name cannot be null or empty");
    }
}