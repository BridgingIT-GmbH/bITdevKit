// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using System.Buffers.Binary;
using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Exercises a list-valued delta codec through real typed cache storage and decoding.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~CacheDeltaCodecTests</code></example>
public sealed class CacheDeltaCodecTests
{
    /// <summary>Retains a complete list as one entry and reconstructs it on a hit without repeating the source.</summary>
    /// <param name="count">The number of measurements, including empty and single-item lists.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(512)]
    public async Task GetOrLoadAsync_ListDeltaCodec_RoundTripsOneEntryAndReusesTheStoredValue(int count)
    {
        // Arrange
        var expected = CreateSamples(count);
        var services = new ServiceCollection();
        services.AddApplicationCaching()
            .WithValueContract<List<Measurement>>("measurements", contract => contract
                .WithCodec("delta", _ => new MeasurementListDeltaCodec())
                .WithDecodedSize(value => value is null ? 0L : 96L + value.Capacity * 16L))
            .WithCollection<List<Measurement>>("delta-series", collection => collection.UseContract("measurements").UseCodec("delta"))
            .WithCollection<List<Measurement>>("json-series", collection => collection.UseContract("measurements"));
        await using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IApplicationCache>();
        var sut = cache.GetCollection<List<Measurement>>("delta-series");
        var sourceCalls = 0;

        // Act
        var loaded = await sut.GetOrLoadAsync(new("sensor:42"), (_, _) =>
        {
            sourceCalls++;
            return ValueTask.FromResult(Result<CacheLoadResult<List<Measurement>>>.Success(new(expected)));
        });
        var hit = await sut.GetOrLoadAsync(new("sensor:42"), (_, _) => throw new InvalidOperationException("A retained series must not reload."));
        var metadata = await sut.PreviewInvalidationAsync(CacheSelection.Key("sensor:42"));
        var json = await cache.GetCollection<List<Measurement>>("json-series").SetAsync(new("sensor:42"), expected);

        // Assert
        loaded.IsSuccess.ShouldBeTrue();
        loaded.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        loaded.Value.Value.ShouldBe(expected);
        hit.IsSuccess.ShouldBeTrue();
        hit.Value.Found.ShouldBeTrue();
        hit.Value.LookupOutcome.ShouldBe(CacheLookupOutcome.Hit);
        hit.Value.Value.ShouldBe(expected);
        hit.Value.Value.ShouldNotBeSameAs(expected);
        sourceCalls.ShouldBe(1);
        metadata.IsSuccess.ShouldBeTrue();
        metadata.Value.Count.ShouldBe(1);
        var retained = metadata.Value.Sample.ShouldHaveSingleItem();
        retained.CodecId.ShouldBe("test.measurement-list.delta");
        retained.CodecVersion.ShouldBe(1);
        retained.Compression.ShouldBe("none");
        json.IsSuccess.ShouldBeTrue();
        if (count > 1)
        {
            retained.LogicalSizeBytes.ShouldBeLessThan(json.Value.Metadata.LogicalSizeBytes / 4);
            retained.StoredSizeBytes.ShouldBeLessThan(json.Value.Metadata.StoredSizeBytes);
        }

        provider.GetRequiredService<CacheRuntimeResources>().Transient.Used.ShouldBe(0);
    }

    /// <summary>Preserves zero and signed deltas on forward-only input while leaving caller streams open.</summary>
    [Fact]
    public async Task DecodeAsync_ForwardOnlySeries_PreservesRepeatedAndDecreasingValues()
    {
        // Arrange
        var expected = new List<Measurement>
        {
            new(1700000000000, -12), new(1700000000000, -12), new(1700000060000, 0),
            new(1700000120000, 32768), new(1700000180000, -32768)
        };
        var sut = new MeasurementListDeltaCodec();
        using var destination = new MemoryStream();

        // Act
        var encoded = await sut.EncodeAsync(expected, destination);
        destination.Position = 0;
        using var source = new ForwardOnlyReadStream(destination, leaveOpen: true);
        var decoded = await sut.DecodeAsync(source);

        // Assert
        encoded.IsSuccess.ShouldBeTrue();
        decoded.IsSuccess.ShouldBeTrue();
        decoded.Value.ShouldBe(expected);
        source.CanSeek.ShouldBeFalse();
        destination.CanRead.ShouldBeTrue();
        destination.CanWrite.ShouldBeTrue();
    }

