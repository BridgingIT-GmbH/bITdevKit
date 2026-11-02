namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System.Diagnostics.Metrics;
using System.Text.Json;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation.Web.Storage;
using Microsoft.Extensions.DependencyInjection;

[Collection(ProcessWideStateTestCollection.Name)]
public sealed class StorageLoadTestTests
{
    [Theory]
    [InlineData("blobs", 0)]
    [InlineData("blobs", 50)]
    [InlineData("blobs", 100)]
    [InlineData("documents", 0)]
    [InlineData("documents", 50)]
    [InlineData("documents", 100)]
    [InlineData("files", 0)]
    [InlineData("files", 50)]
    [InlineData("files", 100)]
    public async Task ExecuteAsync_ReadWriteMix_EmitsMetricsAndRemovesOnlyTemporaryItems(string feature, int readPercent)
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        using var sut = new StorageLoadTest(feature, fixture.Services.GetRequiredService<IServiceScopeFactory>());
        long observedBytes = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, owner) =>
        {
            if (instrument.Meter.Name == BridgingIT.DevKit.Common.Metrics.MeterName && instrument.Name == "storage_load_bytes")
            {
                owner.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "feature" && Equals(tag.Value, feature))
                {
                    Interlocked.Add(ref observedBytes, value);
                }
            }
        });
        listener.Start();
        var result = JsonSerializer.SerializeToElement(await sut.ExecuteAsync(fixture.Request(feature, readPercent), default));
        result.GetProperty("Phase").GetString().ShouldBe("Completed");
        result.GetProperty("cleanupPending").GetInt32().ShouldBe(0);
        var reads = result.GetProperty("readBytes").GetInt64();
        var writes = result.GetProperty("writeBytes").GetInt64();
        (reads + writes).ShouldBeGreaterThan(0);
        observedBytes.ShouldBe(reads + writes);

        if (readPercent == 0)
        {
            reads.ShouldBe(0);
            result.GetProperty("writeMiBPerSecond").GetDouble().ShouldBeGreaterThan(0);
        }
        else if (readPercent == 100)
        {
            writes.ShouldBe(0);
            result.GetProperty("readMiBPerSecond").GetDouble().ShouldBeGreaterThan(0);
        }
        else
        {
            reads.ShouldBeGreaterThan(0);
            writes.ShouldBeGreaterThan(0);
        }

        await fixture.AssertCleanAsync();
    }

    [Theory]
    [InlineData("blobs")]
    [InlineData("documents")]
    [InlineData("files")]
    public async Task ExecuteAsync_CallerCancellation_StillCleansTemporaryItems(string feature)
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        using var sut = new StorageLoadTest(feature, fixture.Services.GetRequiredService<IServiceScopeFactory>());
        using var cancellation = new CancellationTokenSource();
        var run = sut.ExecuteAsync(fixture.Request(feature) with { Seconds = 60 }, cancellation.Token);
        await WaitForReadsAsync(sut);
        cancellation.Cancel();
        var result = JsonSerializer.SerializeToElement(await run.WaitAsync(TimeSpan.FromSeconds(10)));
        result.GetProperty("Phase").GetString().ShouldBe("Stopped");
        result.GetProperty("cleanupPending").GetInt32().ShouldBe(0);
        await fixture.AssertCleanAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentRunAndStaleStop_DoNotReplaceOrCancelActiveRun()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        using var sut = new StorageLoadTest("blobs", fixture.Services.GetRequiredService<IServiceScopeFactory>());
        var token = Guid.NewGuid().ToString("N");
        var run = sut.ExecuteAsync(fixture.Request("blobs") with { Id = token, Seconds = 60 }, default);

        try
        {
            await WaitForReadsAsync(sut);
            JsonSerializer.SerializeToElement(sut.Snapshot()).GetProperty("Id").GetString().ShouldNotBe(token);
            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ExecuteAsync(fixture.Request("blobs"), default));
            sut.Stop(Guid.NewGuid().ToString("N"));
            JsonSerializer.SerializeToElement(sut.Snapshot()).GetProperty("Phase").GetString().ShouldBe("Running");
        }
        finally
        {
            sut.Stop(token);
            await run.WaitAsync(TimeSpan.FromSeconds(10));
        }

        await fixture.AssertCleanAsync();
    }

    [Fact]
    public async Task ExecuteAsync_CleanupFailure_BlocksNewRunsAndCanRetryOwnedCleanup()
    {
        var failDelete = true;
        await using var fixture = new Fixture(failDelete: () => failDelete);
        await fixture.SeedAsync();
        using var sut = new StorageLoadTest("blobs", fixture.Services.GetRequiredService<IServiceScopeFactory>());
        var request = fixture.Request("blobs");
        var result = JsonSerializer.SerializeToElement(await sut.ExecuteAsync(request, default));
        result.GetProperty("Phase").GetString().ShouldBe("Cleanup required");
        result.GetProperty("cleanupPending").GetInt32().ShouldBe(1);
        result.GetProperty("remainingKeys")[0].GetString().ShouldStartWith("reports/bdk-diag-");
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ExecuteAsync(request, default));
        failDelete = false;
        var cleaned = JsonSerializer.SerializeToElement(await sut.RetryCleanupAsync());
        cleaned.GetProperty("Phase").GetString().ShouldBe("Cleaned");
        cleaned.GetProperty("cleanupPending").GetInt32().ShouldBe(0);
        cleaned.GetProperty("Error").ValueKind.ShouldBe(JsonValueKind.Null);
        await fixture.AssertCleanAsync();
    }

    [Fact]
    public async Task RetryCleanupAsync_JsonStringOwner_CleansPersistedPropertyWithoutReplayingLoad()
    {
        var failDelete = true;
        await using var fixture = new Fixture(failDelete: () => failDelete, ownerFormat: "json");
        await fixture.SeedAsync();
        using var sut = new StorageLoadTest("blobs", fixture.Services.GetRequiredService<IServiceScopeFactory>());
        var result = JsonSerializer.SerializeToElement(await sut.ExecuteAsync(fixture.Request("blobs"), default));
        result.GetProperty("Phase").GetString().ShouldBe("Cleanup required");
        result.GetProperty("Error").GetString().ShouldNotContain("ownership changed");
        failDelete = false;

        var cleaned = JsonSerializer.SerializeToElement(await sut.RetryCleanupAsync());

        cleaned.GetProperty("Phase").GetString().ShouldBe("Cleaned");
        cleaned.GetProperty("cleanupPending").GetInt32().ShouldBe(0);
        await fixture.AssertCleanAsync();
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("missing")]
    [InlineData("number")]
    public async Task ExecuteAsync_UnverifiedOwnership_RefusesDeleteEvenOnRetry(string format)
    {
        await using var fixture = new Fixture(ownerFormat: format);
        await fixture.SeedAsync();
        using var sut = new StorageLoadTest("blobs", fixture.Services.GetRequiredService<IServiceScopeFactory>());
        var result = JsonSerializer.SerializeToElement(await sut.ExecuteAsync(fixture.Request("blobs"), default));
        result.GetProperty("Phase").GetString().ShouldBe("Cleanup required");
        result.GetProperty("Error").GetString().ShouldContain("ownership changed");

        var retried = JsonSerializer.SerializeToElement(await sut.RetryCleanupAsync());

        retried.GetProperty("cleanupPending").GetInt32().ShouldBe(1);
        fixture.DeleteAttempts.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_IncompleteRead_DoesNotCountDeclaredLengthAndCleansUp()
    {
        await using var fixture = new Fixture(shortRead: true);
        await fixture.SeedAsync();
        using var sut = new StorageLoadTest("blobs", fixture.Services.GetRequiredService<IServiceScopeFactory>());
        var result = JsonSerializer.SerializeToElement(await sut.ExecuteAsync(fixture.Request("blobs", 100), default));
        result.GetProperty("Phase").GetString().ShouldBe("Failed");
        result.GetProperty("readBytes").GetInt64().ShouldBe(0);
        result.GetProperty("errors").GetInt64().ShouldBeGreaterThan(0);
        result.GetProperty("cleanupPending").GetInt32().ShouldBe(0);
        await fixture.AssertCleanAsync();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task ExecuteAsync_DurationExpiresDuringWrite_DrainsOperationWithoutCancelingIt(int workers)
    {
        await using var fixture = new Fixture(slowWrite: true);
        await fixture.SeedAsync();
        using var sut = new StorageLoadTest("blobs", fixture.Services.GetRequiredService<IServiceScopeFactory>());

        var result = JsonSerializer.SerializeToElement(await sut.ExecuteAsync(fixture.Request("blobs", 0) with { Workers = workers }, default));

        result.GetProperty("Phase").GetString().ShouldBe("Completed");
        result.GetProperty("writeItems").GetInt64().ShouldBe(workers);
        result.GetProperty("errors").GetInt64().ShouldBe(0);
        result.GetProperty("cleanupPending").GetInt32().ShouldBe(0);
        await fixture.AssertCleanAsync();
    }

    [Fact]
    public async Task ExecuteAsync_FileBehaviors_DoesNotInvokeApplicationSideEffects()
    {
        await using var fixture = new Fixture(decorateFiles: true);
        await fixture.SeedAsync();
        using var sut = new StorageLoadTest("files", fixture.Services.GetRequiredService<IServiceScopeFactory>());

        var result = JsonSerializer.SerializeToElement(await sut.ExecuteAsync(fixture.Request("files"), default));

        result.GetProperty("Phase").GetString().ShouldBe("Completed");
        fixture.Decorator.ReceivedCalls().Select(call => call.GetMethodInfo().Name).ShouldNotContain("WriteFileAsync");
        fixture.Decorator.ReceivedCalls().Select(call => call.GetMethodInfo().Name).ShouldNotContain("DeleteFileAsync");
        await fixture.AssertCleanAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WriteCommittedBeforeFailure_ReconcilesOwnershipAndCleansWithoutReplayingWrite()
    {
        await using var fixture = new Fixture(uncertainWrite: true);
        await fixture.SeedAsync();
        using var sut = new StorageLoadTest("blobs", fixture.Services.GetRequiredService<IServiceScopeFactory>());

        var result = JsonSerializer.SerializeToElement(await sut.ExecuteAsync(fixture.Request("blobs"), default));

        result.GetProperty("Phase").GetString().ShouldBe("Failed");
        result.GetProperty("writeBytes").GetInt64().ShouldBe(0);
        result.GetProperty("cleanupPending").GetInt32().ShouldBe(0);
        await fixture.AssertCleanAsync();
    }

    [Fact]
    public async Task ExecuteAsync_InvalidSettingsOrNewContainer_DoesNotWrite()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        using var sut = new StorageLoadTest("blobs", fixture.Services.GetRequiredService<IServiceScopeFactory>());
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.ExecuteAsync(fixture.Request("blobs") with { Workers = 17 }, default));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.ExecuteAsync(fixture.Request("blobs") with { Container = "not-existing" }, default));
        await fixture.AssertCleanAsync();
    }

    [Theory]
    [InlineData("blobs")]
    [InlineData("documents")]
    public async Task ExecuteAsync_MaximumWorkers_CompletesAndRemovesTemporaryItems(string feature)
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        using var sut = new StorageLoadTest(feature, fixture.Services.GetRequiredService<IServiceScopeFactory>());
        var result = JsonSerializer.SerializeToElement(await sut.ExecuteAsync(fixture.Request(feature, 100) with { Workers = 16 }, default));
        result.GetProperty("errors").GetInt64().ShouldBe(0, result.ToString());
        result.GetProperty("Phase").GetString().ShouldBe("Completed");
        result.GetProperty("readItems").GetInt64().ShouldBeGreaterThan(0);
        result.GetProperty("cleanupPending").GetInt32().ShouldBe(0);
        await fixture.AssertCleanAsync();
    }

    [Fact]
    public void Validate_MaximumWorkers_AcceptsRequest()
    {
        var request = new StorageLoadRequest { Target = "files", Workers = 16 };
        request.Validate();
    }

    private static async Task WaitForReadsAsync(StorageLoadTest sut)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        while (JsonSerializer.SerializeToElement(sut.Snapshot()).GetProperty("readBytes").GetInt64() == 0)
        {
            await Task.Delay(10, deadline.Token);
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        internal ServiceProvider Services { get; }

        internal IFileStorageBehavior Decorator { get; private set; }

        internal int DeleteAttempts { get; private set; }

        private readonly InMemoryBlobStoreProvider blobs = new();
        private readonly InMemoryDocumentStoreProvider documents = new();
        private readonly InMemoryFileStorageProvider files = new("files");

        internal Fixture(Func<bool> failDelete = null, bool shortRead = false, bool decorateFiles = false, bool uncertainWrite = false, bool slowWrite = false, string ownerFormat = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddBlobStorage().WithInMemoryClient("reports");
            IBlobStoreProvider provider = this.blobs;

            if (failDelete is not null || shortRead || uncertainWrite || slowWrite || ownerFormat is not null)
            {
                var writes = 0;
                provider = Substitute.For<IBlobStoreProvider, IBlobStoreContainerCatalog>();
                provider.UploadAsync(Arg.Any<BlobUpload>(), Arg.Any<CancellationToken>()).Returns(async call =>
                {
                    if (slowWrite && Interlocked.Increment(ref writes) > 1)
                    {
                        await Task.Delay(1200, call.Arg<CancellationToken>());
                    }

                    var result = await this.blobs.UploadAsync(call.Arg<BlobUpload>(), call.Arg<CancellationToken>());

                    return uncertainWrite ? Result<BlobInfo>.Failure(new BlobStoreIndeterminateOperationError("publish", "mutation")) : result;
                });
                provider.GetPropertiesAsync(Arg.Any<BlobKey>(), Arg.Any<CancellationToken>()).Returns(async call =>
                {
                    var result = await this.blobs.GetPropertiesAsync(call.Arg<BlobKey>(), call.Arg<CancellationToken>());

                    if (result.IsSuccess && ownerFormat is not null)
                    {
                        var properties = result.Value.Properties;
                        properties["bdk_diagnostic_owner"] = ownerFormat switch
                        {
                            "json" => JsonSerializer.SerializeToElement(properties.Get("bdk_diagnostic_owner")),
                            "foreign" => JsonSerializer.SerializeToElement("another-owner"),
                            "number" => JsonSerializer.SerializeToElement(123),
                            _ => null
                        };
                    }

                    return result;
                });
                provider.ExistsAsync(Arg.Any<BlobKey>(), Arg.Any<CancellationToken>()).Returns(call => this.blobs.ExistsAsync(call.Arg<BlobKey>(), call.Arg<CancellationToken>()));
                provider.DownloadAsync(Arg.Any<BlobKey>(), Arg.Any<CancellationToken>()).Returns(call => shortRead ? Task.FromResult(Result<BlobDownload>.Success(new() { Content = new MemoryStream([1]) })) : this.blobs.DownloadAsync(call.Arg<BlobKey>(), call.Arg<CancellationToken>()));
                provider.DeleteAsync(Arg.Any<BlobKey>(), Arg.Any<BlobDeleteOptions>(), Arg.Any<CancellationToken>()).Returns(call =>
                {
                    this.DeleteAttempts++;

                    return failDelete?.Invoke() == true ? Task.FromResult(Result.Failure(new StorageError("Offline"))) : this.blobs.DeleteAsync(call.Arg<BlobKey>(), call.Arg<BlobDeleteOptions>(), call.Arg<CancellationToken>());
                });
                ((IBlobStoreContainerCatalog)provider).ListContainersAsync(Arg.Any<CancellationToken>()).Returns(call => this.blobs.ListContainersAsync(call.Arg<CancellationToken>()));
            }

            services.AddKeyedSingleton("reports", provider);
            services.AddDocumentStorage().WithProvider<TestDocument>(_ => this.documents);
            services.AddFileStorage(factory => factory.RegisterProvider("files", builder =>
            {
                builder.ProviderFactory = _ => this.files;
                builder.WithLifetime(ServiceLifetime.Singleton);

                if (decorateFiles)
                {
                    builder.WithBehavior((provider, _) =>
                    {
                        this.Decorator = Substitute.For<IFileStorageBehavior>();
                        this.Decorator.InnerProvider.Returns(provider);

                        return this.Decorator;
                    });
                }
            }));
            this.Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        internal StorageLoadRequest Request(string feature, int readPercent = 50)
        {
            return new()
            {
                Target = feature switch
                {
                    "blobs" => "reports",
                    "files" => "files",
                    _ => this.Services.GetServices<DocumentStoreClientDescriptor>().Single().ClientId
                },
                Container = "reports",
                Seconds = 1,
                PayloadKiB = 4,
                ReadPercent = readPercent
            };
        }

        internal async Task SeedAsync()
        {
            (await this.blobs.UploadAsync(new() { Key = new("reports", "keep"), Content = new MemoryStream([1, 2, 3]) })).IsSuccess.ShouldBeTrue();
            (await this.documents.UpsertAsync(DocumentTypeIdentity.For<TestDocument>(), new() { Key = new("keep", "keep"), Content = [1, 2, 3] })).IsSuccess.ShouldBeTrue();
            (await this.files.WriteFileAsync("keep", new MemoryStream([1, 2, 3]))).IsSuccess.ShouldBeTrue();
        }

        internal async Task AssertCleanAsync()
        {
            this.blobs.Context.GetSnapshot().Select(item => item.Info.Key.Name).ShouldBe(["keep"]);
            var docs = await this.documents.CountAsync(new("bdk.storage.diagnostics.v1"), new() { AllowFullScan = true }, DateTimeOffset.UtcNow);
            docs.Value.ShouldBe(0);
            (await this.documents.GetAsync(DocumentTypeIdentity.For<TestDocument>(), new("keep", "keep"), DateTimeOffset.UtcNow)).Value.Content.ShouldBe(new byte[] { 1, 2, 3 });
            var files = await this.files.ListFilesAsync("/");
            files.IsSuccess.ShouldBeTrue();
            files.Value.Files.ShouldBe(["keep"]);
            var read = await this.blobs.DownloadAsync(new("reports", "keep"));
            await using var stream = read.Value.Content;
            stream.ReadByte().ShouldBe(1);
        }

        public ValueTask DisposeAsync()
        {
            return this.Services.DisposeAsync();
        }
    }

    public sealed class TestDocument;
}
