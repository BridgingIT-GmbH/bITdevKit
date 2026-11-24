// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using System.IO.Compression;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;

public class CachePayloadTransformTests
{
    /// <summary>A Common-only implementation registers through the existing cache pipeline without implementing a cache interface.</summary>
    [Fact]
    public async Task SharedStreamingTransform_PublishesAndReadsThroughCacheContract()
    {
        var services = new ServiceCollection();
        Func<IServiceProvider, IStreamingPayloadTransform> factory = _ => new SharedTransform();
        services.AddApplicationCaching().WithValueContract<string>("text")
            .WithPayloadTransform("xor", factory)
            .WithBehavior<XorBehavior>("xor", CacheBehaviorStage.PayloadTransformation)
            .WithCollection<string>("items", c => c.With(x => x.Behaviors.PayloadTransformation, new[] { "xor" }));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");

        var written = await sut.SetAsync(new("key"), "shared transform value");
        var read = await sut.GetAsync("key");

        written.IsSuccess.ShouldBeTrue();
        read.IsSuccess.ShouldBeTrue();
        read.Value.Value.ShouldBe("shared transform value");
        read.Value.Version.ShouldBe(written.Value.Version);
    }

    /// <summary>The cache descriptor's shared base preserves existing serialized fields and round-trips.</summary>
    [Fact]
    public void Descriptor_SharedBase_PreservesExistingCacheJson()
    {
        const string json = "{\"FormatId\":\"test-xor\",\"FormatVersion\":1,\"Parameters\":\"Kg==\"}";

        var sut = JsonSerializer.Deserialize<CachePayloadTransformDescriptor>(json);

        sut.FormatId.ShouldBe("test-xor");
        sut.FormatVersion.ShouldBe(1);
        sut.Parameters.ShouldBe("Kg==");
        JsonSerializer.Serialize(sut).ShouldBe(json);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CustomAndCompression_DecodeInReverseOrderWithWholeIntegrity(bool customFirst)
    {
        var transform = new XorTransform();
        var services = new ServiceCollection();
        services.AddApplicationCaching().WithValueContract<string>("text")
            .WithPayloadTransform("xor", _ => transform).WithBehavior<XorBehavior>("xor", CacheBehaviorStage.PayloadTransformation)
            .WithCollection<string>("items", c => c.Compression(CacheCompressionAlgorithm.GZip, CompressionLevel.Fastest)
                .With(x => x.Behaviors.PayloadTransformation, customFirst ? new[] { "xor", "compression" } : new[] { "compression", "xor" }));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");
        var value = new string('a', 100_000) + "é";

        var written = await sut.SetAsync(new("key"), value);
        var read = await sut.GetAsync("key");
        var opened = await sut.OpenReadAsync("key");
        written.IsSuccess.ShouldBeTrue();
        written.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        read.IsSuccess.ShouldBeTrue();
        read.Value.Value.ShouldBe(value);
        read.Value.Version.ShouldBe(written.Value.Version);
        opened.Value.Found.ShouldBeTrue();
        await using var handle = opened.Value.Value;
        handle.Metadata.EnvelopeVersion.ShouldBe(2);
        handle.Metadata.Compression.ShouldBe("stack");
        handle.Stream.CanSeek.ShouldBeFalse();
        await handle.Stream.CopyToAsync(Stream.Null);
        (await handle.WaitForCompletionAsync()).Value.ShouldBe(CacheReadCompletion.Validated);
        transform.Encodes.ShouldBe(1);
        transform.Decodes.ShouldBe(2);
    }

    [Fact]
    public async Task MissingRegisteredReader_IsCompatibilityMissWithoutDeletingForeignRepresentation()
    {
        var native = new InMemoryCacheStoreProvider("store", new());
        await using var writer = Create(native, true);
        await using var reader = Create(native, false);
        var write = writer.GetRequiredService<IApplicationCache>().GetCollection<string>("items");
        var sut = reader.GetRequiredService<IApplicationCache>().GetCollection<string>("items");
        (await write.SetAsync(new("key"), "value")).IsSuccess.ShouldBeTrue();

        var miss = await sut.GetAsync("key");

        miss.IsSuccess.ShouldBeTrue();
        miss.Value.Found.ShouldBeFalse();
        (await write.GetAsync("key")).Value.Value.ShouldBe("value");
    }

    [Fact]
    public async Task UnregisteredTransform_AbortsPublicationAndPreservesPreviousVersion()
    {
        var services = new ServiceCollection();
        var behavior = new SwitchBehavior();
        services.AddSingleton(behavior);
        services.AddApplicationCaching().WithValueContract<string>("text")
            .WithBehavior<SwitchBehavior>("custom", CacheBehaviorStage.PayloadTransformation)
            .WithCollection<string>("items", c => c.With(x => x.Behaviors.PayloadTransformation, new[] { "custom" }));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");
        var original = await sut.SetAsync(new("key"), "old");
        behavior.Transform = true;

        (await sut.SetAsync(new("key"), "new")).IsFailure.ShouldBeTrue();
        var read = await sut.GetAsync("key");
        read.Value.Value.ShouldBe("old");
        read.Value.Version.ShouldBe(original.Value.Version);
    }

    /// <summary>Existing explicit cache implementations also satisfy the shared streaming contract.</summary>
    [Fact]
    public void ExplicitCacheImplementation_SharedContract_ForwardsEveryMember()
    {
        var original = (ICachePayloadTransform)new ExplicitLegacyTransform();
        var sut = (IStreamingPayloadTransform)original;
        using var stream = new MemoryStream();

        sut.FormatId.ShouldBe("explicit-transform");
        sut.FormatVersion.ShouldBe(1);
        sut.MaximumStateBytes.ShouldBe(0);
        sut.CreateEncodingStream(stream, ReadOnlyMemory<byte>.Empty).ShouldBeSameAs(stream);
        sut.CreateDecodingStream(stream, ReadOnlyMemory<byte>.Empty).ShouldBeSameAs(stream);
    }

    private static ServiceProvider Create(InMemoryCacheStoreProvider native, bool transform)
    {
        var services = new ServiceCollection();
        var builder = services.AddApplicationCaching().WithValueContract<string>("text")
            .WithBehavior<XorBehavior>("xor", CacheBehaviorStage.PayloadTransformation)
            .WithProviderKind("shared", InMemoryCacheStoreProvider.DefaultCapabilities, (_, _, _) => native)
            .WithProvider("store", c => c.With(x => x.Kind, "shared"));
        if (transform)
        {
            builder.WithPayloadTransform("xor", _ => new XorTransform());
        }

        builder.WithCollection<string>("items", c =>
        {
            c.UseProvider("store");
            c.With(x => x.Behaviors.PayloadTransformation, new[] { "xor" });
        });
        return services.BuildServiceProvider();
    }

    private sealed class SwitchBehavior : ICachePayloadBehavior
    {
        internal bool Transform { get; set; }

        public async ValueTask<Result> ExecuteAsync(CachePayloadBehaviorContext context, CachePayloadContinuation next, CancellationToken cancellationToken = default) =>
            this.Transform ? await context.ApplyTransformAsync("unregistered", cancellationToken: cancellationToken) : await next(context, cancellationToken);
    }

    private sealed class XorBehavior : ICachePayloadBehavior
    {
        public async ValueTask<Result> ExecuteAsync(CachePayloadBehaviorContext context, CachePayloadContinuation next, CancellationToken cancellationToken = default)
        {
            var applied = await context.ApplyTransformAsync("xor", new byte[] { 42 }, cancellationToken);
            return applied.IsFailure ? applied : await next(context, cancellationToken);
        }
    }

    private sealed class ExplicitLegacyTransform : ICachePayloadTransform
    {
        string ICachePayloadTransform.FormatId => "explicit-transform";

        int ICachePayloadTransform.FormatVersion => 1;

        long ICachePayloadTransform.MaximumStateBytes => 0;

        Stream ICachePayloadTransform.CreateEncodingStream(Stream destination, ReadOnlyMemory<byte> parameters) => destination;

        Stream ICachePayloadTransform.CreateDecodingStream(Stream source, ReadOnlyMemory<byte> parameters) => source;
    }

    private sealed class SharedTransform : IStreamingPayloadTransform
    {
        private readonly XorTransform inner = new();

        public string FormatId => this.inner.FormatId;

        public int FormatVersion => this.inner.FormatVersion;

        public long MaximumStateBytes => this.inner.MaximumStateBytes;

        public Stream CreateEncodingStream(Stream destination, ReadOnlyMemory<byte> parameters) => this.inner.CreateEncodingStream(destination, parameters);

        public Stream CreateDecodingStream(Stream source, ReadOnlyMemory<byte> parameters) => this.inner.CreateDecodingStream(source, parameters);
    }

    // Test-only reversible transform; this is deliberately not an encryption API.
    private sealed class XorTransform : ICachePayloadTransform
    {
        internal int Encodes;
        internal int Decodes;

        public string FormatId => "test-xor";

        public int FormatVersion => 1;

        public long MaximumStateBytes => 65536;

        public Stream CreateEncodingStream(Stream destination, ReadOnlyMemory<byte> parameters)
        {
            Interlocked.Increment(ref this.Encodes);
            return new XorStream(destination, parameters.Span[0]);
        }

        public Stream CreateDecodingStream(Stream source, ReadOnlyMemory<byte> parameters)
        {
            Interlocked.Increment(ref this.Decodes);
            return new XorStream(source, parameters.Span[0]);
        }
    }

    private sealed class XorStream(Stream inner, byte key) : Stream
    {
        public override bool CanRead => inner.CanRead;

        public override bool CanWrite => inner.CanWrite;

        public override bool CanSeek => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken);
            Apply(buffer.Span[..read], key);
            return read;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var copy = buffer.ToArray();
            Apply(copy, key);
            await inner.WriteAsync(copy, cancellationToken);
        }

        private static void Apply(Span<byte> bytes, byte value)
        {
            for (var index = 0; index < bytes.Length; index++) { bytes[index] ^= value; }
        }

        public override void Flush() => inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}