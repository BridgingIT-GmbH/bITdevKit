// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using BridgingIT.DevKit.Application.Storage;
using Microsoft.Extensions.DependencyInjection;

public sealed partial class StorageProfilingBehaviorTests
{
    /// <summary>Every file operation forwards original inputs and captures a bounded operation without enumerating lazy paths or touching content.</summary>
    /// <example><code>await suite.FileProvider_AllOperations_ProfilesAndForwardsWithoutInspection();</code></example>
    [Fact]
    public async Task FileProvider_AllOperations_ProfilesAndForwardsWithoutInspection()
    {
        using var services = await Services();
        var inner = Substitute.For<IFileStorageProvider>(); inner.LocationName.Returns("documents");
        var sut = new ProfilingFileStorageBehavior(inner, services.GetRequiredService<IOperationProfiler>());
        using var content = new UninspectableStream(); using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var progress = new Progress<FileProgress>();
        var pairs = Unenumerable<(string SourcePath, string DestinationPath)>(); var paths = Unenumerable<string>();
        var metadata = new FileMetadata(); Func<FileMetadata, FileMetadata> update = _ => throw new InvalidOperationException("Only the provider may call this.");

        await sut.FileExistsAsync("private-path", progress, token);
        await sut.ReadFileAsync("private-path", progress, token);
        await sut.RenameDirectoryAsync("private-path", "destination", token);
        await sut.WriteFileAsync("private-path", content, progress, token);
        await sut.OpenWriteFileAsync("private-path", true, progress, token);
        await sut.DeleteFileAsync("private-path", progress, token);
        await sut.GetChecksumAsync("private-path", token);
        await sut.GetFileMetadataAsync("private-path", token);
        await sut.SetFileMetadataAsync("private-path", metadata, token);
        await sut.UpdateFileMetadataAsync("private-path", update, token);
        await sut.ListFilesAsync("private-path", "private-prefix", true, "private-token", token);
        await sut.CopyFileAsync("private-path", "destination", progress, token);
        await sut.RenameFileAsync("private-path", "destination", progress, token);
        await sut.MoveFileAsync("private-path", "destination", progress, token);
        await sut.CopyFilesAsync(pairs, progress, token);
        await sut.MoveFilesAsync(pairs, progress, token);
        await sut.DeleteFilesAsync(paths, progress, token);
        await sut.DirectoryExistsAsync("private-path", token);
        await sut.CreateDirectoryAsync("private-path", token);
        await sut.DeleteDirectoryAsync("private-path", true, token);
        await sut.ListDirectoriesAsync("private-path", "private-prefix", true, token);
        await sut.CheckHealthAsync(token);

        var records = await Stored(services);
        records.Count.ShouldBe(22); records.Select(record => record.Key).Distinct().Count().ShouldBe(22);
        records.ShouldAllBe(record => record.Kind == "FileStorage" && record.Dimensions.Count == 2);
        foreach (var record in records) { AssertRedacted(record); }

        var calls = inner.ReceivedCalls().Where(call => call.GetMethodInfo().Name.EndsWith("Async", StringComparison.Ordinal)).ToArray();
        calls.Length.ShouldBe(22); calls.ShouldAllBe(call => call.GetArguments().OfType<CancellationToken>().Single() == token);
        await inner.Received(1).WriteFileAsync("private-path", content, progress, token);
        await inner.Received(1).CopyFilesAsync(pairs, progress, token);
        await inner.Received(1).MoveFilesAsync(pairs, progress, token);
        await inner.Received(1).DeleteFilesAsync(paths, progress, token);
        await inner.Received(1).UpdateFileMetadataAsync("private-path", update, token);
        content.Disposed.ShouldBeFalse();
    }

