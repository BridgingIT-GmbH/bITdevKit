// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Mcp;

using Application.Storage;
using System.Text.Json;

public sealed class StorageDimensionsMcpTests
{
    [Fact]
    public async Task BlobInspection_CountsNamesOnlyOnExplicitRequest_AndReturnsCountersAndCapabilities()
    {
        var diagnostics = Substitute.For<IBlobStorageDiagnosticsService>();
        var snapshot = new BlobStorageDiagnosticsSnapshot { Clients = [new() { Name = "reports", IsHealthy = true,
            Capabilities = new() { Dimensions = new() { SupportsDimensions = true, SupportsQueries = true } },
            DimensionDiagnostics = new() { PositiveCacheEntries = 8, RegistrationConflicts = 2, DictionaryEntries = 500 } }] };
        diagnostics.GetSnapshotAsync(Arg.Any<CancellationToken>()).Returns(Result<BlobStorageDiagnosticsSnapshot>.Success(snapshot));
        diagnostics.GetSnapshotAsync(true, Arg.Any<CancellationToken>()).Returns(Result<BlobStorageDiagnosticsSnapshot>.Success(snapshot));
        var sut = new BlobStorageMcpHandler(diagnostics);
        (await sut.HandleAsync(new("blobs.clients", McpToolset.Diagnostics, JsonSerializer.SerializeToElement(new { })), CancellationToken.None)).Available.ShouldBeTrue();
        await diagnostics.Received(1).GetSnapshotAsync(Arg.Any<CancellationToken>());
        await diagnostics.DidNotReceive().GetSnapshotAsync(true, Arg.Any<CancellationToken>());
        var response = await sut.HandleAsync(new("blobs.clients", McpToolset.Diagnostics, JsonSerializer.SerializeToElement(new { includeDimensionNameCounts = true })), CancellationToken.None);
        response.Available.ShouldBeTrue();
        await diagnostics.Received(1).GetSnapshotAsync(true, Arg.Any<CancellationToken>());
        var json = JsonSerializer.Serialize(response.Data);
        json.ShouldContain("DictionaryEntries");
        json.ShouldContain("SupportedOperators");
        json.ShouldNotContain("ConnectionString");
    }

    [Fact]
    public async Task DocumentInspection_CountsNamesOnlyOnExplicitRequest_AndReturnsCountersAndCapabilities()
    {
        var diagnostics = Substitute.For<IDocumentStorageDiagnosticsService>();
        var snapshot = new DocumentStorageDiagnosticsSnapshot { Clients = [new() { Name = "default", IsHealthy = true,
            Capabilities = new() { Dimensions = new() { SupportsDimensions = true, SupportsQueries = true } },
            DimensionDiagnostics = new() { PositiveCacheEntries = 8, RegistrationConflicts = 2, DictionaryEntries = 500 } }] };
        diagnostics.CaptureAsync(Arg.Any<CancellationToken>()).Returns(Result<DocumentStorageDiagnosticsSnapshot>.Success(snapshot));
        diagnostics.CaptureAsync(true, Arg.Any<CancellationToken>()).Returns(Result<DocumentStorageDiagnosticsSnapshot>.Success(snapshot));
        var sut = new DocumentStorageMcpHandler(diagnostics);
        (await sut.HandleAsync(new("documents.clients", McpToolset.Diagnostics, JsonSerializer.SerializeToElement(new { })), CancellationToken.None)).Available.ShouldBeTrue();
        await diagnostics.Received(1).CaptureAsync(Arg.Any<CancellationToken>());
        await diagnostics.DidNotReceive().CaptureAsync(true, Arg.Any<CancellationToken>());
        var response = await sut.HandleAsync(new("documents.clients", McpToolset.Diagnostics, JsonSerializer.SerializeToElement(new { includeDimensionNameCounts = true })), CancellationToken.None);
        response.Available.ShouldBeTrue();
        await diagnostics.Received(1).CaptureAsync(true, Arg.Any<CancellationToken>());
        var json = JsonSerializer.Serialize(response.Data);
        json.ShouldContain("DictionaryEntries");
        json.ShouldContain("SupportedOperators");
        json.ShouldNotContain("ConnectionString");
    }
}
