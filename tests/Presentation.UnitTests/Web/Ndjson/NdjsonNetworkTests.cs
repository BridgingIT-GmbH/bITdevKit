// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Ndjson;

using System.Runtime.CompilerServices;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Builder;

public class NdjsonNetworkTests
{
    [Theory]
    [InlineData(NdjsonMode.Raw)]
    [InlineData(NdjsonMode.Envelope)]
    public async Task StreamingResult_RealKestrel_ReceivesFirstRecordBeforeSourceCompletion(NdjsonMode mode)
    {
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = await LoopbackStreamingFixture.StartAsync(app => app.MapGet("/stream", () => NdjsonHttpResult.From(Source())).WithNdjson<int>(o => o.Mode = mode));
        using var client = new HttpClient { BaseAddress = server.Address };
        var result = await client.GetNdjsonAsync<int>("/stream", new() { ExpectedMode = mode });
        result.IsSuccess.ShouldBeTrue();
        await using var response = result.Value;
        await using var items = (mode == NdjsonMode.Raw ? response.ReadAsync() : response.ReadDataAsync()).GetAsyncEnumerator();
        (await items.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue();
        items.Current.ShouldBe(1);
        finish.Task.IsCompleted.ShouldBeFalse();
        disposed.Task.IsCompleted.ShouldBeFalse();
        finish.TrySetResult();
        (await items.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue();
        items.Current.ShouldBe(2);
        (await items.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeFalse();
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));

        async IAsyncEnumerable<int> Source([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            try
            {
                yield return 1;
                await finish.Task.WaitAsync(cancellationToken);
                yield return 2;
            }
            finally { disposed.TrySetResult(); }
        }
    }

    [Theory]
    [InlineData(NdjsonMode.Raw)]
    [InlineData(NdjsonMode.Envelope)]
    public async Task StreamingResult_LiveIdleCancellation_ReleasesProducerResources(NdjsonMode mode)
    {
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = await LoopbackStreamingFixture.StartAsync(app => app.MapGet("/stream", () => NdjsonHttpResult.From(Source())).WithNdjson<int>(o => o.Mode = mode));
        using var client = new HttpClient { BaseAddress = server.Address };
        var opened = await client.GetNdjsonAsync<int>("/stream", new() { ExpectedMode = mode });
        await using var response = opened.Value;
        await using var items = (mode == NdjsonMode.Raw ? response.ReadAsync() : response.ReadDataAsync()).GetAsyncEnumerator();
        (await items.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue();
        await response.DisposeAsync();
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));

        async IAsyncEnumerable<int> Source([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            try
            {
                yield return 1;
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            finally { disposed.TrySetResult(); }
        }
    }
}