    /// <summary>All typed document methods retain client/type dimensions and preserve query, key and cancellation forwarding.</summary>
    /// <example><code>await suite.DocumentClient_AllOperations_ProfilesWithClientIdentity();</code></example>
    [Fact]
    public async Task DocumentClient_AllOperations_ProfilesWithClientIdentity()
    {
        using var services = await Services();
        var inner = Substitute.For<IDocumentStoreClient<StoredDocument>, IDocumentStoreClientIdentity>();
        ((IDocumentStoreClientIdentity)inner).ClientName.Returns("archive");
        var sut = new ProfilingDocumentStoreClientBehavior<StoredDocument>(inner, services.GetRequiredService<IOperationProfiler>());
        using var cancellation = new CancellationTokenSource(); var token = cancellation.Token;
        var key = new DocumentKey("private-partition", "private-key");
        var query = new DocumentQuery { DocumentKey = key, ContinuationToken = "private-token" };
        var count = new DocumentCountQuery { DocumentKey = key };
        var value = new StoredDocument(); var options = new DocumentWriteOptions();
        var update = new DocumentPropertiesUpdate(key); var deleteOptions = new DocumentDeleteOptions();
        var writes = new[] { new DocumentWrite<StoredDocument>(key, value) }; var deletes = new[] { new DocumentDelete(key) };

        await sut.GetAsync(key, token); await sut.FindPageAsync(query, token); await sut.ListPageAsync(query, token);
        await sut.CountAsync(count, token); await sut.ExistsAsync(key, token); await sut.UpsertAsync(key, value, options, token);
        await sut.UpsertManyAsync(writes, token); await sut.UpdatePropertiesAsync(update, token); await sut.DeleteAsync(key, deleteOptions, token); await sut.DeleteManyAsync(deletes, token);

        var records = await Stored(services);
        records.Count.ShouldBe(10); records.Select(record => record.Key).Distinct().Count().ShouldBe(10);
        records.ShouldAllBe(record => record.Kind == "DocumentStorage" && record.Dimensions.Count == 3);
        foreach (var record in records)
        {
            record.Dimensions.ShouldContain(dimension => dimension.Key == "documentstorage.client" && dimension.Value.Scalar == "archive");
            AssertRedacted(record);
        }

        var calls = inner.ReceivedCalls().Where(call => call.GetMethodInfo().Name.EndsWith("Async", StringComparison.Ordinal)).ToArray();
        calls.Length.ShouldBe(10); calls.ShouldAllBe(call => call.GetArguments().OfType<CancellationToken>().Single() == token);
        await inner.Received(1).FindPageAsync(query, token); await inner.Received(1).UpsertAsync(key, value, options, token);
        await inner.Received(1).UpsertManyAsync(writes, token); await inner.Received(1).DeleteManyAsync(deletes, token);
    }

    /// <summary>Every blob method discovers its container from keys, uploads, metadata updates or queries, without retaining names and prefixes.</summary>
    /// <example><code>await suite.BlobClient_AllOperations_ProfilesContainerAndPreservesInputs();</code></example>
    [Fact]
    public async Task BlobClient_AllOperations_ProfilesContainerAndPreservesInputs()
    {
        using var services = await Services();
        var inner = Substitute.For<IBlobStoreClient>();
        var sut = new ProfilingBlobStoreClientBehavior(inner, services.GetRequiredService<IOperationProfiler>(), "archive");
        using var content = new UninspectableStream(); using var cancellation = new CancellationTokenSource(); var token = cancellation.Token;
        var key = new BlobKey("reports", "private-key"); var upload = new BlobUpload { Key = key, Content = content };
        var query = new BlobQuery { Container = "reports", Prefix = "private-path", ContinuationToken = "private-token" };
        var update = new BlobPropertiesUpdate { Key = key }; var options = new BlobDeleteOptions();

        await sut.UploadAsync(upload, token); await sut.DownloadAsync(key, token); await sut.GetPropertiesAsync(key, token);
        await sut.UpdatePropertiesAsync(update, token); await sut.ExistsAsync(key, token); await sut.ListPageAsync(query, token); await sut.DeleteAsync(key, options, token);

        var records = await Stored(services);
        records.Count.ShouldBe(7); records.Select(record => record.Key).Distinct().Count().ShouldBe(7);
        records.ShouldAllBe(record => record.Kind == "BlobStorage" && record.Dimensions.Count == 3);
        foreach (var record in records)
        {
            record.Dimensions.ShouldContain(dimension => dimension.Key == "blobstorage.container" && dimension.Value.Scalar == "reports");
            AssertRedacted(record);
        }

        var calls = inner.ReceivedCalls().Where(call => call.GetMethodInfo().Name.EndsWith("Async", StringComparison.Ordinal)).ToArray();
        calls.Length.ShouldBe(7); calls.ShouldAllBe(call => call.GetArguments().OfType<CancellationToken>().Single() == token);
        await inner.Received(1).UploadAsync(upload, token); await inner.Received(1).ListPageAsync(query, token); content.Disposed.ShouldBeFalse();
    }

