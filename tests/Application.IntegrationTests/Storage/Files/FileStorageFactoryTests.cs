// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.Windows.Storage;
using BridgingIT.DevKit.Infrastructure.Azure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using System.Threading.Tasks;
using System.Text;

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

        // Act & Assert - Network (Singleton)
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
                .WithBehavior((p, sp) => new CustomBehavior(p)) // behavior
                .WithLifetime(ServiceLifetime.Transient);
        });

        // Act
        var provider = factory.CreateProvider("inMemory");

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<CustomBehavior>();
        var behavior = provider as CustomBehavior;
        behavior.InnerProvider.ShouldBeOfType<InMemoryFileStorageProvider>();
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

    [Fact]
    public void AddFileStorage_RegistersExternalProviders_Succeeds()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddLogging()
            .AddMemoryCache();

        services.AddFileStorage(c => c
            .RegisterProvider("azureBlob", builder =>
            {
                builder.UseAzureBlob("AzureBlobStorage", "connection-string", "container-name")
                       .WithCaching(new CachingOptions { CacheDuration = TimeSpan.FromMinutes(10) })
                       .WithLifetime(ServiceLifetime.Scoped);
            })
            .RegisterProvider("azureFiles", builder =>
            {
                builder.UseAzureFiles("AzureFilesStorage", "connection-string", "share-name")
                       .WithLogging()
                       .WithLifetime(ServiceLifetime.Scoped);
            }));

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IFileStorageFactory>();

        // Act & Assert - Azure Blob (Scoped)
        var azureBlobProvider = factory.CreateProvider("azureBlob");
        azureBlobProvider.ShouldNotBeNull();
        //azureBlobProvider.ShouldBeOfType<AzureBlobFileStorageProvider>();
        azureBlobProvider.LocationName.ShouldBe("AzureBlobStorage");

        // Act & Assert - Azure Files (Scoped)
        var azureFilesProvider = factory.CreateProvider("azureFiles");
        azureFilesProvider.ShouldNotBeNull();
        //azureFilesProvider.ShouldBeOfType<AzureFilesFileStorageProvider>();
        azureFilesProvider.LocationName.ShouldBe("AzureFilesStorage");
    }

    [Fact]
    public async Task LocalProvider_BasicFileOperations_Succeed()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), "TestStorage_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);

        var services = new ServiceCollection()
            .AddLogging()
            .AddMemoryCache();

        services.AddFileStorage(c => c
            .RegisterProvider("local", builder =>
            {
                builder.UseLocal(tempPath, "TestLocal")
                       .WithLogging()
                       .WithLifetime(ServiceLifetime.Singleton);
            }));

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IFileStorageFactory>();
        var provider = factory.CreateProvider("local");

        // Act - Write a file
        var content = new MemoryStream(Encoding.UTF8.GetBytes("Test content"));
        var writeResult = await provider.WriteFileAsync("test.txt", content, null, CancellationToken.None);

        // Assert - Write
        writeResult.ShouldBeSuccess("Write should succeed");

        // Act - Read the file
        var readResult = await provider.ReadFileAsync("test.txt", null, CancellationToken.None);

        // Assert - Read
        readResult.ShouldBeSuccess("Read should succeed");
        using (var stream = readResult.Value)
        {
            using var reader = new StreamReader(stream);
            var readContent = await reader.ReadToEndAsync();
            readContent.ShouldBe("Test content");
        }

        // Act - Delete the file
        var deleteResult = await provider.DeleteFileAsync("test.txt", null, CancellationToken.None);

        // Assert - Delete
        deleteResult.ShouldBeSuccess("Delete should succeed");

        // Cleanup
        Directory.Delete(tempPath, true);
    }

    [Fact]
    public void Factory_RegistersExternalProviderWithBehaviors_Succeeds()
    {
        // Arrange
        var serviceProvider = this.CreateServiceProvider();
        var factory = new FileStorageFactory(serviceProvider);

        factory.RegisterNetworkFileStorageProvider("network", builder =>
        {
            builder.UseWindowsNetwork("NetworkStorage", @"\\server\docs", "username", "password", "domain")
                   .WithLogging()
                   .WithRetry(new RetryOptions { MaxRetries = 3 })
                   .WithLifetime(ServiceLifetime.Singleton);
        });

        // Act
        var provider = factory.CreateProvider("network");

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<RetryFileStorageBehavior>(); // Last behavior applied
        var retryBehavior = provider as RetryFileStorageBehavior;
        retryBehavior.InnerProvider.ShouldBeOfType<LoggingFileStorageBehavior>();
        var loggingBehavior = retryBehavior.InnerProvider as LoggingFileStorageBehavior;
        loggingBehavior.InnerProvider.ShouldBeOfType<NetworkFileStorageProvider>();
        loggingBehavior.InnerProvider.LocationName.ShouldBe("NetworkStorage");
    }

    [Fact]
    public async Task LocalProvider_FileOperations_WithProgressReporting_Succeeds()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), "TestStorage_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);

        var services = new ServiceCollection()
            .AddLogging()
            .AddMemoryCache();

        services.AddFileStorage(c => c
            .RegisterProvider("local", builder =>
            {
                builder.UseLocal(tempPath, "TestLocal")
                       .WithLogging()
                       .WithLifetime(ServiceLifetime.Singleton);
            }));

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IFileStorageFactory>();
        var provider = factory.CreateProvider("local");

        // Act - Write a file with progress reporting
        var progressUpdates = new List<FileProgress>();
        var progress = new Progress<FileProgress>(p => progressUpdates.Add(p));
        var content = new MemoryStream(Encoding.UTF8.GetBytes("Test content for progress"));
        var writeResult = await provider.WriteFileAsync("test.txt", content, progress, CancellationToken.None);

        // Assert - Write with progress
        writeResult.ShouldBeSuccess("Write should succeed");
        progressUpdates.ShouldNotBeEmpty("Progress updates should be reported");
        progressUpdates.Last().BytesProcessed.ShouldBeGreaterThan(0);

        // Act - Read the file with progress reporting
        progressUpdates.Clear();
        var readResult = await provider.ReadFileAsync("test.txt", progress, CancellationToken.None);

        // Assert - Read with progress
        readResult.ShouldBeSuccess("Read should succeed");
        progressUpdates.ShouldNotBeEmpty("Progress updates should be reported during read");
        progressUpdates.Last().BytesProcessed.ShouldBeGreaterThan(0);

        // Cleanup
        Directory.Delete(tempPath, true);
    }

    //[Fact]
    //public void Factory_RegistersProvider_WithoutUseConfiguration_Throws()
    //{
    //    // Arrange
    //    var serviceProvider = this.CreateServiceProvider();
    //    var factory = new FileStorageFactory(serviceProvider);

    //    // Act & Assert
    //    Should.Throw<InvalidOperationException>(() =>
    //        factory.RegisterProvider("inMemory", builder =>
    //        {
    //            // Intentionally not calling UseInMemory or any UseXXX method
    //            builder.WithLogging()
    //                   .WithLifetime(ServiceLifetime.Transient);
    //        })).Message.ShouldContain("Provider configuration must be specified before building");
    //}

    [Fact]
    public void Factory_CreateProviderByType_ExternalProvider_Succeeds()
    {
        // Arrange
        var serviceProvider = this.CreateServiceProvider();
        var factory = new FileStorageFactory(serviceProvider);

        factory.RegisterAzureBlobProvider("azureBlob", builder =>
        {
            builder.UseAzureBlob("AzureBlobStorage", "connection-string", "container-name")
                   .WithLifetime(ServiceLifetime.Scoped);
        });

        // Act
        var provider = factory.CreateProvider<AzureBlobFileStorageProvider>();

        // Assert
        provider.ShouldNotBeNull();
        //provider.ShouldBeOfType<AzureBlobFileStorageProvider>();
        provider.LocationName.ShouldBe("AzureBlobStorage");
    }

    [Fact]
    public async Task Factory_CreateProvider_ConcurrentAccess_Succeeds()
    {
        // Arrange
        var serviceProvider = this.CreateServiceProvider();
        var factory = new FileStorageFactory(serviceProvider);

        factory.RegisterProvider("inMemory", builder =>
        {
            builder.UseInMemory("TestSingleton")
                   .WithLifetime(ServiceLifetime.Singleton);
        });

        factory.RegisterProvider("transient", builder =>
        {
            builder.UseInMemory("TestTransient")
                   .WithLifetime(ServiceLifetime.Transient);
        });

        // Act - Create providers concurrently
        var tasks = new List<Task<IFileStorageProvider>>();
        for (var i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() => factory.CreateProvider("inMemory")));
            tasks.Add(Task.Run(() => factory.CreateProvider("transient")));
        }
        var providers = await Task.WhenAll(tasks); // 10 providers each for Singleton and Transient

        // Assert - Singleton providers should be the same instance
        var singletonProviders = providers.Where(p => p.LocationName == "TestSingleton").ToList();
        singletonProviders.All(p => p == singletonProviders[0]).ShouldBeTrue("Singleton providers should be the same instance");

        // Assert - Transient providers should be different instances
        var transientProviders = providers.Where(p => p.LocationName == "TestTransient").ToList();
        transientProviders.Distinct().Count().ShouldBe(transientProviders.Count, "Transient providers should be different instances");
    }
}