    /// <summary>Reports a truncated delta as an explicit codec failure without returning a partial list.</summary>
    [Fact]
    public async Task DecodeAsync_TruncatedDelta_ReturnsFailureWithoutPartialValues()
    {
        // Arrange
        var sut = new MeasurementListDeltaCodec();
        using var payload = new MemoryStream();
        (await sut.EncodeAsync(CreateSamples(8), payload)).IsSuccess.ShouldBeTrue();
        payload.SetLength(payload.Length - 1);
        payload.Position = 0;
        using var source = new ForwardOnlyReadStream(payload, leaveOpen: true);

        // Act
        var result = await sut.DecodeAsync(source);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<ValueCodecError>().ShouldHaveSingleItem().Code.ShouldBe("InvalidDeltaList");
        payload.CanRead.ShouldBeTrue();
    }

    /// <summary>Rejects an oversized declared list before allocating its backing array.</summary>
    [Fact]
    public async Task DecodeAsync_OversizedItemCount_ReturnsBoundedFailure()
    {
        // Arrange
        var sut = new MeasurementListDeltaCodec();
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, int.MaxValue);
        using var source = new MemoryStream(header);

        // Act
        var result = await sut.DecodeAsync(source);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<ValueCodecError>().ShouldHaveSingleItem().Code.ShouldBe("InvalidDeltaList");
    }

    /// <summary>Honors cancellation before either codec operation consumes or writes bytes.</summary>
    [Fact]
    public async Task Codec_CancelledOperations_PropagateCancellationWithoutIo()
    {
        // Arrange
        var sut = new MeasurementListDeltaCodec();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var stream = new MemoryStream(new byte[4]);

        // Act / Assert
        await Should.ThrowAsync<OperationCanceledException>(async () => await sut.EncodeAsync(CreateSamples(1), stream, cancellation.Token));
        await Should.ThrowAsync<OperationCanceledException>(async () => await sut.DecodeAsync(stream, cancellation.Token));
        stream.Position.ShouldBe(0);
        stream.Length.ShouldBe(4);
    }

    private static List<Measurement> CreateSamples(int count) => Enumerable.Range(0, count)
        .Select(index => new Measurement(1700000000000L + index / 2 * 60000L, index / 3 % 7 - 3)).ToList();

    /// <summary>Contains an exact timestamp and scaled integer reading for this lossless codec example.</summary>
    /// <param name="Timestamp">Unix time in milliseconds.</param>
    /// <param name="Value">A signed reading in application-defined fixed units.</param>
    /// <example><code>var measurement = new Measurement(1700000000000, -12);</code></example>
    public readonly record struct Measurement(long Timestamp, long Value);

    /// <summary>Demonstrates a bounded format using an absolute first item and ZigZag variable-length signed differences.</summary>
    /// <remarks>This fixture is test-only. Signed differences and reconstructed values must fit Int64.</remarks>
    private sealed class MeasurementListDeltaCodec : IValueCodec<List<Measurement>>
    {
        private const int MaximumItems = 4096;

        /// <inheritdoc />
        public string FormatId => "test.measurement-list.delta";

        /// <inheritdoc />
        public int FormatVersion => 1;

        /// <inheritdoc />
        public ValueCodecCapabilities Capabilities { get; } = new(true, true, false, true, true, MaximumScratchBytes: 20);

        /// <inheritdoc />
        public async ValueTask<Result> EncodeAsync(List<Measurement> value, Stream destination, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value is null || value.Count > MaximumItems || !destination.CanWrite)
            {
                return Result.Failure(new ValueCodecError("InvalidDeltaList", "The list or destination is invalid for this test format."));
            }

            try
            {
                var buffer = new byte[20];
                BinaryPrimitives.WriteInt32LittleEndian(buffer, value.Count);
                if (value.Count > 0)
                {
                    BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(4), value[0].Timestamp);
                    BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(12), value[0].Value);
                }

                await destination.WriteAsync(buffer.AsMemory(0, value.Count == 0 ? 4 : 20), cancellationToken);
                for (var index = 1; index < value.Count; index++)
                {
                    var length = WriteDelta(buffer, 0, checked(value[index].Timestamp - value[index - 1].Timestamp));
                    length += WriteDelta(buffer, length, checked(value[index].Value - value[index - 1].Value));
                    await destination.WriteAsync(buffer.AsMemory(0, length), cancellationToken);
                }

                return Result.Success();
            }
            catch (OverflowException)
            {
                return Result.Failure(new ValueCodecError("InvalidDeltaList", "A signed difference exceeds this test format's range."));
            }
        }

        /// <inheritdoc />
        public async ValueTask<Result<List<Measurement>>> DecodeAsync(Stream source, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!source.CanRead)
            {
                return Result<List<Measurement>>.Failure(new ValueCodecError("InvalidDeltaList", "The source is not readable."));
            }

            try
            {
                var buffer = new byte[20];
                await source.ReadExactlyAsync(buffer.AsMemory(0, 4), cancellationToken);
                var count = BinaryPrimitives.ReadInt32LittleEndian(buffer);
                if (count is < 0 or > MaximumItems) { throw new InvalidDataException("The item count exceeds the format bound."); }

                var result = new List<Measurement>(count);
                if (count > 0)
                {
                    await source.ReadExactlyAsync(buffer.AsMemory(0, 16), cancellationToken);
                    result.Add(new(BinaryPrimitives.ReadInt64LittleEndian(buffer), BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(8))));
                }

                for (var index = 1; index < count; index++)
                {
                    var timestamp = checked(result[^1].Timestamp + await ReadDeltaAsync(source, buffer, cancellationToken));
                    var value = checked(result[^1].Value + await ReadDeltaAsync(source, buffer, cancellationToken));
                    result.Add(new(timestamp, value));
                }

                if (await source.ReadAsync(buffer.AsMemory(0, 1), cancellationToken) != 0) { throw new InvalidDataException("Unexpected trailing bytes."); }

                return Result<List<Measurement>>.Success(result);
            }
            catch (Exception exception) when (exception is EndOfStreamException or InvalidDataException or OverflowException)
            {
                return Result<List<Measurement>>.Failure(new ValueCodecError("InvalidDeltaList", "The encoded list is incomplete or invalid."));
            }
        }

        private static int WriteDelta(byte[] buffer, int offset, long delta)
        {
            var value = unchecked((ulong)((delta << 1) ^ (delta >> 63)));
            var start = offset;
            while (value >= 128)
            {
                buffer[offset++] = (byte)((value & 127) | 128);
                value >>= 7;
            }

            buffer[offset++] = (byte)value;
            return offset - start;
        }

        private static async ValueTask<long> ReadDeltaAsync(Stream source, byte[] buffer, CancellationToken cancellationToken)
        {
            ulong value = 0;
            for (var shift = 0; shift < 64; shift += 7)
            {
                await source.ReadExactlyAsync(buffer.AsMemory(0, 1), cancellationToken);
                var next = buffer[0];
                if (shift == 63 && next > 1) { throw new InvalidDataException("The delta exceeds Int64."); }

                value |= (ulong)(next & 127) << shift;
                if ((next & 128) == 0) { return unchecked((long)(value >> 1) ^ -((long)value & 1)); }
            }

            throw new InvalidDataException("The delta is not terminated.");
        }
    }
}