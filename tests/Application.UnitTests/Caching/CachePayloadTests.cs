// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Compression;

public class CachePayloadTests
{
    [Theory]
    [InlineData(CompressionLevel.Optimal)]
    [InlineData(CompressionLevel.SmallestSize)]
    public async Task HighQualityBrotli_DeclinesBeforeCreatingNativeStateWhenWorkspaceCannotFit(CompressionLevel level)
    {
        await using var fixture = new Fixture(CacheCompressionAlgorithm.Brotli, level: level);
        var input = new byte[140000];
        var identity = fixture.Runtime.Identity("bounded");
        var fence = (await fixture.Runtime.Access.CallAsync(token => fixture.Provider.CaptureFenceAsync(identity, token), default)).Value;
        var created = await CachePayloadStage.CreateAsync(fixture.Provider, new(identity, fence, default, 1024 * 1024),
            fixture.Binding, fixture.Resources, TimeProvider.System, new("bounded"), "RAW", 1, default, fixture.Runtime.Access, fixture.Tasks);
        await using (var stage = created.Value)
        {
            await stage.Stream.WriteAsync(input);
            (await stage.CompleteAsync(new(), null, default)).IsSuccess.ShouldBeTrue();
            stage.Metadata.Compression.ShouldBe("none");
            (await stage.PublishAsync(default)).Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        }

        await fixture.Provider.ReleaseFenceAsync(identity, fence);
        var opened = await fixture.Runtime.OpenAsync("bounded", null, default);
        await using (var handle = opened.Value.Value)
        {
            using var output = new MemoryStream();
            await handle.Stream.CopyToAsync(output);
            output.ToArray().ShouldBe(input);
            (await handle.WaitForCompletionAsync()).Value.ShouldBe(CacheReadCompletion.Validated);
        }

        fixture.Resources.Transient.Used.ShouldBe(0);
    }

    [Theory]
    [InlineData(CompressionLevel.Optimal)]
    [InlineData(CompressionLevel.SmallestSize)]
    public async Task HighQualityBrotli_WithAdmittedNativeWorkspace_CompressesAndRemainsReadable(CompressionLevel level)
    {
        await using var fixture = new Fixture(CacheCompressionAlgorithm.Brotli, level: level, largeWorkspace: true);
        var input = new byte[140000];
        var identity = fixture.Runtime.Identity("quality");
        var fence = (await fixture.Runtime.Access.CallAsync(token => fixture.Provider.CaptureFenceAsync(identity, token), default)).Value;
        var created = await CachePayloadStage.CreateAsync(fixture.Provider, new(identity, fence, default, 1024 * 1024),
            fixture.Binding, fixture.Resources, TimeProvider.System, new("quality"), "RAW", 1, default, fixture.Runtime.Access, fixture.Tasks);
        await using (var stage = created.Value)
        {
            await stage.Stream.WriteAsync(input);
            (await stage.CompleteAsync(new(), null, default)).IsSuccess.ShouldBeTrue();
            stage.Metadata.Compression.ShouldBe("brotli");
            (await stage.PublishAsync(default)).Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        }

        await fixture.Provider.ReleaseFenceAsync(identity, fence);
        var opened = await fixture.Runtime.OpenAsync("quality", null, default);
        await using (var handle = opened.Value.Value)
        {
            using var output = new MemoryStream();
            await handle.Stream.CopyToAsync(output);
            output.ToArray().ShouldBe(input);
            (await handle.WaitForCompletionAsync()).Value.ShouldBe(CacheReadCompletion.Validated);
        }

        fixture.Resources.Transient.Used.ShouldBe(0);
    }

    [Theory]
    [InlineData(CacheCompressionAlgorithm.GZip)]
    [InlineData(CacheCompressionAlgorithm.Brotli)]
    public async Task Compression_OneOriginalInputPassAndConfirmedRead(CacheCompressionAlgorithm algorithm)
    {
        await using var fixture = new Fixture(algorithm);
        var input = new byte[140000];
        var identity = fixture.Runtime.Identity("one");
        var fence = (await fixture.Runtime.Access.CallAsync(token => fixture.Provider.CaptureFenceAsync(identity, token), default)).Value;
        var created = await CachePayloadStage.CreateAsync(fixture.Provider, new(identity, fence, default, 1024 * 1024),
            fixture.Binding, fixture.Resources, TimeProvider.System, new("one"), "RAW", 1, default, fixture.Runtime.Access, fixture.Tasks);
        await using (var sut = created.Value)
        {
            // Source content is not provided to CompleteAsync, so compression cannot replay it.
            await sut.Stream.WriteAsync(input);
            (await sut.CompleteAsync(new(), null, default)).IsSuccess.ShouldBeTrue();
            sut.Metadata.Compression.ShouldBe(algorithm == CacheCompressionAlgorithm.GZip ? "gzip" : "brotli");
            (await sut.PublishAsync(default)).Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        }

        (await fixture.Provider.ReleaseFenceAsync(identity, fence)).IsSuccess.ShouldBeTrue();
        var opened = await fixture.Runtime.OpenAsync("ONE", null, default);
        opened.IsSuccess.ShouldBeTrue();
        opened.Value.Found.ShouldBeTrue();
        await using (var handle = opened.Value.Value)
        {
            using var output = new MemoryStream();
            await handle.Stream.CopyToAsync(output);
            output.ToArray().ShouldBe(input);
            (await handle.WaitForCompletionAsync()).Value.ShouldBe(CacheReadCompletion.Validated);
        }

        fixture.Resources.Transient.Used.ShouldBe(0);
        fixture.Resources.Transfers.Active.ShouldBe(0);
        fixture.Resources.ProviderIO.Active.ShouldBe(0);
    }

