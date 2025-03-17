// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.Windows.Storage;
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
        var services = new ServiceCollection()
            .AddLogging()
            .AddMemoryCache();

        services.AddFileStorage(c => c
            .RegisterProvider("inMemory", builder =>
            {
                builder.UseInMemory("TestInMemory")
                    .WithLogging() // behavior
                    .WithBehavior((p, sp) => new CustomBehavior(p)) // behavior
                    .WithLifetime(ServiceLifetime.Transient);
            })
            .RegisterProvider("network", builder =>
            {
                builder.UseWindowsNetwork("NetworkStorage", @"\\server\docs", "username", "password", "domain")
                       .WithLogging()
                       .WithRetry(new RetryOptions { MaxRetries = 3 })
                       .WithLifetime(ServiceLifetime.Singleton);
            })
            .RegisterProvider("local", builder =>
            {
                builder.UseLocal("TestLocal", Path.Combine(Path.GetTempPath(), "TestStorage_" + Guid.NewGuid().ToString()))
                    .WithLogging() // behavior
                    .WithLifetime(ServiceLifetime.Singleton);
            }));

        var serviceProvider = services.BuildServiceProvider(); ;
        var factory = serviceProvider.GetRequiredService<IFileStorageFactory>();

        // Act & Assert - InMemory (Transient)
        var inMemoryProvider = factory.CreateProvider("inMemory");
        inMemoryProvider.ShouldNotBeNull();
        //inMemoryProvider.ShouldBeOfType<InMemoryFileStorageProvider>();
        inMemoryProvider.LocationName.ShouldBe("TestInMemory");

        // Act & Assert - Local (Singleton)
        var networkProvider = factory.CreateProvider("network");
        networkProvider.ShouldNotBeNull();
        //networkProvider.ShouldBeOfType<NetworkFileStorageProvider>();
        networkProvider.LocationName.ShouldBe("NetworkStorage");

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
        var serviceProvider = this.CreateServiceProvider();
        var factory = new FileStorageFactory(serviceProvider);

        factory.RegisterProvider("inMemory", builder =>
        {
            builder.UseInMemory("TestInMemory")
                .WithLifetime(ServiceLifetime.Transient);
        });

        factory.RegisterProvider("local", builder =>
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "TestStorage_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);
            builder.UseLocal("TestLocal", tempPath)
                .WithLifetime(ServiceLifetime.Singleton);
        });

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
        var serviceProvider = this.CreateServiceProvider();
        var factory = new FileStorageFactory(serviceProvider);

        factory.RegisterProvider("inMemory", builder =>
        {
            builder.UseInMemory("TestInMemory")
                .WithLogging()
                .WithCaching()
                .WithRetry()
                .WithLifetime(ServiceLifetime.Transient);
        });

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
        var serviceProvider = this.CreateServiceProvider();
        var factory = new FileStorageFactory(serviceProvider);

        factory.RegisterProvider("inMemory", builder =>
        {
            builder.UseInMemory("TestInMemory")
                .WithLifetime(ServiceLifetime.Transient);
        });

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
        var serviceProvider = this.CreateServiceProvider();
        var factory = new FileStorageFactory(serviceProvider);

        factory.RegisterProvider("inMemory", builder =>
        {
            builder.UseInMemory("InMemoryTest")
                .WithLifetime(ServiceLifetime.Transient);
        });

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
        var serviceProvider = this.CreateServiceProvider();
        var factory = new FileStorageFactory(serviceProvider);

        factory.RegisterProvider("inMemory1", builder =>
        {
            builder.UseInMemory("InMemory1")
                .WithLifetime(ServiceLifetime.Transient);
        });

        factory.RegisterProvider("inMemory2", builder =>
        {
            builder.UseInMemory("InMemory2")
                .WithLifetime(ServiceLifetime.Transient);
        });

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => factory.CreateProvider<InMemoryFileStorageProvider>())
            .Message.ShouldContain("Multiple file storage providers of type InMemoryFileStorageProvider are registered with names: ");
    }

    [Fact]
    public void Factory_WithLifetime_AppliesCorrectLifetime()
    {
        // Arrange
        var serviceProvider = this.CreateServiceProvider();
        var factory = new FileStorageFactory(serviceProvider);

        factory.RegisterProvider("inMemory", builder =>
        {
            builder.UseInMemory("TestInMemory")
                .WithLifetime(ServiceLifetime.Singleton);
        });

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
        var serviceProvider = this.CreateServiceProvider();
        var factory = new FileStorageFactory(serviceProvider);

        // Act & Assert
        Should.Throw<KeyNotFoundException>(() => factory.CreateProvider("nonexistent"))
            .Message.ShouldContain("No file storage provider registered with name 'nonexistent'");
    }

    [Fact]
    public void Factory_RegisterProvider_WithDuplicateName_Throws()
    {
        // Arrange
        var serviceProvider = this.CreateServiceProvider();
        var factory = new FileStorageFactory(serviceProvider);

        factory.RegisterProvider("inMemory", builder =>
        {
            builder.UseInMemory("TestInMemory")
                .WithLifetime(ServiceLifetime.Transient);
        });

        // Act & Assert
        Should.Throw<ArgumentException>(() => factory.RegisterProvider("inMemory", builder =>
        {
            builder.UseInMemory("AnotherInMemory")
                .WithLifetime(ServiceLifetime.Transient);
        })).Message.ShouldContain("A provider with name 'inMemory' is already registered");
    }

    [Fact]
    public void Factory_RegisterProvider_WithNullName_Throws()
    {
        // Arrange
        var serviceProvider = this.CreateServiceProvider();
        var factory = new FileStorageFactory(serviceProvider);

        // Act & Assert
        Should.Throw<ArgumentException>(() => factory.RegisterProvider(null, builder =>
        {
            builder.UseInMemory("TestInMemory")
                .WithLifetime(ServiceLifetime.Transient);
        })).Message.ShouldContain("Provider name cannot be null or empty");
    }
}