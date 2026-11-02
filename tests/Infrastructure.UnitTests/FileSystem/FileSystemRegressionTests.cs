namespace BridgingIT.DevKit.Infrastructure.UnitTests.FileSystem;

using Application.Storage;
using Infrastructure.FileSystem;
using Microsoft.Extensions.DependencyInjection;

public sealed class FileSystemRegressionTests
{
    [LocalFileSystemTheory]
    [InlineData("download")]
    [InlineData("properties")]
    [InlineData("delete")]
    public async Task BlobOperation_ConcurrentFirstCreation_DoesNotAccessRecordWithoutAuthority(string operation)
    {
        using var root = new OwnedFileSystemRoot();
        var reader = new FileSystemBlobStoreProvider(root.Options);
        var writer = new FileSystemBlobStoreProvider(root.Options);
        var key = new BlobKey("race", "first");
        var created = false;
        reader.Store.Fault = point =>
        {
            if (point != "authority-directory-missing")
            {
                return;
            }

            reader.Store.Fault = null;
            using var source = new MemoryStream([1, 2, 3]);
            writer.UploadAsync(new() { Key = key, Content = source }).GetAwaiter().GetResult().IsSuccess.ShouldBeTrue();
            created = true;
        };
        switch (operation)
        {
            case "download":
                (await reader.DownloadAsync(key)).HasError<BlobStoreNotFoundError>().ShouldBeTrue();
                break;
            case "properties":
                (await reader.UpdatePropertiesAsync(new() { Key = key })).HasError<BlobStoreNotFoundError>().ShouldBeTrue();
                break;
            case "delete":
                (await reader.DeleteAsync(key)).IsSuccess.ShouldBeTrue();
                break;
        }

        created.ShouldBeTrue();
        var surviving = await writer.DownloadAsync(key);
        surviving.IsSuccess.ShouldBeTrue();
        await using var stream = surviving.Value.Content;
        var bytes = new byte[3];
        await stream.ReadExactlyAsync(bytes);
        bytes.ShouldBe(new byte[] { 1, 2, 3 });
    }

    [LocalFileSystemTheory]
    [InlineData("get")]
    [InlineData("properties")]
    [InlineData("delete")]
    public async Task DocumentOperation_ConcurrentFirstCreation_DoesNotAccessRecordWithoutAuthority(string operation)
    {
        using var root = new OwnedFileSystemRoot();
        var reader = new FileSystemDocumentStoreProvider(root.Options);
        var writer = new FileSystemDocumentStoreProvider(root.Options);
        var type = DocumentTypeIdentity.For<RegressionDocument>();
        var key = new DocumentKey("race", "first");
        var created = false;
        reader.Store.Fault = point =>
        {
            if (point != "authority-directory-missing")
            {
                return;
            }

            reader.Store.Fault = null;
            writer.UpsertAsync(type, new() { Key = key, Content = [1, 2, 3] }).GetAwaiter().GetResult().IsSuccess.ShouldBeTrue();
            created = true;
        };
        switch (operation)
        {
            case "get":
                (await reader.GetAsync(type, key, DateTimeOffset.UtcNow)).HasError<DocumentStoreNotFoundError>().ShouldBeTrue();
                break;
            case "properties":
                (await reader.UpdatePropertiesAsync(type, new(key), null, true)).HasError<DocumentStoreNotFoundError>().ShouldBeTrue();
                break;
            case "delete":
                (await reader.DeleteAsync(type, key)).IsSuccess.ShouldBeTrue();
                break;
        }

        created.ShouldBeTrue();
        var surviving = await writer.GetAsync(type, key, DateTimeOffset.UtcNow);
        surviving.IsSuccess.ShouldBeTrue();
        surviving.Value.Content.ShouldBe(new byte[] { 1, 2, 3 });
    }

    [LocalFileSystemTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegisteredDocumentClient_CustomDefaultTake_PreservesPageSizeAndContinuation(bool find)
    {
        using var root = new OwnedFileSystemRoot();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostApplicationLifetime>(Substitute.For<Microsoft.Extensions.Hosting.IHostApplicationLifetime>());
        services.AddDocumentStorage().WithFileSystemClient<RegressionDocument>(root.Path, documentStoreOptions: new()
        {
            DefaultTake = 2,
            MaxTake = 2,
            AllowFullScans = true,
            RejectClientSideFilteredQueries = false
        }, name: "paged");
        await using var container = services.BuildServiceProvider();
        var client = container.GetRequiredService<IDocumentStoreClientFactory>().CreateClient<RegressionDocument>("paged");

        foreach (var key in new[]
        {
            "a",
            "b",
            "c"
        }
        )
        {
            (await client.UpsertAsync(new("partition", key), new RegressionDocument { Name = key })).IsSuccess.ShouldBeTrue();
        }

        var query = new DocumentQuery
        {
            AllowFullScan = true
        };

        if (find)
        {
            var first = await client.FindPageAsync(query);
            first.IsSuccess.ShouldBeTrue();
            first.Value.Items.Select(item => item.Key.RowKey).ShouldBe(new[] { "a", "b" });
            first.Value.ContinuationToken.ShouldNotBeNullOrWhiteSpace();
            var second = await client.FindPageAsync(new() { AllowFullScan = true, ContinuationToken = first.Value.ContinuationToken });
            second.IsSuccess.ShouldBeTrue();
            second.Value.Items.Select(item => item.Key.RowKey).ShouldBe(new[] { "c" });
            second.Value.ContinuationToken.ShouldBeNull();
        }
        else
        {
            var first = await client.ListPageAsync(query);
            first.IsSuccess.ShouldBeTrue();
            first.Value.Items.Select(key => key.RowKey).ShouldBe(new[] { "a", "b" });
            first.Value.ContinuationToken.ShouldNotBeNullOrWhiteSpace();
            var second = await client.ListPageAsync(new() { AllowFullScan = true, ContinuationToken = first.Value.ContinuationToken });
            second.IsSuccess.ShouldBeTrue();
            second.Value.Items.Select(key => key.RowKey).ShouldBe(new[] { "c" });
            second.Value.ContinuationToken.ShouldBeNull();
        }
    }

    public sealed class RegressionDocument
    {
        public string Name { get; set; }
    }
}