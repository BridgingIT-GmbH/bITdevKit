// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using System.Runtime.CompilerServices;
using BridgingIT.DevKit.Testing.Ndjson;

public class NdjsonSequenceWriterTests
{
    [Fact]
    public async Task WriteAsync_IdleProducer_FlushesBatchAtOriginalDeadline()
    {
        var clock = new NdjsonTestTimeProvider();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var output = new NdjsonTestStreams.RecordingStream();
        var writer = new NdjsonWriter(options: new() { BatchByteLimit = 64, BatchMaxDelay = TimeSpan.FromMilliseconds(100) }, timeProvider: clock);
        var task = writer.WriteAsync(Source(), output);
        await clock.WaitForTimersAsync(1);
        clock.Advance(TimeSpan.FromMilliseconds(100));
        await output.FirstFlush.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Encoding.UTF8.GetString(output.Bytes).ShouldBe("1\n");
        task.IsCompleted.ShouldBeFalse();
        gate.TrySetResult();
        await task.WaitAsync(TimeSpan.FromSeconds(10));

        async IAsyncEnumerable<int> Source([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return 1;
            await gate.Task.WaitAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task WriteAsync_AlwaysReadyProducer_DoesNotStarveOriginalBatchDeadline()
    {
        var clock = new NdjsonTestTimeProvider();
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var output = new NdjsonTestStreams.RecordingStream();
        var writer = new NdjsonWriter(options: new() { BatchByteLimit = 65536, BatchMaxDelay = TimeSpan.FromMilliseconds(100) }, timeProvider: clock);
        var task = writer.WriteAsync(Source(), output);
        await output.FirstFlush.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Encoding.UTF8.GetString(output.Bytes).ShouldBe("1\n2\n");
        finish.TrySetResult();
        await task.WaitAsync(TimeSpan.FromSeconds(10));
        Encoding.UTF8.GetString(output.Bytes).ShouldBe("1\n2\n3\n");

        async IAsyncEnumerable<int> Source([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return 1;
            clock.Advance(TimeSpan.FromMilliseconds(80));
            yield return 2;
            clock.Advance(TimeSpan.FromMilliseconds(20));
            yield return 3;
            await finish.Task.WaitAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task WriteAsync_DefaultPolicy_FlushesBeforeRequestingNextValue()
    {
        using var output = new NdjsonTestStreams.RecordingStream();
        await new NdjsonWriter().WriteAsync(Source(), output);
        output.FlushCount.ShouldBeGreaterThanOrEqualTo(2);

        async IAsyncEnumerable<int> Source()
        {
            yield return 1;
            output.FlushCount.ShouldBe(1);
            await Task.CompletedTask;
            yield return 2;
        }
    }

    [Fact]
    public async Task WriteAsync_RecordLargerThanBatch_BypassesWithoutSplitting()
    {
        using var output = new NdjsonTestStreams.RecordingStream();
        await new NdjsonWriter(options: new() { BatchByteLimit = 4, BatchMaxDelay = TimeSpan.FromSeconds(1) }).WriteAsync(new[] { "long value" }, output);
        output.WriteCount.ShouldBe(1);
        Encoding.UTF8.GetString(output.Bytes).ShouldBe("\"long value\"\n");
    }
}
