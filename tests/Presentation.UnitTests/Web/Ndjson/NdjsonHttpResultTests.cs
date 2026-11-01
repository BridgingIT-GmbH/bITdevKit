// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Ndjson;

using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Testing.Ndjson;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

public class NdjsonHttpResultTests
{
    [Fact]
    public async Task ExecuteAsync_RawIdleSource_StartsHeadersBeforeFirstMove()
    {
        using var output = new NdjsonTestStreams.RecordingStream();
        using var services = Services();
        var (context, feature, lifetime) = Context(services, output);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = new OutcomeObserver();
        var task = NdjsonHttpResult.From(Source()).WithObserver(observer).ExecuteAsync(context);
        await feature.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        output.Bytes.ShouldBeEmpty();
        context.Response.ContentType.ShouldBe(NdjsonDefaults.CONTENT_TYPE);
        context.Response.Headers.ContainsKey("Content-Disposition").ShouldBeFalse();
        gate.TrySetResult();
        await task.WaitAsync(TimeSpan.FromSeconds(10));
        Encoding.UTF8.GetString(output.Bytes).ShouldBe("1\n");
        observer.Outcome.Status.ShouldBe(NdjsonStreamStatus.Succeeded);
        observer.Outcome.CleanupCompleted.ShouldBeTrue();
        lifetime.Aborted.ShouldBeFalse();

        async IAsyncEnumerable<int> Source([EnumeratorCancellation] CancellationToken ct = default)
        {
            feature.Started.Task.IsCompleted.ShouldBeTrue();
            await gate.Task.WaitAsync(ct);
            yield return 1;
        }
    }

