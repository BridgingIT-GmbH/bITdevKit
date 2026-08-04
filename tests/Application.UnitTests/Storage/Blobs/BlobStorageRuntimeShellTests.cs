// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

[UnitTest("Application")]
public class BlobStorageRuntimeShellTests
{
    private static readonly BlobKey HealthProbeKey = new("__bdk", "healthcheck/probe");

    [Fact]
    public void AddBlobStorage_WithEnabledOptions_RegistersFeatureOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBlobStorage(options => options.Enabled(true));
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetRequiredService<BlobStorageOptions>().IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void AddBlobStorage_WhenConfigured_RegistersDiagnosticsService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBlobStorage();
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetRequiredService<IBlobStorageDiagnosticsService>()
            .ShouldBeOfType<BlobStorageDiagnosticsService>();
        services.Count(descriptor => descriptor.ServiceType == typeof(IBlobStorageDiagnosticsService)).ShouldBe(1);
    }

    [Fact]
    public void WithUploadConcurrencyBehavior_RegistersOneSharedCoordinator()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBlobStorage()
            .WithUploadConcurrencyBehavior()
            .WithInMemoryClient("reports");
        using var serviceProvider = services.BuildServiceProvider();
        using var firstScope = serviceProvider.CreateScope();
        using var secondScope = serviceProvider.CreateScope();

        // Act
        var first = firstScope.ServiceProvider
            .GetRequiredService<IBlobUploadAdmissionCoordinator>();
        var second = secondScope.ServiceProvider
            .GetRequiredService<IBlobUploadAdmissionCoordinator>();

        // Assert
        second.ShouldBeSameAs(first);
    }

    [Fact]
    public void AddBlobStorage_WithoutUploadConcurrencyBehavior_DoesNotRegisterCoordinator()
    {
        var services = new ServiceCollection();
        services.AddBlobStorage().WithInMemoryClient("reports");
        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetService<IBlobUploadAdmissionCoordinator>().ShouldBeNull();
    }

    [Fact]
    public void WithUploadConcurrencyBehavior_WhenRegisteredTwice_Throws()
    {
        var services = new ServiceCollection();
        var builder = services.AddBlobStorage().WithUploadConcurrencyBehavior();

        Should.Throw<InvalidOperationException>(() =>
            builder.WithUploadConcurrencyBehavior());
    }

    [Theory]
    [InlineData(0, 16, 30)]
    [InlineData(4, -1, 30)]
    [InlineData(4, 16, 0)]
    public void WithUploadConcurrencyBehavior_WithInvalidOptions_Throws(
        int maxConcurrent,
        int maxQueued,
        int timeoutSeconds)
    {
        var services = new ServiceCollection();

        Should.Throw<InvalidOperationException>(() =>
            services.AddBlobStorage().WithUploadConcurrencyBehavior(options =>
            {
                options.MaxConcurrentUploads = maxConcurrent;
                options.MaxQueuedUploads = maxQueued;
                options.QueueWaitTimeout = TimeSpan.FromSeconds(timeoutSeconds);
            }));
    }

    [Fact]
    public void AddBlobStorage_WhenConfigured_RegistersRetentionBackgroundService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddLogging();
        services.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
        services.AddBlobStorage();
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        var retention = serviceProvider.GetRequiredService<BlobRetentionBackgroundService>();
        serviceProvider.GetServices<IHostedService>().ShouldContain(retention);
    }

    [Fact]
    public async Task DiagnosticsSnapshot_WithoutRegisteredClients_ReturnsEmptySnapshot()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBlobStorage();
        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var result = await serviceProvider.GetRequiredService<IBlobStorageDiagnosticsService>().GetSnapshotAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ClientCount.ShouldBe(0);
        result.Value.HealthyClientCount.ShouldBe(0);
        result.Value.UnhealthyClientCount.ShouldBe(0);
        result.Value.Clients.ShouldBeEmpty();
    }

    [Fact]
    public async Task DiagnosticsSnapshot_WithUploadAdmission_ReportsLimitsAndCounts()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBlobStorage()
            .WithUploadConcurrencyBehavior(options =>
            {
                options.MaxConcurrentUploads = 3;
                options.MaxQueuedUploads = 7;
            })
            .WithInMemoryClient("reports");
        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var result = await serviceProvider
            .GetRequiredService<IBlobStorageDiagnosticsService>()
            .GetSnapshotAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var client = result.Value.Clients.Single();
        client.UploadAdmissionEnabled.ShouldBeTrue();
        client.MaxConcurrentUploads.ShouldBe(3);
        client.MaxQueuedUploads.ShouldBe(7);
        client.ActiveUploads.ShouldBe(0);
        client.QueuedUploads.ShouldBe(0);
    }

    [Fact]
    public void AddBlobStorage_WhenDisabled_DoesNotRegisterRuntimeClientsOrHealthChecks()
    {
        // Arrange
        var provider = CreateProvider();
        var services = new ServiceCollection();

        // Act
        services.AddBlobStorage(options => options.Enabled(false))
            .WithClient("reports", _ => provider);
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetRequiredService<BlobStorageOptions>().IsEnabled.ShouldBeFalse();
        serviceProvider.GetService<IBlobStoreClientFactory>().ShouldBeNull();
        serviceProvider.GetService<HealthCheckService>().ShouldBeNull();
        services.Any(descriptor => descriptor.ServiceType == typeof(BlobStoreClientRegistration)).ShouldBeFalse();
    }

    [Fact]
    public async Task CreateClient_WithRegisteredName_ReturnsConfiguredClient()
    {
        // Arrange
        var provider = CreateProvider();
        provider.ExistsAsync(new BlobKey("reports", "probe"), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        var services = new ServiceCollection();
        services.AddBlobStorage()
            .WithClient("reports", _ => provider, providerName: "Test");
        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var client = serviceProvider.GetRequiredService<IBlobStoreClientFactory>().CreateClient("reports");
        var result = await client.ExistsAsync(new BlobKey("reports", "probe"));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
        await provider.Received(1).ExistsAsync(new BlobKey("reports", "probe"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateClient_WithInMemoryProvider_ExposesSortedContainerCatalog()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBlobStorage()
            .WithInMemoryClient("reports");
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IBlobStoreClientFactory>().CreateClient("reports");
        using var firstContent = new MemoryStream([1]);
        using var secondContent = new MemoryStream([2]);
        using var thirdContent = new MemoryStream([3]);
        await client.UploadAsync(new BlobUpload { Key = new BlobKey("zeta", "one.bin"), Content = firstContent });
        await client.UploadAsync(new BlobUpload { Key = new BlobKey("alpha", "two.bin"), Content = secondContent });
        await client.UploadAsync(new BlobUpload { Key = new BlobKey("zeta", "three.bin"), Content = thirdContent });

        // Act
        var result = await client.ShouldBeAssignableTo<IBlobStoreContainerCatalog>().ListContainersAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        result.Value.ShouldBe(["alpha", "zeta"]);
    }

    [Fact]
    public async Task CreateClient_WithSingletonLifetime_RemainsUsableAcrossDisposedScopes()
    {
        // Arrange
        var provider = CreateProvider();
        provider.ExistsAsync(Arg.Any<BlobKey>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
        services.AddBlobStorage(options => options.UseLifetime(ServiceLifetime.Singleton))
            .WithClient("reports", _ => provider);
        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        IBlobStoreClient first;
        using (var firstScope = serviceProvider.CreateScope())
        {
            first = firstScope.ServiceProvider.GetRequiredService<IBlobStoreClientFactory>().CreateClient("reports");
        }

        IBlobStoreClient second;
        Result<bool> operation;
        using (var secondScope = serviceProvider.CreateScope())
        {
            second = secondScope.ServiceProvider.GetRequiredService<IBlobStoreClientFactory>().CreateClient("reports");
            operation = await second.ExistsAsync(new BlobKey("reports", "probe.bin"));
        }

        // Assert
        second.ShouldBeSameAs(first);
        operation.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, operation.Errors.Select(e => e.Message)));
    }

    [Fact]
    public void CreateClient_WithScopedLifetime_ReturnsSameClientWithinScopeOnly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBlobStorage()
            .WithClient("reports", _ => CreateProvider());
        using var serviceProvider = services.BuildServiceProvider();
        using var firstScope = serviceProvider.CreateScope();
        using var secondScope = serviceProvider.CreateScope();

        // Act
        var firstFactory = firstScope.ServiceProvider.GetRequiredService<IBlobStoreClientFactory>();
        var first = firstFactory.CreateClient("reports");
        var second = firstFactory.CreateClient("reports");
        var third = secondScope.ServiceProvider.GetRequiredService<IBlobStoreClientFactory>().CreateClient("reports");

        // Assert
        second.ShouldBeSameAs(first);
        third.ShouldNotBeSameAs(first);
    }

    [Fact]
    public void CreateClient_WithTransientLifetime_ReturnsNewClientEveryCall()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBlobStorage()
            .WithClient("reports", _ => CreateProvider(), lifetime: ServiceLifetime.Transient);
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IBlobStoreClientFactory>();

        // Act
        var first = factory.CreateClient("reports");
        var second = factory.CreateClient("reports");

        // Assert
        second.ShouldNotBeSameAs(first);
    }

    [Fact]
    public async Task ListPageAsync_WithRegisteredTokenProtector_UsesAndRequiresProtectedTokens()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IContinuationTokenProtector>(new HmacContinuationTokenProtector(new byte[32]));
        services.AddBlobStorage()
            .WithInMemoryClient("reports", options =>
            {
                options.DefaultTake = 1;
                options.MaxTake = 1;
            });
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IBlobStoreClientFactory>().CreateClient("reports");
        await client.UploadAsync(new BlobUpload
        {
            Key = new BlobKey("reports", "paging/a.txt"),
            Content = new MemoryStream([1])
        });
        await client.UploadAsync(new BlobUpload
        {
            Key = new BlobKey("reports", "paging/b.txt"),
            Content = new MemoryStream([2])
        });

        // Act
        var first = await client.ListPageAsync(new BlobQuery { Container = "reports", Prefix = "paging/", Take = 1 });
        var second = await client.ListPageAsync(new BlobQuery
        {
            Container = "reports",
            Prefix = "paging/",
            Take = 1,
            ContinuationToken = first.Value.ContinuationToken
        });
        var unsigned = BlobContinuationTokenSerializer.Serialize(new BlobContinuationToken
        {
            Provider = InMemoryBlobStoreProvider.ProviderName,
            QueryHash = BlobQueryHash.Compute(new BlobQuery { Container = "reports", Prefix = "paging/", Take = 1 }),
            Container = "reports",
            Name = "paging/a.txt"
        });
        var rejected = await client.ListPageAsync(new BlobQuery
        {
            Container = "reports",
            Prefix = "paging/",
            Take = 1,
            ContinuationToken = unsigned.Value
        });

        // Assert
        first.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, first.Errors.Select(e => e.Message)));
        first.Value.ContinuationToken.ShouldStartWith("p1.");
        second.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, second.Errors.Select(e => e.Message)));
        second.Value.Items.Single().Key.Name.ShouldBe("paging/b.txt");
        rejected.IsFailure.ShouldBeTrue();
        rejected.HasError<BlobStoreInvalidContinuationTokenError>().ShouldBeTrue();
    }

    [Fact]
    public void CreateClient_WithUnknownName_ThrowsDeterministically()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBlobStorage()
            .WithClient("reports", _ => CreateProvider());
        using var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IBlobStoreClientFactory>();

        // Act
        var action = () => factory.CreateClient("missing");

        // Assert
        var exception = action.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain("missing");
    }

    [Fact]
    public void WithClient_WithDuplicateName_ThrowsDeterministically()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var action = () => services.AddBlobStorage()
            .WithClient("reports", _ => CreateProvider())
            .WithClient("Reports", _ => CreateProvider());

        // Assert
        var exception = action.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain("reports");
    }

    [Fact]
    public void GetRegistrations_WithNamedClients_ReturnsNamesProviderNamesAndCapabilities()
    {
        // Arrange
        var capabilities = new BlobStoreProviderCapabilities
        {
            SupportsContinuationPaging = true,
            SupportsPrefixListing = true,
            SupportsContentHash = true
        };
        var services = new ServiceCollection();
        services.AddBlobStorage()
            .WithClient("reports", _ => CreateProvider(capabilities), providerName: "TestProvider", capabilities: capabilities);
        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var registration = serviceProvider.GetRequiredService<IBlobStoreClientFactory>()
            .GetRegistrations()
            .Single();

        // Assert
        registration.Name.ShouldBe("reports");
        registration.ProviderName.ShouldBe("TestProvider");
        registration.Capabilities.SupportsPrefixListing.ShouldBeTrue();
        registration.Capabilities.SupportsContentHash.ShouldBeTrue();
    }

    [Fact]
    public async Task BlobStoreClient_WithInvalidUpload_ValidatesBeforeProviderInvocation()
    {
        // Arrange
        var provider = CreateProvider();
        var sut = new BlobStoreClient("provider", provider, new BlobStoreOptions());
        var upload = new BlobUpload
        {
            Key = new BlobKey("", "file.bin"),
            Content = new MemoryStream([1, 2, 3])
        };

        // Act
        var result = await sut.UploadAsync(upload);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreValidationError>().ShouldBeTrue();
        await provider.DidNotReceive().UploadAsync(Arg.Any<BlobUpload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HealthCheck_WithRegisteredClients_ProbesEveryConfiguredClient()
    {
        // Arrange
        var firstProvider = CreateProvider();
        var secondProvider = CreateProvider();
        firstProvider.ExistsAsync(HealthProbeKey, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        secondProvider.ExistsAsync(HealthProbeKey, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBlobStorage()
            .WithClient("reports", _ => firstProvider)
            .WithClient("media", _ => secondProvider);
        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var report = await serviceProvider.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        // Assert
        report.Status.ShouldBe(HealthStatus.Healthy);
        report.Entries.Keys.Single().ShouldBe("BlobStorage");
        await firstProvider.Received(1).ExistsAsync(HealthProbeKey, Arg.Any<CancellationToken>());
        await secondProvider.Received(1).ExistsAsync(HealthProbeKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HealthCheck_WithMissingProbeBlob_ReturnsHealthy()
    {
        // Arrange
        var provider = CreateProvider();
        provider.ExistsAsync(HealthProbeKey, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Failure(new BlobStoreNotFoundError(HealthProbeKey)));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBlobStorage()
            .WithClient("reports", _ => provider);
        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var report = await serviceProvider.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        // Assert
        report.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task HealthCheck_WithProviderFailure_ReturnsUnhealthyAndReadableFailedClientNames()
    {
        // Arrange
        var healthyProvider = CreateProvider();
        var failingProvider = CreateProvider();
        healthyProvider.ExistsAsync(HealthProbeKey, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        failingProvider.ExistsAsync(HealthProbeKey, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Failure(new BlobStoreProviderError("backend unavailable")));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBlobStorage()
            .WithClient("healthy", _ => healthyProvider)
            .WithClient("failing", _ => failingProvider);
        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var report = await serviceProvider.GetRequiredService<HealthCheckService>().CheckHealthAsync();
        var entry = report.Entries["BlobStorage"];

        // Assert
        report.Status.ShouldBe(HealthStatus.Unhealthy);
        entry.Description.ShouldContain("failing");
        entry.Data["failedClientCount"].ShouldBe(1);
        entry.Data["failedClients"].ShouldBeOfType<string>().ShouldBe("failing");
        entry.Data["clientErrors"].ShouldBeOfType<string>().ShouldContain("backend unavailable");
    }

    private static IBlobStoreProvider CreateProvider(BlobStoreProviderCapabilities capabilities = null)
    {
        var provider = Substitute.For<IBlobStoreProvider>();
        provider.Capabilities.Returns(capabilities ?? new BlobStoreProviderCapabilities
        {
            SupportsContinuationPaging = true,
            SupportsPrefixListing = true,
            SupportsFullContainerScan = true
        });

        return provider;
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource started = new();
        private readonly CancellationTokenSource stopping = new();
        private readonly CancellationTokenSource stopped = new();

        public CancellationToken ApplicationStarted => this.started.Token;

        public CancellationToken ApplicationStopping => this.stopping.Token;

        public CancellationToken ApplicationStopped => this.stopped.Token;

        public void StopApplication()
        {
            this.stopping.Cancel();
        }
    }
}
