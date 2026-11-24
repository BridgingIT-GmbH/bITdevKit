// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Benchmarks;

using System.Text.Json;
using BenchmarkDotNet.Attributes;

/// <summary>Measures cache-independent JSON codec work, its existing serializer adapter, and explicit materialization.</summary>
/// <remarks>All successful decodes validate the declared value. Stream allocation is included equally in the encode paths.</remarks>
/// <example><code>dotnet run -c Release --project benchmarks/Common.Benchmarks -- --filter *ValueCodecBenchmarks*</code></example>
[MemoryDiagnoser]
public class ValueCodecBenchmarks
{
    private readonly SystemTextJsonValueCodec<CodecValue> codec = new();
    private readonly JsonSerializerOptions options = DefaultJsonSerializerOptions.Create();
    private readonly SerializerValueCodec<CodecValue> adapter = new(new SystemTextJsonSerializer(), new("json", 1));
    private CodecValue value;
    private byte[] bytes;

    /// <summary>Gets or sets the approximate logical string payload size, independent of JSON envelope bytes.</summary>
    [Params(256, 4096, 65536)]
    public int PayloadBytes { get; set; }

    /// <summary>Builds an immutable input and verifies codec round-trip outside the measured interval.</summary>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        this.value = new(42, new string('x', this.PayloadBytes));
        using var output = new MemoryStream();
        Ensure(await this.codec.EncodeAsync(this.value, output));
        this.bytes = output.ToArray();
        using var input = new MemoryStream(this.bytes, writable: false);
        if ((await this.codec.DecodeAsync(input)).Value != this.value) { throw new InvalidOperationException("The benchmark seed did not round-trip."); }
    }

    /// <summary>Measures direct BCL UTF-8 materialization as a source-side allocation baseline.</summary>
    [Benchmark(Baseline = true)]
    public byte[] MaterializeJson() => JsonSerializer.SerializeToUtf8Bytes(this.value, this.options);

    /// <summary>Measures asynchronous encoding to a caller-owned stream without a second byte-array copy.</summary>
    [Benchmark]
    public async Task<long> EncodeForwardAsync()
    {
        using var output = new MemoryStream();
        Ensure(await this.codec.EncodeAsync(this.value, output));
        return output.Length;
    }

    /// <summary>Measures complete JSON decoding from a genuinely nonseekable source.</summary>
    [Benchmark]
    public async Task<int> DecodeForwardAsync()
    {
        using var input = new ForwardInput(this.bytes);
        var result = await this.codec.DecodeAsync(input);
        Ensure(result);
        if (result.Value.Id != 42 || result.Value.Text.Length != this.PayloadBytes) { throw new InvalidDataException("Decoded work was incomplete."); }

        return result.Value.Text.Length;
    }

    /// <summary>Measures adaptation of the existing seek-dependent synchronous serializer on its supported input.</summary>
    [Benchmark]
    public async Task<int> DecodeSerializerAdapterAsync()
    {
        using var input = new MemoryStream(this.bytes, writable: false);
        var result = await this.adapter.DecodeAsync(input);
        Ensure(result);
        return result.Value.Text.Length;
    }

    /// <summary>Measures pre-cancelled admission separately; it performs no encode or decode work.</summary>
    [Benchmark]
    public async Task<bool> RejectPreCancelledAsync()
    {
        try
        {
            await this.codec.EncodeAsync(this.value, Stream.Null, new CancellationToken(canceled: true));
            throw new InvalidOperationException("Cancelled codec work was unexpectedly admitted.");
        }
        catch (OperationCanceledException) { return true; }
    }

    private static void Ensure(IResult result)
    {
        if (result.IsFailure) { throw new InvalidDataException(string.Join("; ", result.Errors.Select(error => error.Message))); }
    }

    /// <summary>Represents an immutable codec workload without domain or cache dependencies.</summary>
    /// <param name="Id">The round-trip integrity sentinel.</param>
    /// <param name="Text">The parameterized logical payload.</param>
    public sealed record CodecValue(int Id, string Text);

    private sealed class ForwardInput(byte[] payload) : Stream
    {
        private readonly MemoryStream input = new(payload, writable: false);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => this.input.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => this.input.ReadAsync(buffer, cancellationToken);

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) { this.input.Dispose(); }

            base.Dispose(disposing);
        }
    }
}
