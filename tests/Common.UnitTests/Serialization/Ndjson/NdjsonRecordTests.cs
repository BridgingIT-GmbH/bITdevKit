// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using System.Text.Json;
using BridgingIT.DevKit.Testing.Ndjson;

public class NdjsonRecordTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(254)]
    [InlineData(4094)]
    [InlineData(65534)]
    [InlineData(900000)]
    public async Task EncodeRecordAsync_ExactStringByteLimit_AcceptsExactAndRejectsNextByte(int characters)
    {
        var writer = new NdjsonWriter(options: new() { MaxRecordBytes = characters + 2 });
        using var frame = await writer.EncodeRecordAsync(new string('a', characters));
        frame.JsonBytes.Length.ShouldBe(characters + 2);
        frame.FramedBytes.Length.ShouldBe(characters + 3);
        var error = await Should.ThrowAsync<NdjsonStreamException>(() => writer.EncodeRecordAsync(new string('a', characters + 1)));
        error.ReasonCode.ShouldBe("stream_record_too_large");
    }

    [Fact]
    public async Task WriteRecordAsync_OversizedValue_WritesNothing()
    {
        var writer = new NdjsonWriter(options: new() { MaxRecordBytes = 8 });
        using var stream = new NdjsonTestStreams.RecordingStream();
        await Should.ThrowAsync<NdjsonStreamException>(() => writer.WriteRecordAsync(new string('a', 100), stream));
        stream.Bytes.ShouldBeEmpty();
        stream.Disposed.ShouldBeFalse();
    }

    [Fact]
    public async Task WriteEncodedRecordAsync_EncodedOnce_PreservesBytesAndCallerOwnership()
    {
        var converter = new CountingConverter();
        var writer = new NdjsonWriter(new JsonSerializerOptions { Converters = { converter } });
        using var stream = new NdjsonTestStreams.RecordingStream();
        using var frame = await writer.EncodeRecordAsync(new Value());
        var original = frame.FramedBytes.ToArray();
        await writer.WriteEncodedRecordAsync(frame, stream);
        converter.Count.ShouldBe(1);
        stream.Bytes.ShouldBe(original);
        stream.FlushCount.ShouldBe(0);
        frame.FramedBytes.Length.ShouldBe(original.Length);
        frame.Dispose();
        frame.Dispose();
        Should.Throw<ObjectDisposedException>(() => frame.FramedBytes);
        await Should.ThrowAsync<ObjectDisposedException>(() => writer.WriteEncodedRecordAsync(frame, stream));
    }

    [Fact]
    public async Task WriteAsync_NullAndNestedCollections_PreservesRecordBoundaries()
    {
        var writer = new NdjsonWriter();
        using var stream = new NdjsonTestStreams.RecordingStream();
        await writer.WriteAsync(new object[] { null, new[] { 1, 2 }, new Dictionary<string, int> { ["a"] = 1 } }, stream);
        Encoding.UTF8.GetString(stream.Bytes).ShouldBe("null\n[1,2]\n{\"a\":1}\n");
        stream.FlushCount.ShouldBeGreaterThanOrEqualTo(3);
        stream.Disposed.ShouldBeFalse();
    }

    private sealed record Value;
    private sealed class CountingConverter : System.Text.Json.Serialization.JsonConverter<Value>
    {
        internal int Count { get; private set; }

        public override Value Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new();
        public override void Write(Utf8JsonWriter writer, Value value, JsonSerializerOptions options) { this.Count++; writer.WriteRawValue("{\"spelling\":\"\\u0061\"}"); }
    }
}