    [Fact]
    public async Task IncompressibleCandidate_KeepsOriginalAndSupportsCheckedRange()
    {
        await using var fixture = new Fixture(CacheCompressionAlgorithm.GZip);
        var input = new byte[140000];
        Random.Shared.NextBytes(input);
        var identity = fixture.Runtime.Identity("one");
        var fence = (await fixture.Runtime.Access.CallAsync(token => fixture.Provider.CaptureFenceAsync(identity, token), default)).Value;
        var created = await CachePayloadStage.CreateAsync(fixture.Provider, new(identity, fence, default, 1024 * 1024),
            fixture.Binding, fixture.Resources, TimeProvider.System, new("one"), "RAW", 1, default, fixture.Runtime.Access, fixture.Tasks);
        await using (var sut = created.Value)
        {
            await sut.Stream.WriteAsync(input);
            (await sut.CompleteAsync(new(), null, default)).IsSuccess.ShouldBeTrue();
            sut.Metadata.Compression.ShouldBe("none");
            (await sut.PublishAsync(default)).Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        }

        await fixture.Provider.ReleaseFenceAsync(identity, fence);
        var opened = await fixture.Runtime.OpenAsync("one", new(Offset: 65530, Length: 300), default);
        opened.IsSuccess.ShouldBeTrue();
        await using (var handle = opened.Value.Value)
        {
            using var output = new MemoryStream();
            await handle.Stream.CopyToAsync(output);
            output.ToArray().ShouldBe(input.AsSpan(65530, 300).ToArray());
            handle.Range.ShouldBe(new CacheReadRange(65530, 300));
            handle.IntegrityScope.ShouldBe(CacheReadIntegrityScope.AffectedStoredFrames);
            (await handle.WaitForCompletionAsync()).Value.ShouldBe(CacheReadCompletion.RangeCompleted);
        }

        fixture.Resources.Transient.Used.ShouldBe(0);
    }

    [Fact]
    public async Task IndexedProviderRange_DoesNotRequireGeneralStreamSeeking()
    {
        await using var fixture = new Fixture(CacheCompressionAlgorithm.GZip, compress: false);
        var input = new byte[140000];
        Random.Shared.NextBytes(input);
        var identity = fixture.Runtime.Identity("one");
        var fence = (await fixture.Runtime.Access.CallAsync(token => fixture.Provider.CaptureFenceAsync(identity, token), default)).Value;
        var created = await CachePayloadStage.CreateAsync(fixture.Provider, new(identity, fence, default, 1024 * 1024),
            fixture.Binding, fixture.Resources, TimeProvider.System, new("one"), "RAW", 1, default, fixture.Runtime.Access, fixture.Tasks);
        await using (var stage = created.Value)
        {
            await stage.Stream.WriteAsync(input);
            (await stage.CompleteAsync(new(), null, default)).IsSuccess.ShouldBeTrue();
            (await stage.PublishAsync(default)).IsSuccess.ShouldBeTrue();
        }

        await fixture.Provider.ReleaseFenceAsync(identity, fence);
        var acquired = (await fixture.Provider.LookupAsync(identity, new(OpenPayload: true))).Value.Handle;
        var indexed = new ForwardOnlyRangeHandle(acquired);
        indexed.Stream.CanSeek.ShouldBeFalse();
        var capabilities = fixture.Binding.Provider.Capabilities with { SeekableRead = false };
        var binding = fixture.Binding with { Provider = new CacheProviderBinding("memory", fixture.Binding.Provider.NativeOptions,
            new("indexed-test", capabilities, (_, _, _) => throw new InvalidOperationException())) };
        fixture.Resources.ForCollection("items").Transfers.TryAcquire(out var transfer).ShouldBeTrue();

        var opened = await CachePayloadReader.OpenAsync(indexed, new(Offset: 65530, Length: 300), binding, fixture.Resources,
            fixture.Tasks, TimeProvider.System, transfer, default);

        opened.IsSuccess.ShouldBeTrue();
        await using (var handle = opened.Value)
        {
            handle.Stream.CanSeek.ShouldBeFalse();
            using var output = new MemoryStream();
            await handle.Stream.CopyToAsync(output);
            output.ToArray().ShouldBe(input.AsSpan(65530, 300).ToArray());
            (await handle.WaitForCompletionAsync()).Value.ShouldBe(CacheReadCompletion.RangeCompleted);
        }

        indexed.Positions.ShouldBe(1);
        fixture.Resources.Transient.Used.ShouldBe(0);
    }

