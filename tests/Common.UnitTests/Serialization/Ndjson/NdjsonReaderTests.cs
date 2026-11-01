// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using BridgingIT.DevKit.Testing.Ndjson;

public class NdjsonReaderTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4096)]
    public async Task ReadRecordsAsync_FragmentedOriginalBytes_PreservesSpellingAndLeaseLifetime(int fragmentSize)
    {
        using var stream = new NdjsonTestStreams.FragmentedReadStream(Encoding.UTF8.GetBytes("  {\"a\":\"\\u0061\"} \r\n\"☀😀\"\n"), fragmentSize);
        var reader = new NdjsonReader();
        await using var iterator = reader.ReadRecordsAsync(stream).GetAsyncEnumerator();
        (await iterator.MoveNextAsync()).ShouldBeTrue();
        var first = iterator.Current;
        Encoding.UTF8.GetString(first.FramedBytes.Span).ShouldBe("  {\"a\":\"\\u0061\"} \r\n");
        first.DelimiterBytes.ToArray().ShouldBe(new byte[] { 13, 10 });
        reader.Deserialize<Dictionary<string, string>>(first)["a"].ShouldBe("a");
        (await iterator.MoveNextAsync()).ShouldBeTrue();
        Should.Throw<ObjectDisposedException>(() => first.JsonBytes);
        reader.Deserialize<string>(iterator.Current).ShouldBe("☀😀");
        (await iterator.MoveNextAsync()).ShouldBeFalse();
        stream.Disposed.ShouldBeFalse();
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("1 2\n")]
    [InlineData("{bad}\n")]
    [InlineData("1")]
    [InlineData("\uFEFF1\n")]
    [InlineData("[1,]\n")]
    [InlineData("/*comment*/1\n")]
    public async Task ReadRecordsAsync_InvalidFraming_RejectsInput(string input)
    {
        using var stream = new NdjsonTestStreams.FragmentedReadStream(Encoding.UTF8.GetBytes(input));
        var error = await Should.ThrowAsync<NdjsonStreamException>(async () => { await foreach (var unused in new NdjsonReader().ReadRecordsAsync(stream)) { } });
        error.ReasonCode.ShouldBe("stream_invalid_record");
        stream.Disposed.ShouldBeFalse();
    }

    [Fact]
    public async Task ReadRecordsAsync_Compatibility_ReportsActualEmptyFinalDelimiter()
    {
        using var stream = new NdjsonTestStreams.FragmentedReadStream("\n1\r\n2"u8.ToArray());
        var reader = new NdjsonReader(options: new() { MaxRecordBytes = 1, SkipEmptyLines = true, AllowUnterminatedFinalRecord = true });
        var delimiters = new List<int>();
        await foreach (var record in reader.ReadRecordsAsync(stream)) { delimiters.Add(record.DelimiterBytes.Length); }

        delimiters.ShouldBe(new[] { 2, 0 });
    }

    [Fact]
    public async Task ReadRecordsAsync_InvalidUtf8AndLimit_RejectsWithoutDecoderReplacement()
    {
        using var invalid = new NdjsonTestStreams.FragmentedReadStream(new byte[] { 34, 0xc0, 0x80, 34, 10 });
        await Should.ThrowAsync<NdjsonStreamException>(async () => { await foreach (var unused in new NdjsonReader().ReadRecordsAsync(invalid)) { } });
        using var huge = new NdjsonTestStreams.FragmentedReadStream("1234\n"u8.ToArray());
        var error = await Should.ThrowAsync<NdjsonStreamException>(async () => { await foreach (var unused in new NdjsonReader(options: new() { MaxRecordBytes = 3 }).ReadRecordsAsync(huge)) { } });
        error.ReasonCode.ShouldBe("stream_record_too_large");
    }

    [Theory]
    [InlineData(0, "\n")]
    [InlineData(254, "\n")]
    [InlineData(254, "\r\n")]
    [InlineData(255, "\r\n")]
    [InlineData(4094, "\n")]
    [InlineData(65534, "\r\n")]
    public async Task ReadRecordsAsync_FragmentedExactLimitAcrossPoolBuckets_PreservesFrameAndRejectsNextByte(int characters, string delimiter)
    {
        // Arrange
        var value = new string('a', characters);
        var bytes = Encoding.UTF8.GetBytes($"\"{value}\"{delimiter}");
        using var source = new NdjsonTestStreams.FragmentedReadStream(bytes, 17);
        var sut = new NdjsonReader(options: new() { MaxRecordBytes = characters + 2 });
        var count = 0;

        // Act and assert
        await foreach (var frame in sut.ReadRecordsAsync(source))
        {
            frame.FramedBytes.ToArray().ShouldBe(bytes);
            sut.Deserialize<string>(frame).ShouldBe(value);
            count++;
        }

        count.ShouldBe(1);
        using var oversized = new NdjsonTestStreams.FragmentedReadStream(Encoding.UTF8.GetBytes($"\"{value}a\"{delimiter}"), 17);
        var error = await Should.ThrowAsync<NdjsonStreamException>(async () =>
        {
            await foreach (var unused in sut.ReadRecordsAsync(oversized)) { }
        });
        error.ReasonCode.ShouldBe("stream_record_too_large");
    }
}
