// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

[UnitTest("Application")]
public class DocumentStorageBuilderContextTests
{
    [Fact]
    public void GetRequiredService_WithBehaviorBeforeClient_ShouldResolveDecoratedClient()
    {
        // Arrange
        var inner = Substitute.For<IDocumentStoreClient<DocumentStorageBuilderPersonStub>>();
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDocumentStorage(o => o.Enabled(true))
            .WithBehavior<LoggingDocumentStoreClientBehavior<DocumentStorageBuilderPersonStub>>()
            .WithClient<DocumentStorageBuilderPersonStub>(_ => inner);

        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var result = serviceProvider.GetRequiredService<IDocumentStoreClient<DocumentStorageBuilderPersonStub>>();

        // Assert
        result.ShouldBeOfType<LoggingDocumentStoreClientBehavior<DocumentStorageBuilderPersonStub>>();
    }

    [Fact]
    public void GetRequiredService_WithBehaviorAfterClient_ShouldResolveDecoratedClient()
    {
        // Arrange
        var inner = Substitute.For<IDocumentStoreClient<DocumentStorageBuilderPersonStub>>();
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDocumentStorage()
            .WithClient<DocumentStorageBuilderPersonStub>(_ => inner)
            .WithBehavior<LoggingDocumentStoreClientBehavior<DocumentStorageBuilderPersonStub>>();

        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var result = serviceProvider.GetRequiredService<IDocumentStoreClient<DocumentStorageBuilderPersonStub>>();

        // Assert
        result.ShouldBeOfType<LoggingDocumentStoreClientBehavior<DocumentStorageBuilderPersonStub>>();
    }

    [Fact]
    public async Task Create_WithMultipleClients_ShouldResolveSelectedTypedClient()
    {
        // Arrange
        var personClient = Substitute.For<IDocumentStoreClient<DocumentStorageBuilderPersonStub>>();
        var archiveClient = Substitute.For<IDocumentStoreClient<DocumentStorageBuilderArchiveStub>>();
        var key = new DocumentKey("archive", "row-1");
        archiveClient.GetResultAsync(key, Arg.Any<CancellationToken>())
            .Returns(Result<DocumentStorageBuilderArchiveStub>.Success(new DocumentStorageBuilderArchiveStub { Name = "selected" }));

        var services = new ServiceCollection();
        services.AddDocumentStorage()
            .WithClient<DocumentStorageBuilderPersonStub>(_ => personClient)
            .WithClient<DocumentStorageBuilderArchiveStub>(_ => archiveClient);

        using var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDocumentStoreClientFactory>();
        var archiveDescriptor = factory.GetDescriptors()
            .Single(e => e.DocumentType == typeof(DocumentStorageBuilderArchiveStub));

        // Act
        var accessor = factory.Create(archiveDescriptor.ClientId);
        var result = await accessor.GetJsonResultAsync(key);

        // Assert
        accessor.Descriptor.DocumentType.ShouldBe(typeof(DocumentStorageBuilderArchiveStub));
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain("selected");
        await archiveClient.Received(1).GetResultAsync(key, Arg.Any<CancellationToken>());
        await personClient.DidNotReceive().GetResultAsync(Arg.Any<DocumentKey>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void GetDescriptors_WithCustomCapabilities_ShouldExposeCapabilities()
    {
        // Arrange
        var client = Substitute.For<IDocumentStoreClient<DocumentStorageBuilderPersonStub>>();
        var services = new ServiceCollection();

        services.AddDocumentStorage()
            .WithClient<DocumentStorageBuilderPersonStub>(
                _ => client,
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
    public async Task HealthCheck_WithRegisteredClients_ShouldProbeEveryTypedClient()
    {
        // Arrange
        var personClient = Substitute.For<IDocumentStoreClient<DocumentStorageBuilderPersonStub>>();
        var archiveClient = Substitute.For<IDocumentStoreClient<DocumentStorageBuilderArchiveStub>>();
        var probeKey = new DocumentKey("__bdk/healthcheck", "probe");
        personClient.ExistsResultAsync(probeKey, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        archiveClient.ExistsResultAsync(probeKey, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDocumentStorage()
            .WithClient<DocumentStorageBuilderPersonStub>(_ => personClient)
            .WithClient<DocumentStorageBuilderArchiveStub>(_ => archiveClient);

        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var report = await serviceProvider.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        // Assert
        report.Status.ShouldBe(HealthStatus.Healthy);
        report.Entries.Count.ShouldBe(1);
        report.Entries.Keys.Single().ShouldBe("DocumentStorage");
        await personClient.Received(1).ExistsResultAsync(probeKey, Arg.Any<CancellationToken>());
        await archiveClient.Received(1).ExistsResultAsync(probeKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HealthCheck_WithFailingClient_ShouldReportFailedClient()
    {
        // Arrange
        var personClient = Substitute.For<IDocumentStoreClient<DocumentStorageBuilderPersonStub>>();
        var archiveClient = Substitute.For<IDocumentStoreClient<DocumentStorageBuilderArchiveStub>>();
        var probeKey = new DocumentKey("__bdk/healthcheck", "probe");
        personClient.ExistsResultAsync(probeKey, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        archiveClient.ExistsResultAsync(probeKey, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Failure(new DocumentStoreInvalidQueryError("backend unavailable")));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDocumentStorage()
            .WithClient<DocumentStorageBuilderPersonStub>(_ => personClient)
            .WithClient<DocumentStorageBuilderArchiveStub>(_ => archiveClient);

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
}