    [Fact]
    public async Task RejectedValidator_CannotPublishEvenWhenCallerIgnoresFailure()
    {
        await using var fixture = new Fixture(CacheCompressionAlgorithm.GZip);
        var identity = fixture.Runtime.Identity("one");
        var fence = (await fixture.Runtime.Access.CallAsync(token => fixture.Provider.CaptureFenceAsync(identity, token), default)).Value;
        var created = await CachePayloadStage.CreateAsync(fixture.Provider, new(identity, fence, default, 1024 * 1024),
            fixture.Binding, fixture.Resources, TimeProvider.System, new("one"), "RAW", 1, default, fixture.Runtime.Access, fixture.Tasks);
        await using (var sut = created.Value)
        {
            await sut.Stream.WriteAsync(new byte[3]);
            var validation = await sut.CompleteAsync(new(), (_, _) => ValueTask.FromResult(Result.Failure(new CacheError(
                CacheErrorCode.Contract, "Invalid raw contract."))), default);
            validation.IsFailure.ShouldBeTrue();
            (await sut.PublishAsync(default)).IsFailure.ShouldBeTrue();
        }

        await fixture.Provider.ReleaseFenceAsync(identity, fence);
        (await fixture.Runtime.OpenAsync("one", null, default)).Value.Found.ShouldBeFalse();
        fixture.Resources.Transient.Used.ShouldBe(0);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider services;

        internal Fixture(CacheCompressionAlgorithm algorithm, bool compress = true, CompressionLevel level = CompressionLevel.Fastest, bool largeWorkspace = false)
        {
            var services = new ServiceCollection();
            var builder = services.AddApplicationCaching();
            if (largeWorkspace)
            {
                builder.WithOptions(o => o.With(x => x.Limits.MaxResidentBytes, CacheDefaults.GiB).With(x => x.Limits.MaxTransientBytes, CacheDefaults.GiB))
                    .WithProvider("memory", p => p.With(x => x.MaxResidentBytes, CacheDefaults.GiB))
                    .Defaults(c => c.With(x => x.Limits.MaxResidentBytes, CacheDefaults.GiB));
            }

            builder.WithBinaryContract("raw", "raw")
                .WithBinaryCollection("items", c => c.UseContract("raw").With(x => x.Compression.Enabled, compress)
                    .With(x => x.Compression.Algorithm, algorithm).With(x => x.Compression.Level, level));
            this.services = services.BuildServiceProvider();
            var catalog = this.services.GetRequiredService<CacheCatalog>();
            this.Resources = this.services.GetRequiredService<CacheRuntimeResources>();
            this.Provider = this.services.GetRequiredService<CacheProviderRegistry>().Get("memory");
            this.Binding = catalog.Collections["items"];
            this.Tasks = new(catalog);
            this.Runtime = new(this.Binding, this.Provider, catalog, this.Resources, new(catalog), this.Tasks, TimeProvider.System);
        }

        internal CacheCollectionRuntime Runtime { get; }

        internal ICacheStoreProvider Provider { get; }

        internal CacheCollectionBinding Binding { get; }

        internal CacheRuntimeResources Resources { get; }

        internal CacheRuntimeTasks Tasks { get; }

        public async ValueTask DisposeAsync()
        {
            await this.Tasks.DisposeAsync();
            await this.services.DisposeAsync();
        }
    }

    private sealed class ForwardOnlyRangeHandle : ICacheProviderRangeReadHandle
    {
        private readonly ICacheProviderReadHandle inner;

        internal ForwardOnlyRangeHandle(ICacheProviderReadHandle inner)
        {
            this.inner = inner;
            this.Stream = new ForwardOnlyReadStream(inner.Stream);
        }

        internal int Positions { get; private set; }

        public Stream Stream { get; }

        public CacheEntryMetadata Metadata => this.inner.Metadata;

        public ValueTask<Result> PositionForRangeAsync(long storedOffset, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.Positions++;
            this.inner.Stream.Seek(storedOffset, SeekOrigin.Begin);
            return ValueTask.FromResult(Result.Success());
        }

        public ValueTask DisposeAsync() => this.inner.DisposeAsync();
    }
}