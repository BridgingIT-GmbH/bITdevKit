// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using BridgingIT.DevKit.Testing.Ndjson;

public class NdjsonEnvelopeTests
{
    private const string Heartbeat = "{\"version\":1,\"kind\":\"heartbeat\"}\n";

    [Fact]
    public async Task ReadAsync_TerminalError_ThrowsOnSamePullWithoutYieldingRemoteMessage()
    {
        using var stream = new NdjsonTestStreams.FragmentedReadStream(Encoding.UTF8.GetBytes(Heartbeat + "{\"version\":1,\"kind\":\"error\",\"error\":{\"code\":\"example_failed\",\"message\":\"private remote text\",\"traceId\":\"trace-1\"}}\n"), 4096, true);
        await using var reader = new NdjsonEnvelopeReader().ReadAsync<int>(stream).GetAsyncEnumerator();
        (await reader.MoveNextAsync()).ShouldBeTrue();
        reader.Current.Kind.ShouldBe(NdjsonEnvelopeKind.Heartbeat);
        var error = await Should.ThrowAsync<NdjsonStreamException>(() => reader.MoveNextAsync().AsTask());
        error.ReasonCode.ShouldBe("stream_terminal_error");
        error.RemoteErrorCode.ShouldBe("example_failed");
        error.TraceId.ShouldBe("trace-1");
        error.Message.ShouldBe("NDJSON stream terminated with an error.");
    }

    [Fact]
    public async Task ReadDataAsync_CompletionWithoutEof_EndsAfterValidatedCount()
    {
        using var stream = new NdjsonTestStreams.FragmentedReadStream(Encoding.UTF8.GetBytes(Heartbeat + "{\"version\":1,\"kind\":\"data\",\"data\":null}\n{\"version\":1,\"kind\":\"completed\",\"recordCount\":1}\n"), 4096, true);
        var values = new List<string>();
        await foreach (var value in new NdjsonEnvelopeReader().ReadDataAsync<string>(stream)) { values.Add(value); }

        values.Count.ShouldBe(1);
        values[0].ShouldBeNull();
        stream.Disposed.ShouldBeFalse();
    }

    [Theory]
    [InlineData("{\"version\":2,\"kind\":\"heartbeat\"}\n")]
    [InlineData("{\"version\":1,\"kind\":\"unknown\"}\n")]
    [InlineData("{\"version\":1,\"version\":1,\"kind\":\"heartbeat\"}\n")]
    [InlineData("{\"Version\":1,\"kind\":\"heartbeat\"}\n")]
    [InlineData("{\"version\":\"1\",\"kind\":\"heartbeat\"}\n")]
    [InlineData("{\"version\":1,\"kind\":\"completed\",\"recordCount\":1}\n")]
    [InlineData("{\"version\":1,\"kind\":\"completed\",\"recordCount\":-1}\n")]
    [InlineData("{\"version\":1,\"kind\":\"data\"}\n")]
    public async Task ReadAsync_InvalidEnvelope_RejectsProtocol(string invalid)
    {
        using var stream = new NdjsonTestStreams.FragmentedReadStream(Encoding.UTF8.GetBytes(Heartbeat + invalid));
        await Should.ThrowAsync<NdjsonStreamException>(async () => { await foreach (var unused in new NdjsonEnvelopeReader().ReadAsync<int>(stream)) { } });
    }

    [Fact]
    public async Task ReadDataAsync_NoTerminalFrame_ReportsIncomplete()
    {
        using var stream = new NdjsonTestStreams.FragmentedReadStream(Encoding.UTF8.GetBytes(Heartbeat));
        var error = await Should.ThrowAsync<NdjsonStreamException>(async () => { await foreach (var unused in new NdjsonEnvelopeReader().ReadDataAsync<int>(stream)) { } });
        error.ReasonCode.ShouldBe("stream_incomplete");
    }
}