    /// <summary>All fluent registration paths remain usable with or without Profiling registration, including legacy direct document builders.</summary>
    /// <example><code>await suite.Registration_AllBuilders_UsesOptionalProfiler(false);</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registration_AllBuilders_UsesOptionalProfiler(bool capture)
    {
        var collection = new ServiceCollection(); collection.AddLogging();
        if (capture) { collection.AddProfiling(options => options.Enabled()).WithOperationProfiling(); }

        collection.AddFileStorage(factory => factory.RegisterProvider("files", builder => builder.UseInMemory("documents").WithProfiling()));
        collection.AddDocumentStorage().WithProfilingBehavior<StoredDocument>()
            .RegisterProvider<StoredDocument>(_ => new InMemoryDocumentStoreProvider(), name: "archive");
        collection.AddBlobStorage().WithProfilingBehavior().WithInMemoryClient("reports");
        await using var services = collection.BuildServiceProvider();
        if (capture) { await services.GetRequiredService<OperationProfilingWriterService>().TickAsync(); }

        var file = services.GetRequiredService<IFileStorageProviderFactory>().CreateProvider("files");
        var document = services.GetRequiredService<IDocumentStoreClientFactory>().CreateClient<StoredDocument>("archive");
        var blob = services.GetRequiredService<IBlobStoreClientFactory>().CreateClient("reports");
        await file.FileExistsAsync("private-path"); await document.ExistsAsync(new("private-partition", "private-key")); await blob.ExistsAsync(new("reports", "private-key"));

        var legacyServices = new ServiceCollection();
        legacyServices.AddSingleton<IDocumentStoreClient<StoredDocument>>(new DocumentStoreClient<StoredDocument>(new InMemoryDocumentStoreProvider(), clientName: "legacy"));
        new DocumentStoreBuilderContext<StoredDocument>(legacyServices).WithProfilingBehavior();
        using var legacy = legacyServices.BuildServiceProvider();
        legacy.GetRequiredService<IDocumentStoreClient<StoredDocument>>().ShouldBeOfType<ProfilingDocumentStoreClientBehavior<StoredDocument>>();
        (await legacy.GetRequiredService<IDocumentStoreClient<StoredDocument>>().ExistsAsync(new("p", "r"))).IsSuccess.ShouldBeTrue();
        if (capture)
        {
            var records = await Stored(services); records.Count.ShouldBe(3);
            records.Single(record => record.Kind == "FileStorage").Dimensions.ShouldContain(dimension => dimension.Value.Scalar == "documents");
            records.Single(record => record.Kind == "DocumentStorage").Dimensions.ShouldContain(dimension => dimension.Value.Scalar == "archive");
            records.Single(record => record.Kind == "BlobStorage").Dimensions.ShouldContain(dimension => dimension.Key == "blobstorage.store" && dimension.Value.Scalar == "reports");
        }
        else { services.GetService<IOperationProfiler>().ShouldBeNull(); }
    }

    private static IEnumerable<T> Unenumerable<T>() => new UnenumerableValues<T>();

    private sealed class UnenumerableValues<T> : IEnumerable<T>
    {
        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException("Only the provider may enumerate input paths.");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
