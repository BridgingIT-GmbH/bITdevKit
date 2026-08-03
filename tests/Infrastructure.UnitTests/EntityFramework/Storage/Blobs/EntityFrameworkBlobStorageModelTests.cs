// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Storage;

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Infrastructure")]
public sealed class EntityFrameworkBlobStorageModelTests
{
    [Fact]
    public void BlobStoreDbContext_AsBlobStoreContext_ExposesRequiredDbSets()
    {
        // Arrange
        using var sut = CreateContext();

        // Act
        var context = sut as IBlobStoreContext;

        // Assert
        context.ShouldNotBeNull();
        context.StorageBlobs.ShouldNotBeNull();
        context.StorageBlobChunks.ShouldNotBeNull();
    }

    [Fact]
    public void Model_WithBlobStorageConfiguration_ContainsRequiredStorageBlobColumns()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var sut = context.Model.FindEntityType(typeof(StorageBlob));

        // Assert
        sut.ShouldNotBeNull();
        typeof(StorageBlob).GetCustomAttribute<TableAttribute>()?.Name.ShouldBe("__Storage_Blobs");
        sut.FindProperty(nameof(StorageBlob.Id)).ShouldNotBeNull();
        sut.FindProperty(nameof(StorageBlob.Container)).ShouldNotBeNull();
        sut.FindProperty(nameof(StorageBlob.Name)).ShouldNotBeNull();
        sut.FindProperty(nameof(StorageBlob.ContainerHash)).ShouldNotBeNull();
        sut.FindProperty(nameof(StorageBlob.NameHash)).ShouldNotBeNull();
        sut.FindProperty(nameof(StorageBlob.Length)).ShouldNotBeNull();
        sut.FindProperty(nameof(StorageBlob.ContentTypeMimeType)).ShouldNotBeNull();
        sut.FindProperty(nameof(StorageBlob.ContentHash)).ShouldNotBeNull();
        sut.FindProperty(nameof(StorageBlob.ETag)).ShouldNotBeNull();
        sut.FindProperty(nameof(StorageBlob.CreatedAt)).ShouldNotBeNull();
        sut.FindProperty(nameof(StorageBlob.LastModifiedAt)).ShouldNotBeNull();
        sut.FindProperty(nameof(StorageBlob.ExpiresAt)).ShouldNotBeNull();
        sut.FindProperty(nameof(StorageBlob.PropertiesJson)).ShouldNotBeNull();
        sut.FindProperty(nameof(StorageBlob.LeaseId)).ShouldNotBeNull();
        sut.FindProperty(nameof(StorageBlob.LeaseAcquiredBy)).ShouldNotBeNull();
        sut.FindProperty(nameof(StorageBlob.LeaseAcquiredUntil)).ShouldNotBeNull();
        sut.FindProperty(nameof(StorageBlob.ConcurrencyVersion)).ShouldNotBeNull();
    }

    [Fact]
    public void Model_WithBlobStorageConfiguration_ContainsRequiredStorageBlobChunkColumns()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var sut = context.Model.FindEntityType(typeof(StorageBlobChunk));

        // Assert
        sut.ShouldNotBeNull();
        typeof(StorageBlobChunk).GetCustomAttribute<TableAttribute>()?.Name.ShouldBe("__Storage_BlobChunks");
        sut.FindProperty(nameof(StorageBlobChunk.BlobId)).ShouldNotBeNull();
        sut.FindProperty(nameof(StorageBlobChunk.Index)).ShouldNotBeNull();
        sut.FindProperty(nameof(StorageBlobChunk.Content)).ShouldNotBeNull();
        sut.FindProperty(nameof(StorageBlobChunk.Length)).ShouldNotBeNull();
    }

    [Fact]
    public void StorageBlob_WithConfiguration_HasUniqueContainerHashAndNameHashLookup()
    {
        // Arrange
        using var context = CreateContext();
        var sut = context.Model.FindEntityType(typeof(StorageBlob));

        // Act
        var uniqueHashIndex = sut.GetIndexes().SingleOrDefault(index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(StorageBlob.ContainerHash), nameof(StorageBlob.NameHash)]));
        var prefixIndex = sut.GetIndexes().SingleOrDefault(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(StorageBlob.Container), nameof(StorageBlob.Name)]));
        var leaseIndex = sut.GetIndexes().SingleOrDefault(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(StorageBlob.LeaseAcquiredUntil)]));
        var expiresAtIndex = sut.GetIndexes().SingleOrDefault(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(StorageBlob.ExpiresAt)]));

        // Assert
        uniqueHashIndex.ShouldNotBeNull();
        prefixIndex.ShouldNotBeNull();
        leaseIndex.ShouldNotBeNull();
        expiresAtIndex.ShouldNotBeNull();
    }

    [Fact]
    public void StorageBlobChunk_WithConfiguration_HasUniqueBlobIdAndIndexSupport()
    {
        // Arrange
        using var context = CreateContext();
        var sut = context.Model.FindEntityType(typeof(StorageBlobChunk));

        // Act
        var primaryKey = sut.FindPrimaryKey();
        var uniqueIndex = sut.GetIndexes().SingleOrDefault(index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(StorageBlobChunk.BlobId), nameof(StorageBlobChunk.Index)]));

        // Assert
        primaryKey.ShouldNotBeNull();
        primaryKey.Properties.Select(property => property.Name)
            .ShouldBe([nameof(StorageBlobChunk.BlobId), nameof(StorageBlobChunk.Index)]);
        uniqueIndex.ShouldNotBeNull();
    }

    [Fact]
    public void StorageBlob_ContentTypeMimeType_StoresMimeStringNotContentTypeEnum()
    {
        // Arrange
        using var context = CreateContext();
        var sut = context.Model.FindEntityType(typeof(StorageBlob))
            .FindProperty(nameof(StorageBlob.ContentTypeMimeType));

        // Act
        var clrType = sut.ClrType;

        // Assert
        clrType.ShouldBe(typeof(string));
        clrType.ShouldNotBe(typeof(ContentType));
    }

    [Fact]
    public void StorageBlob_Properties_UsesNotMappedDictionaryAndPropertiesJsonColumn()
    {
        // Arrange
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(StorageBlob));

        // Act
        var properties = entityType.FindProperty(nameof(StorageBlob.Properties));
        var propertiesJson = entityType.FindProperty(nameof(StorageBlob.PropertiesJson));
        var row = new StorageBlob();
        row.Properties["source"] = "monthly-export";
        row.Properties["reviewed"] = true;
        var serialized = row.PropertiesJson;
        var restored = new StorageBlob { PropertiesJson = serialized };

        // Assert
        properties.ShouldBeNull();
        propertiesJson.ShouldNotBeNull();
        typeof(StorageBlob).GetProperty(nameof(StorageBlob.PropertiesJson))
            ?.GetCustomAttribute<ColumnAttribute>()?.Name.ShouldBe("Properties");
        serialized.ShouldContain("monthly-export");
        restored.Properties["source"].ToString().ShouldBe("monthly-export");
        restored.Properties["reviewed"].ToString().ShouldBe("True");
    }

    [Fact]
    public void WithEntityFrameworkClient_WithContextNotImplementingBlobStoreContext_FailsGenericGuard()
    {
        // Arrange
        var extensionType = typeof(EntityFrameworkBlobStoreProvider<>).Assembly
            .GetType("Microsoft.Extensions.DependencyInjection.ServiceCollectionExtensions");
        extensionType.ShouldNotBeNull();

        var methods = extensionType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method =>
                method.Name == "WithEntityFrameworkClient" &&
                method.IsGenericMethodDefinition &&
                method.GetGenericArguments().Length == 1 &&
                method.GetParameters().FirstOrDefault()?.ParameterType == typeof(BlobStorageBuilderContext))
            .ToArray();

        // Assert
        methods.Length.ShouldBeGreaterThanOrEqualTo(1);
        foreach (var method in methods)
        {
            Action action = () => method.MakeGenericMethod(typeof(NonBlobDbContext));
            action.ShouldThrow<ArgumentException>();
        }
    }

    [Fact]
    public async Task EntityFrameworkClient_WithSingletonLifetime_ResolvesScopedDbContextPerOperation()
    {
        // Arrange
        TrackingBlobStoreDbContext.Reset();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TrackingBlobStoreDbContext>(options =>
            options.UseInMemoryDatabase($"blob-storage-{Guid.NewGuid():N}"));
        services.AddBlobStorage(options => options.UseLifetime(ServiceLifetime.Singleton))
            .WithEntityFrameworkClient<TrackingBlobStoreDbContext>("reports");

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = serviceProvider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IBlobStoreClientFactory>().CreateClient("reports");

        // Act
        var first = await sut.ExistsAsync(new BlobKey("reports", "first.bin"));
        var second = await sut.ExistsAsync(new BlobKey("reports", "second.bin"));

        // Assert
        first.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, first.Errors.Select(e => e.Message)));
        second.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, second.Errors.Select(e => e.Message)));
        first.Value.ShouldBeFalse();
        second.Value.ShouldBeFalse();
        TrackingBlobStoreDbContext.CreatedInstanceIds.Count.ShouldBe(2);
        TrackingBlobStoreDbContext.DisposedInstanceIds.Count.ShouldBe(2);
        TrackingBlobStoreDbContext.CreatedInstanceIds.Distinct().Count().ShouldBe(2);
    }

    private static BlobStoreDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<BlobStoreDbContext>()
            .UseInMemoryDatabase($"blob-storage-model-{Guid.NewGuid():N}")
            .Options);

    private sealed class BlobStoreDbContext(DbContextOptions<BlobStoreDbContext> options)
        : DbContext(options), IBlobStoreContext
    {
        public DbSet<StorageBlob> StorageBlobs { get; set; }

        public DbSet<StorageBlobChunk> StorageBlobChunks { get; set; }
    }

    private sealed class NonBlobDbContext(DbContextOptions<NonBlobDbContext> options) : DbContext(options);

    private sealed class TrackingBlobStoreDbContext : DbContext, IBlobStoreContext
    {
        public TrackingBlobStoreDbContext(DbContextOptions<TrackingBlobStoreDbContext> options)
            : base(options)
        {
            CreatedInstanceIds.Add(this.InstanceId);
        }

        public static ConcurrentBag<Guid> CreatedInstanceIds { get; } = [];

        public static ConcurrentBag<Guid> DisposedInstanceIds { get; } = [];

        public Guid InstanceId { get; } = Guid.NewGuid();

        public DbSet<StorageBlob> StorageBlobs { get; set; }

        public DbSet<StorageBlobChunk> StorageBlobChunks { get; set; }

        public static void Reset()
        {
            CreatedInstanceIds.Clear();
            DisposedInstanceIds.Clear();
        }

        public override void Dispose()
        {
            DisposedInstanceIds.Add(this.InstanceId);
            base.Dispose();
        }

        public override async ValueTask DisposeAsync()
        {
            DisposedInstanceIds.Add(this.InstanceId);
            await base.DisposeAsync();
        }
    }
}
