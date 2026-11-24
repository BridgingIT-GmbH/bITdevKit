// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using System.IO.Compression;
using System.Security.Cryptography;

public class CacheEnvelopeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(65536)]
    [InlineData(140000)]
    public async Task Frames_RoundTripAndWholeIntegrity(int length)
    {
        var input = new byte[length];
        Random.Shared.NextBytes(input);
        var (stored, metadata) = await EncodeAsync(input);
        var budget = new ResourceBudget("test", 1024 * 1024);
        using var raw = new MemoryStream(stored);
        using var sut = (await CacheEnvelopeReader.OpenAsync(raw, metadata, budget, budget, default)).Value;
        using var output = new MemoryStream();

        await sut.CopyToAsync(output);

        output.ToArray().ShouldBe(input);
        (await sut.VerifyAsync(false, default)).IsSuccess.ShouldBeTrue();
        sut.Dispose();
        budget.Used.ShouldBe(0);
    }

    [Fact]
    public async Task CorruptedFrame_NeverDeliversItsBytes()
    {
        var (stored, metadata) = await EncodeAsync(new byte[100]);
        stored[^300] ^= 0x42; // Mutate frame/body/footer depending on header size, all must fail validation.
        var budget = new ResourceBudget("test", 1024 * 1024);
        using var raw = new MemoryStream(stored);
        var opened = await CacheEnvelopeReader.OpenAsync(raw, metadata, budget, budget, default);
        if (opened.IsSuccess)
        {
            using var sut = opened.Value;
            var output = new byte[100];
            await Should.ThrowAsync<InvalidDataException>(async () =>
            {
                while (await sut.ReadAsync(output) != 0) { }
            });
        }
        else
        {
            opened.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Corruption);
        }
    }

    [Fact]
    public async Task Seek_ValidatesAffectedFramesWithoutClaimingWholeIntegrity()
    {
        var input = new byte[140000];
        Random.Shared.NextBytes(input);
        var (stored, metadata) = await EncodeAsync(input);
        var budget = new ResourceBudget("test", 1024 * 1024);
        using var raw = new MemoryStream(stored);
        using var sut = (await CacheEnvelopeReader.OpenAsync(raw, metadata, budget, budget, default)).Value;

        sut.Seek(70000, SeekOrigin.Begin).ShouldBe(70000);
        var actual = new byte[123];
        await sut.ReadExactlyAsync(actual);

        actual.ShouldBe(input.AsSpan(70000, 123).ToArray());
        (await sut.VerifyAsync(true, default)).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("gzip")]
    [InlineData("brotli")]
    public async Task CompressedFrame_ReadsWithoutWritePolicyAndRejectsSeek(string algorithm)
    {
        var input = new byte[140000];
        var (stored, metadata) = await EncodeAsync(input, algorithm);
        var budget = new ResourceBudget("test", 1024 * 1024);
        using var raw = new MemoryStream(stored);
        using var sut = (await CacheEnvelopeReader.OpenAsync(raw, metadata, budget, budget, default)).Value;
        await using Stream decoded = algorithm == "gzip" ? new GZipStream(sut, CompressionMode.Decompress, true) :
            new BrotliStream(sut, CompressionMode.Decompress, true);
        using var output = new MemoryStream();

        await decoded.CopyToAsync(output);

        output.ToArray().ShouldBe(input);
        sut.CanSeek.ShouldBeFalse();
        (await sut.VerifyAsync(false, default)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task UnknownEnvelope_IsCompatibilityFailureBeforeReading()
    {
        var budget = new ResourceBudget("test", 1024 * 1024);
        using var raw = new MemoryStream();

        var result = await CacheEnvelopeReader.OpenAsync(raw, new() { EnvelopeVersion = 2 }, budget, budget, default);

        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Compatibility);
        raw.Position.ShouldBe(0);
        budget.Used.ShouldBe(0);
    }

    [Fact]
    public async Task OversizeCaughtByCallback_StillPoisonsWrite()
    {
        using var destination = new MemoryStream();
        using var sut = new LimitedWriteStream(destination, 4);
        await sut.WriteAsync(new byte[4]);

        await Should.ThrowAsync<StreamSizeLimitExceededException>(async () => await sut.WriteAsync(new byte[1]));

        sut.CompletedSuccessfully.ShouldBeFalse();
        destination.Length.ShouldBe(4);
    }

    private static async Task<(byte[] Stored, CacheEntryMetadata Metadata)> EncodeAsync(byte[] input, string compression = "none")
    {
        var identity = new CacheEntryIdentity("test", "items", "1", "a");
        var version = CacheEntryVersion.New();
        var header = new CacheEntryEnvelope { IdentityDigest = identity.Digest, EntryVersion = version.Value,
            ContractId = "TEST", CodecId = "RAW", Compression = compression };
        var budget = new ResourceBudget("test", 1024 * 1024);
        using var raw = new MemoryStream();
        using var writer = (await CacheEnvelopeWriter.CreateAsync(raw, header, budget, budget, default)).Value;
        if (compression == "none")
        {
            await writer.WriteAsync(input);
        }
        else
        {
            await using Stream encoder = compression == "gzip" ? new GZipStream(writer, CompressionLevel.Fastest, true) :
                new BrotliStream(writer, CompressionLevel.Fastest, true);
            await encoder.WriteAsync(input);
        }

        var result = await writer.CompleteAsync(default, input.Length, Convert.ToHexStringLower(SHA256.HashData(input)));
        return (raw.ToArray(), new() { Identity = identity, Version = version, ContractId = "TEST", CodecId = "RAW",
            Compression = compression, LogicalSizeBytes = input.Length, EncodedSizeBytes = input.Length,
            StoredSizeBytes = result.StoredBytes, StoredHash = result.StoredHash, PayloadHash = result.LogicalHash });
    }
}