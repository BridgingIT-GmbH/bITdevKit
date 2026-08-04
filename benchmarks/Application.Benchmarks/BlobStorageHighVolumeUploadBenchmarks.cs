// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

[MemoryDiagnoser]
public class BlobStorageHighVolumeUploadBenchmarks
{
    private readonly byte[] payload = CreatePayload();
    private ChunkFlushSaveChangesInterceptor interceptor;
    private ServiceProvider serviceProvider;
    private EntityFrameworkBlobStoreProvider<BenchmarkBlobDbContext> provider;

    [Params(1, 4)]
    public int ChunkFlushCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        this.interceptor = new ChunkFlushSaveChangesInterceptor();
        services.AddDbContext<BenchmarkBlobDbContext>(options =>
            options
                .UseInMemoryDatabase($"blob-high-volume-{Guid.NewGuid():N}")
                .AddInterceptors(this.interceptor));
        this.serviceProvider = services.BuildServiceProvider();
        this.provider = new EntityFrameworkBlobStoreProvider<BenchmarkBlobDbContext>(
            this.serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            new BlobStoreOptions
            {
                ChunkSize = 64 * 1024,
                ChunkFlushCount = this.ChunkFlushCount,
                MaxPendingChunkBytes = 16 * 1024 * 1024
            },
            storeName: "benchmark");
    }

    [IterationSetup]
    public void ResetDatabase()
    {
        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BenchmarkBlobDbContext>();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();
        this.interceptor.Reset();
    }

    [GlobalCleanup]
    public void Cleanup() => this.serviceProvider?.Dispose();

    [Benchmark]
    public async Task<long> UploadFourMegabytes()
    {
        var result = await this.provider.UploadAsync(new BlobUpload
        {
            Key = new BlobKey("benchmarks", "upload.bin"),
            Content = new MemoryStream(this.payload, writable: false)
        });

        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        }

        const int expectedChunkCount = 64;
        var expectedFlushCount = (int)Math.Ceiling((double)expectedChunkCount / this.ChunkFlushCount);
        if (this.interceptor.ChunkCount != expectedChunkCount ||
            this.interceptor.FlushCount != expectedFlushCount)
        {
            throw new InvalidOperationException(
                $"Observed {this.interceptor.ChunkCount} chunks in {this.interceptor.FlushCount} flushes; " +
                $"expected {expectedChunkCount} chunks in {expectedFlushCount} flushes.");
        }

        return result.Value.Length;
    }

    private static byte[] CreatePayload()
    {
        var bytes = new byte[4 * 1024 * 1024];
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = (byte)(index % 251);
        }

        return bytes;
    }

    private sealed class BenchmarkBlobDbContext(DbContextOptions<BenchmarkBlobDbContext> options)
        : DbContext(options), IBlobStoreContext
    {
        public DbSet<StorageBlob> StorageBlobs { get; set; }

        public DbSet<StorageBlobChunk> StorageBlobChunks { get; set; }
    }

    private sealed class ChunkFlushSaveChangesInterceptor : SaveChangesInterceptor
    {
        private int chunkCount;
        private int flushCount;

        public int ChunkCount => Volatile.Read(ref this.chunkCount);

        public int FlushCount => Volatile.Read(ref this.flushCount);

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var pendingChunkCount = eventData.Context?.ChangeTracker
                .Entries<StorageBlobChunk>()
                .Count(entry => entry.State == EntityState.Added) ?? 0;
            if (pendingChunkCount > 0)
            {
                Interlocked.Add(ref this.chunkCount, pendingChunkCount);
                Interlocked.Increment(ref this.flushCount);
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public void Reset()
        {
            Volatile.Write(ref this.chunkCount, 0);
            Volatile.Write(ref this.flushCount, 0);
        }
    }
}
