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
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using System.Reflection;

/// <summary>Exercises the EF blob provider contract and transaction error handling.</summary>
/// <example><code>await new EntityFrameworkBlobStoreProviderContractTests().SweepExpiredAsync_WithExpiredBlobs_DeletesExpiredRowsAndChunksOnly();</code></example>
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

    /// <summary>Checks which rollback failures the provider allows to propagate.</summary>
    /// <example><code>await tests.RollbackAsync_WhenTransactionFails_SuppressesOnlyExpectedErrors(typeof(InvalidOperationException), true);</code></example>
    [Theory]
    [InlineData(typeof(InvalidOperationException), true)]
    [InlineData(typeof(DbException), true)]
    [InlineData(typeof(OperationCanceledException), false)]
    [InlineData(typeof(OutOfMemoryException), false)]
    public async Task RollbackAsync_WhenTransactionFails_SuppressesOnlyExpectedErrors(Type exceptionType, bool suppress)
    {
        var sut = this.CreateProvider();
        var transaction = Substitute.For<IDbContextTransaction>();
        var failure = exceptionType == typeof(DbException)
            ? Substitute.For<DbException>()
            : (Exception)Activator.CreateInstance(exceptionType);
        transaction.RollbackAsync(CancellationToken.None).Returns(Task.FromException(failure));
        var rollback = sut.GetType().GetMethod("RollbackAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        var thrown = await Record.ExceptionAsync(() => (Task)rollback.Invoke(sut, [transaction, CancellationToken.None]));

        thrown.ShouldBeSameAs(suppress ? null : failure);
        await transaction.Received(1).RollbackAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SweepExpiredAsync_WithExpiredBlobs_DeletesExpiredRowsAndChunksOnly()
    {
        // Arrange
        var provider = (EntityFrameworkBlobStoreProvider<ContractBlobDbContext>)this.CreateProvider();
        var now = DateTimeOffset.UtcNow;
        var expired = CreateKey("ef-retention/expired.txt");
        var future = CreateKey("ef-retention/future.txt");
        await provider.UploadAsync(new StoredBlobUpload
        {
            Key = expired,
            Content = new MemoryStream([1, 2, 3]),
            ExpiresAt = now.AddMinutes(-1)
        });
        await provider.UploadAsync(new StoredBlobUpload
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