    [Fact]
    public async Task ExecuteAsync_FirstRecordTooLarge_AbortsAdmittedRawResponse()
    {
        using var output = new NdjsonTestStreams.RecordingStream();
        using var services = Services();
        var (context, feature, lifetime) = Context(services, output);
        var observer = new OutcomeObserver();
        await NdjsonHttpResult.From(new[] { "too large" }, new() { MaxRecordBytes = 3 }).WithObserver(observer).ExecuteAsync(context);
        feature.Started.Task.IsCompleted.ShouldBeTrue();
        output.Bytes.ShouldBeEmpty();
        lifetime.Aborted.ShouldBeTrue();
        observer.Outcome.ReasonCode.ShouldBe("stream_record_too_large");
        observer.Outcome.WrittenRecordCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyEnvelope_EmitsInitialHeartbeatThenCompletion()
    {
        using var output = new NdjsonTestStreams.RecordingStream();
        using var services = Services();
        var (context, _, lifetime) = Context(services, output);
        var observer = new OutcomeObserver();
        await NdjsonHttpResult.From(Array.Empty<int>(), new() { Mode = NdjsonMode.Envelope }).WithObserver(observer).ExecuteAsync(context);
        Encoding.UTF8.GetString(output.Bytes).ShouldBe("{\"version\":1,\"kind\":\"heartbeat\"}\n{\"version\":1,\"kind\":\"completed\",\"recordCount\":0}\n");
        context.Response.Headers["X-Ndjson-Envelope-Version"].ToString().ShouldBe("1");
        observer.Count.ShouldBe(1);
        observer.Outcome.WrittenRecordCount.ShouldBe(2);
        observer.Outcome.FlushedRecordCount.ShouldBe(2);
        observer.Outcome.Status.ShouldBe(NdjsonStreamStatus.Succeeded);
        lifetime.Aborted.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_EnvelopeSourceFails_WritesOneSafeErrorWithoutCompletion()
    {
        using var output = new NdjsonTestStreams.RecordingStream();
        using var services = Services();
        var (context, _, _) = Context(services, output);
        var observer = new OutcomeObserver();
        await NdjsonHttpResult.From(Source(), new() { Mode = NdjsonMode.Envelope }).WithObserver(observer).ExecuteAsync(context);
        var text = Encoding.UTF8.GetString(output.Bytes);
        text.ShouldContain("\"kind\":\"error\"");
        text.ShouldNotContain("completed");
        text.ShouldNotContain("private source payload");
        observer.Outcome.Status.ShouldBe(NdjsonStreamStatus.Failed);
        observer.Outcome.SourceCompleted.ShouldBeFalse();
        observer.Outcome.CleanupCompleted.ShouldBeTrue();
        observer.Outcome.WrittenRecordCount.ShouldBe(3);

        async IAsyncEnumerable<int> Source()
        {
            yield return 1;
            await Task.CompletedTask;
            throw new InvalidOperationException("private source payload");
        }
    }

    [Fact]
    public async Task ExecuteAsync_IdleEnvelope_HeartbeatsWithoutCompetingProducerMoves()
    {
        var clock = new NdjsonTestTimeProvider();
        using var output = new NdjsonTestStreams.RecordingStream();
        using var services = Services(clock);
        var (context, _, _) = Context(services, output);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var moves = 0;
        var observer = new OutcomeObserver();
        var task = NdjsonHttpResult.From(Source(), new() { Mode = NdjsonMode.Envelope }).WithObserver(observer).ExecuteAsync(context);
        await clock.WaitForTimersAsync(3);
        clock.Advance(TimeSpan.FromSeconds(15));
        await output.SecondFlush.Task.WaitAsync(TimeSpan.FromSeconds(10));
        moves.ShouldBe(1);
        Encoding.UTF8.GetString(output.Bytes).Split("heartbeat").Length.ShouldBe(3);
        gate.TrySetResult();
        await task.WaitAsync(TimeSpan.FromSeconds(10));
        observer.Outcome.Status.ShouldBe(NdjsonStreamStatus.Succeeded);

        async IAsyncEnumerable<int> Source([EnumeratorCancellation] CancellationToken ct = default)
        {
            Interlocked.Increment(ref moves);
            await gate.Task.WaitAsync(ct);
            yield break;
        }
    }

    [Fact]
    public async Task ExecuteAsync_PreEncodedFrame_ForwardsExactlyAndDisposesTransferredLease()
    {
        using var output = new NdjsonTestStreams.RecordingStream();
        using var services = Services();
        var (context, _, _) = Context(services, output);
        var frame = await new NdjsonWriter().EncodeRecordAsync(new { Value = "exact" });
        var original = frame.FramedBytes.ToArray();
        await NdjsonHttpResult.FromEncodedRecords<object>(Source()).ExecuteAsync(context);
        output.Bytes.ShouldBe(original);
        Should.Throw<ObjectDisposedException>(() => frame.FramedBytes);

        async IAsyncEnumerable<NdjsonEncodedRecord> Source()
        {
            yield return frame;
            await Task.CompletedTask;
        }
    }

    [Fact]
    public void MapHttpNdjson_FailedAdmission_ReturnsExistingHttpFailureWithoutEnumeration()
    {
        var result = Result<IAsyncEnumerable<int>>.Failure().WithError(new ValidationError("Invalid selection."));
        var mapped = result.MapHttpNdjson();
        mapped.Result.ShouldNotBeOfType<NdjsonHttpResult<int>>();
        ((IStatusCodeHttpResult)mapped.Result).StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task ExecuteAsync_ReusedResultOrLateObserver_RejectsMutation()
    {
        using var output = new NdjsonTestStreams.RecordingStream();
        using var services = Services();
        var (context, _, _) = Context(services, output);
        var result = NdjsonHttpResult.From(new[] { 1 });
        await result.ExecuteAsync(context);
        Should.Throw<InvalidOperationException>(() => result.WithObserver(new OutcomeObserver()));
        await Should.ThrowAsync<InvalidOperationException>(() => result.ExecuteAsync(context));
    }

    internal static ServiceProvider Services(TimeProvider clock = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (clock is not null) { services.AddSingleton(clock); }

        services.AddNdjsonStreaming();
        return services.BuildServiceProvider();
    }

    internal static (DefaultHttpContext Context, BodyFeature Body, LifetimeFeature Lifetime) Context(IServiceProvider services, Stream output)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        var body = new BodyFeature(output);
        var lifetime = new LifetimeFeature();
        context.Features.Set<IHttpResponseBodyFeature>(body);
        context.Features.Set<IHttpRequestLifetimeFeature>(lifetime);
        return (context, body, lifetime);
    }

    internal sealed class OutcomeObserver : INdjsonStreamObserver
    {
        internal TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal int Count { get; private set; }

        internal NdjsonStreamOutcome Outcome { get; private set; }

        public Task OnFinishedAsync(NdjsonStreamOutcome outcome, CancellationToken cancellationToken = default)
        {
            this.Count++;
            this.Outcome = outcome;
            this.Finished.TrySetResult();
            return Task.CompletedTask;
        }
    }

    internal sealed class BodyFeature(Stream stream) : IHttpResponseBodyFeature
    {
        internal Task StartGate { get; set; } = Task.CompletedTask;

        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool BufferingDisabled { get; private set; }

        public Stream Stream => stream;
        public PipeWriter Writer { get; } = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));
        public void DisableBuffering() => this.BufferingDisabled = true;
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.Started.TrySetResult();
            await this.StartGate.WaitAsync(cancellationToken);
        }

        public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CompleteAsync() => Task.CompletedTask;
    }

    internal sealed class LifetimeFeature : IHttpRequestLifetimeFeature
    {
        private readonly CancellationTokenSource cancellation = new();
        internal bool Aborted { get; private set; }

        internal TaskCompletionSource AbortedSignal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken RequestAborted { get => this.cancellation.Token; set => throw new NotSupportedException(); }

        public void Abort() { this.Aborted = true; this.AbortedSignal.TrySetResult(); this.cancellation.Cancel(); }
    }
}
