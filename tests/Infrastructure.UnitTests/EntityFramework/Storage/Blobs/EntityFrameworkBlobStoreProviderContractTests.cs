// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Storage;

using Application.Storage;
using Application.UnitTests.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Infrastructure")]
public sealed class EntityFrameworkBlobStoreProviderContractTests : BlobStoreProviderContractTests
{
    private ContractBlobDbContext context;

    protected override string ProviderName => EntityFrameworkBlobStoreProvider<ContractBlobDbContext>.ProviderName;

    protected override IBlobStoreProvider CreateProvider(BlobStoreOptions options = null)
    {
        this.context = new ContractBlobDbContext(new DbContextOptionsBuilder<ContractBlobDbContext>()
                .UseInMemoryDatabase($"blob-contract-{Guid.NewGuid():N}")
                .Options);

        return new EntityFrameworkBlobStoreProvider<ContractBlobDbContext>(
            new SingleContextScopeFactory<ContractBlobDbContext>(this.context),
            options);
    }

    [Fact]
    public async Task SweepExpiredAsync_WithExpiredBlobs_DeletesExpiredRowsAndChunksOnly()
    {
        // Arrange
        var provider = (EntityFrameworkBlobStoreProvider<ContractBlobDbContext>)this.CreateProvider();
        var now = DateTimeOffset.UtcNow;
        var expired = CreateKey("ef-retention/expired.txt");
        var future = CreateKey("ef-retention/future.txt");
        await provider.UploadAsync(new BlobUpload
        {
            Key = expired,
            Content = new MemoryStream([1, 2, 3]),
            ExpiresAt = now.AddMinutes(-1)
        });
        await provider.UploadAsync(new BlobUpload
        {
            Key = future,
            Content = new MemoryStream([4, 5, 6]),
            ExpiresAt = now.AddMinutes(1)
        });

        // Act
        var result = await provider.SweepExpiredAsync(new BlobRetentionSweepRequest
        {
            StoreName = "ef",
            ProviderName = this.ProviderName,
            ExpiresOnOrBefore = now,
            BatchSize = 10,
            MaxBatches = 2
        });
        var expiredExists = await provider.ExistsAsync(expired);
        var futureExists = await provider.ExistsAsync(future);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.DeletedCount.ShouldBe(1);
        result.Value.DeletedKeys.ShouldBe([expired]);
        expiredExists.Value.ShouldBeFalse();
        futureExists.Value.ShouldBeTrue();
        this.context.StorageBlobChunks.Count().ShouldBe(1);
    }

    protected override void ResetContentReadProbe() => this.context.ResetObservedState();

    protected override void AssertContentWasNotReadForMetadataOperations() =>
        this.context.StorageBlobChunksAccessCount.ShouldBe(0);

    private sealed class ContractBlobDbContext(DbContextOptions<ContractBlobDbContext> options)
        : DbContext(options), IBlobStoreContext
    {
        public DbSet<StorageBlob> StorageBlobs { get; set; }

        public int StorageBlobChunksAccessCount { get; private set; }

        public DbSet<StorageBlobChunk> StorageBlobChunks
        {
            get
            {
                this.StorageBlobChunksAccessCount++;
                return this.Set<StorageBlobChunk>();
            }

            set { }
        }

        public void ResetObservedState() => this.StorageBlobChunksAccessCount = 0;
    }

    private sealed class SingleContextScopeFactory<TContext>(TContext context)
        : IServiceScopeFactory, IServiceScope, IServiceProvider
        where TContext : DbContext
    {
        public IServiceProvider ServiceProvider => this;

        public IServiceScope CreateScope() => this;

        public object GetService(Type serviceType) => serviceType == typeof(TContext) ? context : null;

        public void Dispose() { }
    }
}
