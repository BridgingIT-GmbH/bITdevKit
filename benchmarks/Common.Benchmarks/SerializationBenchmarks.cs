// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Benchmarks;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BridgingIT.DevKit.Common;

/// <summary>Compares the DevKit serializers using warm mappings and shared payloads.</summary>
/// <example><code>dotnet run -c Release --project benchmarks/Common.Benchmarks -- --filter '*SerializationBenchmarks*'</code></example>
[MemoryDiagnoser]
[Config(typeof(SerializationBenchmarkConfig))]
public class SerializationBenchmarks
{
    private ISerializer serializer;
    private SerializationBenchmarkModel data;
    private byte[] encoded;

    /// <summary>Gets or sets the serializer implementation.</summary>
    /// <example><code>benchmarks.Serializer = "Cbor";</code></example>
    [Params("Cbor", "MessagePack", "SystemTextJson", "JsonNet", "CompressedJsonNet")]
    public string Serializer { get; set; }

    /// <summary>Gets or sets the payload shape.</summary>
    /// <example><code>benchmarks.Payload = "Nested";</code></example>
    [Params("Small", "Nested", "Binary64KiB")]
    public string Payload { get; set; }

    /// <summary>Creates inputs, warms mappings, and validates a round trip outside measurements.</summary>
    /// <example><code>benchmarks.Setup();</code></example>
    [GlobalSetup]
    public void Setup()
    {
        this.serializer = CreateSerializer(this.Serializer);
        this.data = CreatePayload(this.Payload);
        this.encoded = this.serializer.SerializeToBytes(this.data);
        Validate(this.data, this.serializer.Deserialize<SerializationBenchmarkModel>(this.encoded));
    }

    /// <summary>Measures serialization through the shared byte-array adapter.</summary>
    /// <returns>The complete encoded payload.</returns>
    /// <example><code>var bytes = benchmarks.Serialize();</code></example>
    [Benchmark]
    public byte[] Serialize() => this.serializer.SerializeToBytes(this.data);

    /// <summary>Measures typed deserialization from a pre-encoded payload.</summary>
    /// <returns>The reconstructed object.</returns>
    /// <example><code>var value = benchmarks.Deserialize();</code></example>
    [Benchmark]
    public SerializationBenchmarkModel Deserialize() => this.serializer.Deserialize<SerializationBenchmarkModel>(this.encoded);

    /// <summary>Measures serialization followed by typed deserialization.</summary>
    /// <returns>The reconstructed object.</returns>
    /// <example><code>var value = benchmarks.RoundTrip();</code></example>
    [Benchmark]
    public SerializationBenchmarkModel RoundTrip() => this.serializer.Deserialize<SerializationBenchmarkModel>(this.serializer.SerializeToBytes(this.data));

    internal static ISerializer CreateSerializer(string name) => name switch
    {
        "Cbor" => new CborSerializer(),
        "MessagePack" => new MessagePackSerializer(),
        "SystemTextJson" => new SystemTextJsonSerializer(),
        "JsonNet" => new JsonNetSerializer(),
        "CompressedJsonNet" => new CompressionSerializer(new JsonNetSerializer()),
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    internal static SerializationBenchmarkModel CreatePayload(string name)
    {
        var value = new SerializationBenchmarkModel
        {
            Id = 42,
            Name = "serialization benchmark",
            Enabled = true,
            Amount = 123.45m,
            CreatedAt = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero)
        };
        switch (name)
        {
            case "Small":
                break;
            case "Nested":
                value.Values = Enumerable.Range(0, 32).ToList();
                value.Labels = Enumerable.Range(0, 8).ToDictionary(i => $"label{i}", i => $"value{i}");
                value.Child = new SerializationBenchmarkModel
                {
                    Id = 43, Name = "child", Values = [1, 2, 3],
                    Child = new SerializationBenchmarkModel { Id = 44, Name = "grandchild", Data = [1, 2, 3] }
                };
                break;
            case "Binary64KiB":
                value.Data = new byte[64 * 1024];
                new Random(42).NextBytes(value.Data);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(name));
        }

