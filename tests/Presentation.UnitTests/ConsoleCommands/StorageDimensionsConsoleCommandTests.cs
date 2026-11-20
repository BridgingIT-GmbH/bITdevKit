// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.UnitTests.ConsoleCommands;

using Application.Storage;
using Presentation;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using System.Text.Json;

public sealed class StorageDimensionsConsoleCommandTests
{
    [Fact]
    public async Task BlobList_ForwardsTypedFilterAndContinuation_RejectsFilterForDeletion()
    {
        var client = Substitute.For<IBlobStoreClient>();
        BlobQuery received = null;
        client.ListPageAsync(Arg.Any<BlobQuery>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            received = call.Arg<BlobQuery>();
            return Result<BlobPage>.Success(new() { Items = [] });
        });
        var factory = Substitute.For<IBlobStoreClientFactory>();
        factory.GetRegistrations().Returns([new() { Name = "reports" }]);
        factory.CreateClient("reports").Returns(client);
        using var services = new ServiceCollection().AddSingleton(factory).BuildServiceProvider();
        using var output = new StringWriter();
        var console = AnsiConsole.Create(new() { Out = new AnsiConsoleOutput(output), Ansi = AnsiSupport.No });
        var filter = BlobDimensionFilter.Where(BlobDimensionPredicate.Create("amount", BlobDimensionOperator.Equal, 1.0m).Value);
        var sut = new StorageBlobsConsoleCommand { Operation = "list", Client = "reports", Container = "archive", DimensionFilterJson = JsonSerializer.Serialize(filter), ContinuationToken = "next", Take = 2 };
        await sut.ExecuteAsync(console, services);
        received.ShouldNotBeNull(output.ToString());
        received.DimensionFilter.ToCanonicalString().ShouldBe(filter.ToCanonicalString());
        received.ContinuationToken.ShouldBe("next");
        sut.Operation = "delete-all";
        sut.Yes = true;
        await sut.ExecuteAsync(console, services);
        await client.DidNotReceiveWithAnyArgs().DeleteAsync(default, default, default);
        output.ToString().ShouldContain("list operations");
    }

    [Fact]
    public async Task DocumentList_PositiveDimensionAnchorNeedsNoPartition_AndPreservesTypedOperand()
    {
        var accessor = Substitute.For<IDocumentStoreClientAccessor>();
        DocumentQuery received = null;
        accessor.ListPageAsync(Arg.Any<DocumentQuery>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            received = call.Arg<DocumentQuery>();
            return Result<DocumentKeyPage>.Success(new() { Items = [] });
        });
        var factory = Substitute.For<IDocumentStoreClientFactory>();
        factory.GetDescriptors().Returns([new("docs", typeof(Invoice), "Invoice", "Test")]);
        factory.Create("docs").Returns(accessor);
        using var services = new ServiceCollection().AddSingleton(factory).BuildServiceProvider();
        using var output = new StringWriter();
        var console = AnsiConsole.Create(new() { Out = new AnsiConsoleOutput(output), Ansi = AnsiSupport.No });
        var filter = DocumentDimensionFilter.Where(DocumentDimensionPredicate.Create("year", DocumentDimensionOperator.Equal, 2026).Value);
        var sut = new StorageDocumentsConsoleCommand { Operation = "list", ClientId = "docs", DimensionFilterJson = JsonSerializer.Serialize(filter), ContinuationToken = "next", Take = 2 };
        await sut.ExecuteAsync(console, services);
        received.ShouldNotBeNull(output.ToString());
        received.DimensionFilter.ToCanonicalString().ShouldBe(filter.ToCanonicalString());
        received.DocumentKey.ShouldBeNull();
        received.AllowFullScan.ShouldBeFalse();
        received.ContinuationToken.ShouldBe("next");
    }

    public sealed class Invoice { }
}
