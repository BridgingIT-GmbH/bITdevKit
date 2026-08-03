// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using System.Security.Cryptography;

[UnitTest("Application")]
public class DocumentStorageBuilderContextTests
{
    [Fact]
    public async Task WithPermalinks_ClientBoundaryExposesBehaviorForLinkCreation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStoragePermalinks().UseInMemory();
        services.AddDocumentStorage()
            .WithProvider<DocumentStorageBuilderPersonStub>(_ => new InMemoryDocumentStoreProvider())
            .WithPermalinks<DocumentStorageBuilderPersonStub>();
        using var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<IDocumentStoreClient<DocumentStorageBuilderPersonStub>>();
        var key = new DocumentKey("p", "1");
        await client.UpsertAsync(key, new());

        var result = await client.GetPermalinkAsync(key);

        result.IsSuccess.ShouldBeTrue(string.Join(" | ", result.Errors.Select(x => x.Message)));
        StoragePermalinkExtensions.FindDocumentAccessor(client).ShouldNotBeNull();
    }

    [Fact]
    public async Task GetAsync_WithInvalidKey_DoesNotEnterBehaviorOrProvider()
    {
        var provider = Substitute.For<IDocumentStoreProvider>();
        provider.Capabilities.Returns(new DocumentStoreProviderCapabilities());
        ProbeBehavior<DocumentStorageBuilderPersonStub> behavior = null;
        var services = new ServiceCollection();
        services.AddDocumentStorage()
            .WithBehavior<DocumentStorageBuilderPersonStub, ProbeBehavior<DocumentStorageBuilderPersonStub>>(inner => behavior = new(inner))
            .WithProvider<DocumentStorageBuilderPersonStub>(_ => provider);
        using var serviceProvider = services.BuildServiceProvider();

        var result = await serviceProvider.GetRequiredService<IDocumentStoreClient<DocumentStorageBuilderPersonStub>>()
            .GetAsync(new DocumentKey(null, "row"));

        result.IsFailure.ShouldBeTrue();
        behavior.Calls.ShouldBe(0);
        await provider.DidNotReceive().GetAsync(Arg.Any<DocumentTypeIdentity>(), Arg.Any<DocumentKey>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListPageAsync_WithProtector_BindsTokenToNamedClientAndRejectsTampering()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IContinuationTokenProtector>(new HmacContinuationTokenProtector(RandomNumberGenerator.GetBytes(32)));
        services.AddDocumentStorage()
            .WithProvider<DocumentStorageBuilderPersonStub>(_ => new InMemoryDocumentStoreProvider(),
                documentStoreOptions: new() { AllowFullScans = true }, name: "primary", isDefault: true)
            .WithProvider<DocumentStorageBuilderPersonStub>(_ => new InMemoryDocumentStoreProvider(),
                documentStoreOptions: new() { AllowFullScans = true }, name: "archive", isDefault: false);
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDocumentStoreClientFactory>();
        var primary = factory.CreateClient<DocumentStorageBuilderPersonStub>("primary");
        var archive = factory.CreateClient<DocumentStorageBuilderPersonStub>("archive");
        await primary.UpsertAsync(new("p", "1"), new());
        await primary.UpsertAsync(new("p", "2"), new());
        var query = DocumentQueries.Query().AllowFullScan().Take(1).Build();

        var first = await primary.ListPageAsync(query);
        var wrongClient = await archive.ListPageAsync(new() { AllowFullScan = true, Take = 1, ContinuationToken = first.Value.ContinuationToken });
        var token = first.Value.ContinuationToken;
        var tampered = token[..^1] + (token[^1] == 'a' ? 'b' : 'a');
        var modified = await primary.ListPageAsync(new() { AllowFullScan = true, Take = 1, ContinuationToken = tampered });

        first.IsSuccess.ShouldBeTrue();
        wrongClient.Errors.ShouldContain(x => x is DocumentStoreInvalidContinuationTokenError);
        modified.Errors.ShouldContain(x => x is DocumentStoreInvalidContinuationTokenError);
    }

    [Fact]
    public async Task WithCompressionTransform_UsesDiPipelineAndReportsIdentifier()
    {
        var provider = new InMemoryDocumentStoreProvider();
        var services = new ServiceCollection();
        services.AddDocumentStorage()
            .WithCompressionTransform<DocumentStorageBuilderPersonStub>()
            .WithProvider<DocumentStorageBuilderPersonStub>(_ => provider);
        using var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<IDocumentStoreClient<DocumentStorageBuilderPersonStub>>();
        var key = new DocumentKey("p", "compressed");

        await client.UpsertAsync(key, new() { FirstName = new string('A', 1000) });
        var read = await client.GetAsync(key);
        var descriptor = serviceProvider.GetRequiredService<IDocumentStoreClientFactory>().GetDescriptors().Single();

        read.Value.Value.FirstName.ShouldBe(new string('A', 1000));
        descriptor.TransformIdentifiers.ShouldBe(["gzip"]);
    }
    [Fact]
    public void GetRequiredService_WithBehaviorBeforeClient_ShouldResolveDecoratedClient()
    {
        // Arrange
        var provider = new InMemoryDocumentStoreProvider();
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDocumentStorage(o => o.Enabled(true))
            .WithBehavior<LoggingDocumentStoreClientBehavior<DocumentStorageBuilderPersonStub>>()
            .WithProvider<DocumentStorageBuilderPersonStub>(_ => provider);

        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var result = serviceProvider.GetRequiredService<IDocumentStoreClient<DocumentStorageBuilderPersonStub>>();

        // Assert
        ((IDocumentStoreClientIdentity)result).ClientName.ShouldBe("default");
    }

    [Fact]
    public void GetRequiredService_WithBehaviorAfterClient_ShouldResolveDecoratedClient()
    {
        // Arrange
        var provider = new InMemoryDocumentStoreProvider();
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDocumentStorage()
            .WithProvider<DocumentStorageBuilderPersonStub>(_ => provider)
            .WithBehavior<LoggingDocumentStoreClientBehavior<DocumentStorageBuilderPersonStub>>();

        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var result = serviceProvider.GetRequiredService<IDocumentStoreClient<DocumentStorageBuilderPersonStub>>();

        // Assert
        ((IDocumentStoreClientIdentity)result).ClientName.ShouldBe("default");
    }

    [Fact]
    public async Task Create_WithMultipleClients_ShouldResolveSelectedTypedClient()
    {
        // Arrange
        var personProvider = new InMemoryDocumentStoreProvider();
        var archiveProvider = new InMemoryDocumentStoreProvider();
        var key = new DocumentKey("archive", "row-1");
        var services = new ServiceCollection();
        services.AddDocumentStorage()
            .WithProvider<DocumentStorageBuilderPersonStub>(_ => personProvider)
            .WithProvider<DocumentStorageBuilderArchiveStub>(_ => archiveProvider);

        using var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDocumentStoreClientFactory>();
        await factory.CreateClient<DocumentStorageBuilderArchiveStub>("default").UpsertAsync(key, new() { Name = "selected" });
        var archiveDescriptor = factory.GetDescriptors()
            .Single(e => e.DocumentType == typeof(DocumentStorageBuilderArchiveStub));

        // Act
        var accessor = factory.Create(archiveDescriptor.ClientId);
        var result = await accessor.GetJsonAsync(key);

        // Assert
        accessor.Descriptor.DocumentType.ShouldBe(typeof(DocumentStorageBuilderArchiveStub));
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain("selected");
    }

    [Fact]
    public void GetDescriptors_WithCustomCapabilities_ShouldExposeCapabilities()
    {
        // Arrange
        var provider = new InMemoryDocumentStoreProvider();
        var services = new ServiceCollection();

        services.AddDocumentStorage()
            .WithProvider<DocumentStorageBuilderPersonStub>(
                _ => provider,
                capabilities: new DocumentStoreProviderCapabilities
                {
                    FullMatch = DocumentQuerySupport.SupportedEfficiently,
                    RowKeyPrefixMatch = DocumentQuerySupport.SupportedServerSide,
                    RowKeySuffixMatch = DocumentQuerySupport.SupportedServerSide
                });

        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var descriptor = serviceProvider.GetRequiredService<IDocumentStoreClientFactory>()
            .GetDescriptors()
            .Single(e => e.DocumentType == typeof(DocumentStorageBuilderPersonStub));

        // Assert
        descriptor.Capabilities.FullMatch.ShouldBe(DocumentQuerySupport.SupportedEfficiently);
        descriptor.Capabilities.RowKeyPrefixMatch.ShouldBe(DocumentQuerySupport.SupportedServerSide);
        descriptor.Capabilities.RowKeySuffixMatch.ShouldBe(DocumentQuerySupport.SupportedServerSide);
    }

    [Fact]
    public void CreateClient_WithTwoNamesForSameType_ResolvesIsolatedKeyedClients()
    {
        var primary = new InMemoryDocumentStoreProvider();
        var archive = new InMemoryDocumentStoreProvider();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDocumentStorage()
            .WithProvider<DocumentStorageBuilderPersonStub>(_ => primary, name: "primary", isDefault: true)
            .WithProvider<DocumentStorageBuilderPersonStub>(_ => archive, name: "archive", isDefault: false);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDocumentStoreClientFactory>();

        ((IDocumentStoreClientIdentity)factory.CreateClient<DocumentStorageBuilderPersonStub>(" PRIMARY ")).ClientName.ShouldBe("primary");
        ((IDocumentStoreClientIdentity)factory.CreateClient<DocumentStorageBuilderPersonStub>("archive")).ClientName.ShouldBe("archive");
        ((IDocumentStoreClientIdentity)scope.ServiceProvider.GetRequiredService<IDocumentStoreClient<DocumentStorageBuilderPersonStub>>()).ClientName.ShouldBe("primary");
        factory.GetDescriptors().Select(x => x.Name).OrderBy(x => x).ShouldBe(["archive", "primary"]);
    }

    [Fact]
    public void WithClient_WithDuplicateDefault_ThrowsDuringRegistration()
    {
        var services = new ServiceCollection();
        var builder = services.AddDocumentStorage()
            .WithProvider<DocumentStorageBuilderPersonStub>(_ => new InMemoryDocumentStoreProvider(), name: "primary");

        Should.Throw<InvalidOperationException>(() => builder.WithProvider<DocumentStorageBuilderPersonStub>(
            _ => new InMemoryDocumentStoreProvider(),
            name: "archive"));
    }

    [Fact]
    public async Task DocumentRetentionBackgroundService_SweepOnce_UsesContainerOwnedKeyedProvider()
    {
        var provider = new InMemoryDocumentStoreProvider();
        var services = new ServiceCollection();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero));
        var applicationStarted = new CancellationToken(canceled: true);
        var applicationLifetime = Substitute.For<IHostApplicationLifetime>();
        applicationLifetime.ApplicationStarted.Returns(applicationStarted);
        services.AddSingleton(applicationLifetime);
        services.AddLogging();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddStoragePermalinks().UseInMemory();
        services.AddDocumentStorage(options => options
                .UseLifetime(ServiceLifetime.Singleton)
                .WithRetention(retention =>
                {
                    retention.BatchSize = 10;
                    retention.MaxBatchesPerStore = 1;
                }))
            .WithProvider<DocumentStorageBuilderPersonStub>(
                _ => provider,
                lifetime: ServiceLifetime.Singleton)
            .WithPermalinks<DocumentStorageBuilderPersonStub>();
        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var client = serviceProvider.GetRequiredService<IDocumentStoreClient<DocumentStorageBuilderPersonStub>>();
        var key = new DocumentKey("expired", "one");
        await client.UpsertAsync(key, new(), new() { Expiration = ExpirationChange.At(timeProvider.GetUtcNow().AddMinutes(-1)) });
        var queue = serviceProvider.GetRequiredService<StoragePermalinkChangeQueue>();
        while (queue.Reader.TryRead(out _)) { }

        var result = await serviceProvider.GetRequiredService<DocumentRetentionBackgroundService>().SweepOnceAsync();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Single().DeletedCount.ShouldBe(1);
        result.Value.Single().DeletedKeys.ShouldBe([key]);
        queue.Reader.TryRead(out var notification).ShouldBeTrue();
        notification.ChangeKind.ShouldBe(StorageResourceChangeKind.Deleted);
        notification.Location.ShouldBe(StorageResourceLocation.ForDocument(
            $"{typeof(DocumentStorageBuilderPersonStub).FullName.ToLowerInvariant()}:default",
            key));
        notification.OccurredAt.ShouldBe(timeProvider.GetUtcNow());
        (await provider.DeleteAsync(DocumentTypeIdentity.For<DocumentStorageBuilderPersonStub>(), key)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task HealthCheck_WithRegisteredClients_ShouldProbeEveryTypedClient()
    {
        // Arrange
        var personProvider = Substitute.For<IDocumentStoreProvider>();
        var archiveProvider = Substitute.For<IDocumentStoreProvider>();
        personProvider.Capabilities.Returns(new DocumentStoreProviderCapabilities());
        archiveProvider.Capabilities.Returns(new DocumentStoreProviderCapabilities());
        var probeKey = new DocumentKey("__bdk/healthcheck", "probe");
        personProvider.ExistsAsync(Arg.Any<DocumentTypeIdentity>(), probeKey, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        archiveProvider.ExistsAsync(Arg.Any<DocumentTypeIdentity>(), probeKey, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDocumentStorage()
            .WithProvider<DocumentStorageBuilderPersonStub>(_ => personProvider)
            .WithProvider<DocumentStorageBuilderArchiveStub>(_ => archiveProvider);

        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var report = await serviceProvider.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        // Assert
        report.Status.ShouldBe(HealthStatus.Healthy);
        report.Entries.Count.ShouldBe(1);
        report.Entries.Keys.Single().ShouldBe("DocumentStorage");
        await personProvider.Received(1).ExistsAsync(Arg.Any<DocumentTypeIdentity>(), probeKey, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await archiveProvider.Received(1).ExistsAsync(Arg.Any<DocumentTypeIdentity>(), probeKey, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HealthCheck_WithFailingClient_ShouldReportFailedClient()
    {
        // Arrange
        var personProvider = Substitute.For<IDocumentStoreProvider>();
        var archiveProvider = Substitute.For<IDocumentStoreProvider>();
        personProvider.Capabilities.Returns(new DocumentStoreProviderCapabilities());
        archiveProvider.Capabilities.Returns(new DocumentStoreProviderCapabilities());
        var probeKey = new DocumentKey("__bdk/healthcheck", "probe");
        personProvider.ExistsAsync(Arg.Any<DocumentTypeIdentity>(), probeKey, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        archiveProvider.ExistsAsync(Arg.Any<DocumentTypeIdentity>(), probeKey, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Failure(new DocumentStoreInvalidQueryError("backend unavailable")));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDocumentStorage()
            .WithProvider<DocumentStorageBuilderPersonStub>(_ => personProvider)
            .WithProvider<DocumentStorageBuilderArchiveStub>(_ => archiveProvider);

        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var report = await serviceProvider.GetRequiredService<HealthCheckService>().CheckHealthAsync();
        var entry = report.Entries["DocumentStorage"];

        // Assert
        report.Status.ShouldBe(HealthStatus.Unhealthy);
        entry.Description.ShouldContain(nameof(DocumentStorageBuilderArchiveStub));
        entry.Data["failedClientCount"].ShouldBe(1);
        entry.Data["clientErrors"].ShouldBeAssignableTo<string[]>()
            .Single()
            .ShouldContain("backend unavailable");
    }

    public sealed class DocumentStorageBuilderPersonStub
    {
        public string FirstName { get; set; }
    }

    public sealed class DocumentStorageBuilderArchiveStub
    {
        public string Name { get; set; }
    }

    private sealed class ProbeBehavior<T>(IDocumentStoreClient<T> inner) : DocumentStoreClientBehaviorBase<T>(inner)
        where T : class, new()
    {
        public int Calls { get; private set; }
        public override Task<Result<DocumentEntry<T>>> GetAsync(DocumentKey key, CancellationToken cancellationToken = default)
        {
            this.Calls++;
            return base.GetAsync(key, cancellationToken);
        }
    }
}