        return value;
    }

    private static void Validate(SerializationBenchmarkModel expected, SerializationBenchmarkModel actual)
    {
        if (expected is null && actual is null)
        {
            return;
        }

        if (expected is null || actual is null || actual.Id != expected.Id || actual.Name != expected.Name
            || actual.Enabled != expected.Enabled || actual.Amount != expected.Amount || actual.CreatedAt != expected.CreatedAt
            || !actual.Data.SequenceEqual(expected.Data) || !actual.Values.SequenceEqual(expected.Values)
            || actual.Labels.Count != expected.Labels.Count || expected.Labels.Any(p => !actual.Labels.TryGetValue(p.Key, out var v) || v != p.Value))
        {
            throw new InvalidOperationException("The benchmark serializer did not preserve its input.");
        }

        Validate(expected.Child, actual.Child);
    }

    /// <summary>Adds encoded payload size to the BenchmarkDotNet reports.</summary>
    /// <example><code>var config = new SerializationBenchmarkConfig();</code></example>
    public sealed class SerializationBenchmarkConfig : ManualConfig
    {
        /// <summary>Initializes the report columns.</summary>
        /// <example><code>var config = new SerializationBenchmarkConfig();</code></example>
        public SerializationBenchmarkConfig() => this.AddColumn(new PayloadSizeColumn());
    }

    private sealed class PayloadSizeColumn : IColumn
    {
        public string Id => nameof(PayloadSizeColumn);
        public string ColumnName => "Payload bytes";
        public bool AlwaysShow => true;
        public ColumnCategory Category => ColumnCategory.Custom;
        public int PriorityInCategory => 0;
        public bool IsNumeric => true;
        public UnitType UnitType => UnitType.Size;
        public string Legend => "Complete serialized payload size in bytes.";
        public bool IsAvailable(Summary summary) => true;
        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
        public string GetValue(Summary summary, BenchmarkCase benchmarkCase) => this.GetValue(summary, benchmarkCase, summary.Style);
        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
        {
            var serializer = (string)benchmarkCase.Parameters.Items.Single(p => p.Name == nameof(Serializer)).Value;
            var payload = (string)benchmarkCase.Parameters.Items.Single(p => p.Name == nameof(Payload)).Value;
            return CreateSerializer(serializer).SerializeToBytes(CreatePayload(payload)).Length.ToString(CultureInfo.InvariantCulture);
        }
    }
}

/// <summary>Shared concrete payload used by all serializer benchmark cases.</summary>
/// <example><code>var value = new SerializationBenchmarkModel { Id = 42, Name = "fixture" };</code></example>
public class SerializationBenchmarkModel
{
    /// <summary>Gets or sets the Id payload member.</summary>
    /// <example><code>value.Id = 42;</code></example>
    public int Id { get; set; }

    /// <summary>Gets or sets the Name payload member.</summary>
    /// <example><code>value.Name = "fixture";</code></example>
    public string Name { get; set; }

    /// <summary>Gets or sets the Enabled payload member.</summary>
    /// <example><code>value.Enabled = true;</code></example>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the Amount payload member.</summary>
    /// <example><code>value.Amount = 123.45m;</code></example>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets the CreatedAt payload member.</summary>
    /// <example><code>value.CreatedAt = DateTimeOffset.UnixEpoch;</code></example>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the Values payload member.</summary>
    /// <example><code>value.Values = [1, 2, 3];</code></example>
    public List<int> Values { get; set; } = [];

    /// <summary>Gets or sets the Labels payload member.</summary>
    /// <example><code>value.Labels = new() { ["key"] = "value" };</code></example>
    public Dictionary<string, string> Labels { get; set; } = [];

    /// <summary>Gets or sets the Data payload member.</summary>
    /// <example><code>value.Data = [1, 2, 3];</code></example>
    public byte[] Data { get; set; } = [];

    /// <summary>Gets or sets the Child payload member.</summary>
    /// <example><code>value.Child = new SerializationBenchmarkModel();</code></example>
    public SerializationBenchmarkModel Child { get; set; }

}