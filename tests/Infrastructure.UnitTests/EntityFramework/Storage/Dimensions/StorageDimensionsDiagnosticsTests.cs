// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Storage;

using Application.Storage;
using Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;

public sealed partial class StorageDimensionsMutationTests
{
    [Fact]
    public async Task DimensionCounters_DefaultInspectionDoesNoIo_ExplicitInspectionCountsRetainedNames()
    {
        var commands = new DimensionInspectionCommands();
        await using var services = new ServiceCollection().AddDbContext<DimensionsContext>(options => options.UseSqlite(this.connection).AddInterceptors(commands)).BuildServiceProvider();
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        var blob = new EntityFrameworkBlobStoreProvider<DimensionsContext>(scopes);
        var document = new EntityFrameworkDocumentStoreProvider<DimensionsContext>(scopes);
        var blobClient = new BlobStoreClient("ef", blob);
        var docClient = new DocumentStoreClient<Invoice>(document);
        using var content = new MemoryStream([1]);
        var blobWrite = await blobClient.UploadAsync(new() { Key = new("archive", "owner"), Content = content,
            Dimensions = BlobDimensions.Create(new Dictionary<string, object> { ["year"] = 2026, ["flag"] = true }).Value });
        blobWrite.IsSuccess.ShouldBeTrue();
        (await docClient.UpsertAsync(new("archive", "owner"), new() { Amount = 1 }, new() { Dimensions = DocumentDimensions.Create(new Dictionary<string, object> { ["year"] = 2026, ["flag"] = true }).Value })).IsSuccess.ShouldBeTrue();
        commands.Count = 0;
        var blobCounters = await blob.GetDimensionDiagnosticsAsync();
        var docCounters = await document.GetDimensionDiagnosticsAsync(DocumentTypeIdentity.For<Invoice>());
        commands.Count.ShouldBe(0);
        blobCounters.Value.PositiveCacheEntries.ShouldBe(2);
        blobCounters.Value.AcknowledgedNameRegistrations.ShouldBe(2);
        blobCounters.Value.RegistrationOperationsInFlight.ShouldBe(0);
        blobCounters.Value.DictionaryEntries.ShouldBeNull();
        docCounters.Value.PositiveCacheEntries.ShouldBe(2);
        docCounters.Value.AcknowledgedNameRegistrations.ShouldBe(2);
        docCounters.Value.DictionaryEntries.ShouldBeNull();
        (await blobClient.UpdateDimensionsAsync(new() { Key = new("archive", "owner"), Dimensions = BlobDimensions.Empty })).IsSuccess.ShouldBeTrue();
        (await docClient.UpdateDimensionsAsync(new() { Key = new("archive", "owner"), Dimensions = DocumentDimensions.Empty })).IsSuccess.ShouldBeTrue();
        var blobInspection = await blob.GetDimensionDiagnosticsAsync(true);
        var docInspection = await document.GetDimensionDiagnosticsAsync(DocumentTypeIdentity.For<Invoice>(), true);
        blobInspection.Value.DictionaryEntries.ShouldBe(2);
        docInspection.Value.DictionaryEntries.ShouldBe(2);
        blob.ClearDimensionNameCache();
        document.ClearDimensionNameCache();
        (await blob.GetDimensionDiagnosticsAsync()).Value.PositiveCacheEntries.ShouldBe(0);
        (await document.GetDimensionDiagnosticsAsync(DocumentTypeIdentity.For<Invoice>())).Value.PositiveCacheEntries.ShouldBe(0);
    }

    private sealed class DimensionInspectionCommands : DbCommandInterceptor
    {
        public int Count { get; set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            this.Count++;
            return ValueTask.FromResult(result);
        }
    }
}
