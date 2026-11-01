// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

/// <summary>Measures warm, validated NDJSON record processing with fictional stock data.</summary>
/// <example><code>dotnet run -c Release --project benchmarks/Common.Benchmarks -- --filter '*NdjsonRecordBenchmarks*'</code></example>
[MemoryDiagnoser]
[ShortRunJob]
public class NdjsonRecordBenchmarks
{
    private readonly NdjsonWriter writer = new();
    private readonly NdjsonReader reader = new();
    private StockRecord value;
    private NdjsonEnvelope<StockRecord> envelope;
    private byte[] input;

    /// <summary>Gets or sets the record shape, including JSON escaping and large buffers.</summary>
    /// <example><code>benchmarks.Payload = "Small";</code></example>
    [Params("Small", "Escaped", "Large64KiB")]
    public string Payload { get; set; }

    /// <summary>Builds 128 input frames and validates them outside measured operations.</summary>
    /// <example><code>await benchmarks.SetupAsync();</code></example>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        this.value = new StockRecord(
            1,
            this.Payload switch
            {
                "Small" => new string('a', 48),
                "Escaped" => string.Concat(Enumerable.Repeat("工具\"\n", 16)),
                "Large64KiB" => new string('a', 65500),
                _ => throw new InvalidOperationException("Unknown payload."),
            },
            12
        );
        this.envelope = new NdjsonEnvelope<StockRecord>
        {
            Kind = NdjsonEnvelopeKind.Data,
            Data = this.value,
        };
        using var frame = await this.writer.EncodeRecordAsync(this.value);
        using var source = new MemoryStream();
        for (var index = 0; index < 128; index++)
        {
            source.Write(frame.FramedBytes.Span);
        }

        this.input = source.ToArray();
        source.Position = 0;
        var count = 0;
        await foreach (var record in this.reader.ReadAsync<StockRecord>(source))
        {
            if (record != this.value)
            {
                throw new InvalidOperationException("The benchmark changed its input.");
            }

            count++;
        }

        if (count != 128)
        {
            throw new InvalidOperationException("The benchmark lost records.");
        }
    }

    /// <summary>Includes encoding, validation, pooled-buffer clearing, and disposal.</summary>
    /// <example><code>var bytes = await benchmarks.EncodeRawAsync();</code></example>
    [Benchmark]
    public async Task<int> EncodeRawAsync()
    {
        using var frame = await this.writer.EncodeRecordAsync(this.value);
        return frame.FramedBytes.Length;
    }

    /// <summary>Measures the additional fixed-envelope encoding work.</summary>
    /// <example><code>var bytes = await benchmarks.EncodeEnvelopeAsync();</code></example>
    [Benchmark]
    public async Task<int> EncodeEnvelopeAsync()
    {
        using var frame = await this.writer.EncodeRecordAsync(this.envelope);
        return frame.FramedBytes.Length;
    }

    /// <summary>Measures framing and validation without materializing payload models.</summary>
    /// <example><code>var bytes = await benchmarks.ReadBorrowedAsync();</code></example>
    [Benchmark(OperationsPerInvoke = 128)]
    public async Task<long> ReadBorrowedAsync()
    {
        using var source = new MemoryStream(this.input, writable: false);
        long bytes = 0;
        await foreach (var record in this.reader.ReadRecordsAsync(source))
        {
            bytes += record.FramedBytes.Length;
        }

        return bytes;
    }

    /// <summary>Measures framing, validation, and typed deserialization.</summary>
    /// <example><code>var quantity = await benchmarks.ReadTypedAsync();</code></example>
    [Benchmark(OperationsPerInvoke = 128)]
    public async Task<long> ReadTypedAsync()
    {
        using var source = new MemoryStream(this.input, writable: false);
        long quantity = 0;
        await foreach (var record in this.reader.ReadAsync<StockRecord>(source))
        {
            quantity += record.Quantity;
        }

        return quantity;
    }

    /// <summary>A fictional inventory value shared by every record-processing case.</summary>
    /// <param name="Sequence">Stable source position.</param>
    /// <param name="Description">Payload text.</param>
    /// <param name="Quantity">Available stock.</param>
    /// <example><code>var record = new StockRecord(1, "Widget", 12);</code></example>
    public sealed record StockRecord(int Sequence, string Description, int Quantity);
}
