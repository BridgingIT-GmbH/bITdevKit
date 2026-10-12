// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Text.Json;
using BridgingIT.DevKit.Application.Storage;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies storage profiling ownership, metadata, failure handling and stream transparency.</summary>
/// <example><code>await suite.DeleteAsync_IndependentCall_CapturesCoarseDimensionsAndCorrelation("BlobStorage");</code></example>
public sealed partial class StorageProfilingBehaviorTests
{
    /// <summary>Independent calls retain stable resource dimensions and the publisher correlation, with no Runtime session.</summary>
    /// <example><code>await suite.DeleteAsync_IndependentCall_CapturesCoarseDimensionsAndCorrelation("FileStorage");</code></example>
    [Theory]
    [InlineData("FileStorage")]
    [InlineData("DocumentStorage")]
    [InlineData("BlobStorage")]
    public async Task DeleteAsync_IndependentCall_CapturesCoarseDimensionsAndCorrelation(string kind)
    {
        using var services = await Services();
        using var correlation = CorrelationId.BeginScope("origin-correlation");
        var expected = Result.Success().WithMessage("private-result-message");
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var sut = Delete(kind, services.GetRequiredService<IOperationProfiler>(), token =>
        {
            token.ShouldBe(cancellation.Token); calls++;
            return Task.FromResult(expected);
        });

        var result = await sut(cancellation.Token);

        result.ShouldBe(expected); calls.ShouldBe(1);
        var record = (await Stored(services)).ShouldHaveSingleItem();
        record.Kind.ShouldBe(kind); record.CorrelationId.ShouldBe("origin-correlation");
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        var prefix = kind.ToLowerInvariant();
        record.Dimensions.ShouldContain(dimension => dimension.Key == prefix + ".operation" && dimension.Value.Scalar == "delete");
        record.Dimensions.ShouldContain(dimension => dimension.Key == prefix + (kind == "FileStorage" ? ".location" : kind == "DocumentStorage" ? ".client" : ".store"));
        if (kind == "BlobStorage") { record.Dimensions.ShouldContain(dimension => dimension.Key == "blobstorage.container" && dimension.Value.Scalar == "reports"); }

        AssertRedacted(record);
        services.GetRequiredService<IOperationProfiler>().Current.ShouldBeNull();
        record.RuntimeSessionKeyAtStart.ShouldBeNull(); record.RuntimeSessionIdAtStart.ShouldBeNull();
        CorrelationId.Current.ShouldBe("origin-correlation");
    }

    /// <summary>Storage calls aggregate under the caller without changing its correlation or creating additional roots.</summary>
    /// <example><code>await suite.DeleteAsync_JoinedCalls_AggregatesSegmentsUnderCaller();</code></example>
    [Fact]
    public async Task DeleteAsync_JoinedCalls_AggregatesSegmentsUnderCaller()
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        using (var caller = profiling.BeginOperation(new() { Key = "caller", Kind = "HttpRequest", CorrelationId = "request-correlation" }))
        {
            foreach (var kind in new[] { "FileStorage", "DocumentStorage", "BlobStorage" })
            {
                var sut = Delete(kind, profiling, _ => Task.FromResult(Result.Success()));
                await sut(default); await sut(default);
            }

            profiling.Current.ShouldBeSameAs(caller); caller.Complete();
        }

        var record = (await Stored(services)).ShouldHaveSingleItem();
        record.Key.ShouldBe("caller"); record.CorrelationId.ShouldBe("request-correlation");
        record.Segments.Count.ShouldBe(3);
        record.Segments.ShouldAllBe(segment => segment.Statistics.Count == 2 && segment.Dimensions.Count >= 2);
        AssertRedacted(record);
    }

    /// <summary>Configured resources distinguish cohorts through dimensions while sharing the same operation key.</summary>
    /// <example><code>await suite.DeleteAsync_DifferentResources_GroupByDimensionWithoutChangingOperationKey("BlobStorage", "blobstorage.container");</code></example>
    [Theory]
    [InlineData("FileStorage", "filestorage.location")]
    [InlineData("DocumentStorage", "documentstorage.client")]
    [InlineData("BlobStorage", "blobstorage.container")]
    public async Task DeleteAsync_DifferentResources_GroupByDimensionWithoutChangingOperationKey(string kind, string dimension)
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        foreach (var resource in new[] { "primary", "archive" }) { await Delete(kind, profiling, _ => Task.FromResult(Result.Success()), resource)(default); }

        var records = await Stored(services);
        records.Count.ShouldBe(2); records.Select(record => record.Key).Distinct().ShouldHaveSingleItem();
        var grouped = await services.GetRequiredService<IOperationProfilingQueryService>().GroupAsync(new() { Kind = kind, GroupingDimensions = [dimension] });
        grouped.IsSuccess.ShouldBeTrue(); grouped.Value.TotalGroupCount.ShouldBe(2);
        grouped.Value.Groups.ShouldAllBe(group => group.Count == 1);
    }

    /// <summary>Failed Results remain unchanged while the profile records their observed failure without the message.</summary>
    /// <example><code>await suite.DeleteAsync_FailedResult_PreservesResultAndRecordsFailure("DocumentStorage");</code></example>
    [Theory]
    [InlineData("FileStorage")]
    [InlineData("DocumentStorage")]
    [InlineData("BlobStorage")]
    public async Task DeleteAsync_FailedResult_PreservesResultAndRecordsFailure(string kind)
    {
        using var services = await Services();
        var expected = Result.Failure(new Error("private-result-message"));
        var sut = Delete(kind, services.GetRequiredService<IOperationProfiler>(), _ => Task.FromResult(expected));

        (await sut(default)).ShouldBe(expected);

        var record = (await Stored(services)).ShouldHaveSingleItem();
        record.Outcome.ShouldBe(OperationProfilingOutcome.Failed); record.Failure.Message.ShouldBeNull();
        AssertRedacted(record);
    }

    /// <summary>Caller cancellation and business exceptions are rethrown unchanged and close the owning scope.</summary>
    /// <example><code>await suite.DeleteAsync_ThrownException_PreservesExceptionAndClassifiesOutcome("BlobStorage", true);</code></example>
    [Theory]
    [InlineData("FileStorage", false)]
    [InlineData("DocumentStorage", false)]
    [InlineData("BlobStorage", false)]
    [InlineData("FileStorage", true)]
    [InlineData("DocumentStorage", true)]
    [InlineData("BlobStorage", true)]
    public async Task DeleteAsync_ThrownException_PreservesExceptionAndClassifiesOutcome(string kind, bool canceled)
    {
        using var services = await Services();
        using var cancellation = new CancellationTokenSource();
        if (canceled) { cancellation.Cancel(); }

        Exception expected = canceled ? new OperationCanceledException(cancellation.Token) : new InvalidOperationException("private-result-message");
        var sut = Delete(kind, services.GetRequiredService<IOperationProfiler>(), _ => Task.FromException<Result>(expected));

        Exception actual = null;
        try { await sut(cancellation.Token); }
        catch (Exception caught) { actual = caught; }

        actual.ShouldBeSameAs(expected);
        var record = (await Stored(services)).ShouldHaveSingleItem();
        record.Outcome.ShouldBe(canceled ? OperationProfilingOutcome.Canceled : OperationProfilingOutcome.Failed);
        services.GetRequiredService<IOperationProfiler>().Current.ShouldBeNull(); AssertRedacted(record);
    }

    /// <summary>Disabled and inherited-suppressed profiling never creates fallback roots or changes delegate execution.</summary>
    /// <example><code>await suite.DeleteAsync_DisabledOrSuppressed_ExecutesWithoutCapture("FileStorage", false);</code></example>
    [Theory]
    [InlineData("FileStorage", false)]
    [InlineData("DocumentStorage", false)]
    [InlineData("BlobStorage", false)]
    [InlineData("FileStorage", true)]
    [InlineData("DocumentStorage", true)]
    [InlineData("BlobStorage", true)]
    public async Task DeleteAsync_DisabledOrSuppressed_ExecutesWithoutCapture(string kind, bool disabled)
    {
        using var services = await Services(!disabled);
        var profiling = services.GetRequiredService<IOperationProfiler>();
        using var suppression = disabled ? null : profiling.Suppress();
        var calls = 0;
        var sut = Delete(kind, profiling, _ => { calls++; return Task.FromResult(Result.Success()); });

        (await sut(default)).IsSuccess.ShouldBeTrue();

        calls.ShouldBe(1); (services.GetService<OperationProfilingCompletionQueue>()?.Snapshot().Count ?? 0).ShouldBe(0);
    }

    /// <summary>Faults in metadata, status and disposal cannot stop storage work or replace its original exception.</summary>
    /// <example><code>await suite.DeleteAsync_FaultyProfiler_PreservesDelegateAndException("DocumentStorage", false);</code></example>
    [Theory]
    [InlineData("FileStorage", false)]
    [InlineData("DocumentStorage", false)]
    [InlineData("BlobStorage", false)]
    [InlineData("FileStorage", true)]
    [InlineData("DocumentStorage", true)]
    [InlineData("BlobStorage", true)]
    public async Task DeleteAsync_FaultyProfiler_PreservesDelegateAndException(string kind, bool faultyStatus)
    {
        var profiling = Substitute.For<IOperationProfiler>();
        profiling.Current.Returns((IProfilingOperationScope)null);
        var scope = Substitute.For<IProfilingOperationScope>();
        if (faultyStatus) { scope.IsRecording.Returns(_ => throw new InvalidOperationException("status")); }
        else { scope.IsRecording.Returns(true); scope.When(value => value.SetDimension(Arg.Any<string>(), Arg.Any<object>())).Do(_ => throw new InvalidOperationException("metadata")); }

        scope.When(value => value.Dispose()).Do(_ => throw new InvalidOperationException("dispose"));
        profiling.BeginOperation(Arg.Any<OperationProfilingStartRequest>()).Returns(scope);
        var calls = 0;
        var expected = new InvalidOperationException("original");
        var sut = Delete(kind, profiling, _ => { calls++; return Task.FromException<Result>(expected); });

        (await Should.ThrowAsync<InvalidOperationException>(() => sut(default))).ShouldBeSameAs(expected);

        calls.ShouldBe(1);
    }

    /// <summary>Opening a file or blob stream does not inspect, consume, replace or dispose the returned content.</summary>
    /// <example><code>await suite.ReadAsync_ReturnedStreams_RemainCallerOwned(false);</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAsync_ReturnedStreams_RemainCallerOwned(bool blob)
    {
        using var services = await Services();
        using var stream = new UninspectableStream();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        if (blob)
        {
            var key = new BlobKey("reports", "private-path");
            var inner = Substitute.For<IBlobStoreClient>();
            var download = new BlobDownload { Content = stream, Info = new BlobInfo { Key = key } };
            var expected = Result<BlobDownload>.Success(download);
            inner.DownloadAsync(key, Arg.Any<CancellationToken>()).Returns(expected);
            var sut = new ProfilingBlobStoreClientBehavior(inner, profiling, "archive");
            (await sut.DownloadAsync(key)).Value.ShouldBeSameAs(download);
        }
        else
        {
            var inner = Substitute.For<IFileStorageProvider>(); inner.LocationName.Returns("documents");
            inner.ReadFileAsync("private-path", null, Arg.Any<CancellationToken>()).Returns(Result<Stream>.Success(stream));
            var sut = new ProfilingFileStorageBehavior(inner, profiling);
            (await sut.ReadFileAsync("private-path")).Value.ShouldBeSameAs(stream);
        }

        stream.Disposed.ShouldBeFalse();
        (await Stored(services)).ShouldHaveSingleItem().Outcome.ShouldBe(OperationProfilingOutcome.Completed);
    }

    /// <summary>Document batch failure metadata marks the profile as partial failure while preserving its successful partial result.</summary>
    /// <example><code>await suite.DocumentBatch_PartialResult_RecordsFailureWithoutKeys(false);</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DocumentBatch_PartialResult_RecordsFailureWithoutKeys(bool deletes)
    {
        using var services = await Services();
        var inner = Substitute.For<IDocumentStoreClient<StoredDocument>>();
        var sut = new ProfilingDocumentStoreClientBehavior<StoredDocument>(inner, services.GetRequiredService<IOperationProfiler>());
        var key = new DocumentKey("private-partition", "private-key");
        if (deletes)
        {
            var expected = Result<DocumentBatchResult<DocumentKey>>.Success(new() { FailedKeys = [key] });
            inner.DeleteManyAsync(Arg.Any<IReadOnlyCollection<DocumentDelete>>(), Arg.Any<CancellationToken>()).Returns(expected);
            (await sut.DeleteManyAsync([new(key)])).ShouldBe(expected);
        }
        else
        {
            var expected = Result<DocumentBatchResult<DocumentInfo>>.Success(new() { FailedKey = key });
            inner.UpsertManyAsync(Arg.Any<IReadOnlyCollection<DocumentWrite<StoredDocument>>>(), Arg.Any<CancellationToken>()).Returns(expected);
            (await sut.UpsertManyAsync([new(key, new())])).ShouldBe(expected);
        }

        var record = (await Stored(services)).ShouldHaveSingleItem();
        record.Outcome.ShouldBe(OperationProfilingOutcome.Failed); record.Failure.Code.ShouldBe("PartialBatch"); AssertRedacted(record);
    }

    private static Func<CancellationToken, Task<Result>> Delete(string kind, IOperationProfiler profiling, Func<CancellationToken, Task<Result>> next, string resource = null)
    {
        if (kind == "FileStorage")
        {
            var inner = Substitute.For<IFileStorageProvider>(); inner.LocationName.Returns(resource ?? "documents");
            inner.DeleteFileAsync("private-path", null, Arg.Any<CancellationToken>()).Returns(call => next(call.Arg<CancellationToken>()));
            var sut = new ProfilingFileStorageBehavior(inner, profiling);
            return token => sut.DeleteFileAsync("private-path", cancellationToken: token);
        }

        if (kind == "DocumentStorage")
        {
            var inner = Substitute.For<IDocumentStoreClient<StoredDocument>, IDocumentStoreClientIdentity>();
            ((IDocumentStoreClientIdentity)inner).ClientName.Returns(resource ?? "archive");
            var key = new DocumentKey("private-partition", "private-key");
            inner.DeleteAsync(key, null, Arg.Any<CancellationToken>()).Returns(call => next(call.Arg<CancellationToken>()));
            var sut = new ProfilingDocumentStoreClientBehavior<StoredDocument>(inner, profiling);
            return token => sut.DeleteAsync(key, cancellationToken: token);
        }

        var blobInner = Substitute.For<IBlobStoreClient>(); var blobKey = new BlobKey(resource ?? "reports", "private-key");
        blobInner.DeleteAsync(blobKey, null, Arg.Any<CancellationToken>()).Returns(call => next(call.Arg<CancellationToken>()));
        var blob = new ProfilingBlobStoreClientBehavior(blobInner, profiling, "archive");
        return token => blob.DeleteAsync(blobKey, cancellationToken: token);
    }

    private static void AssertRedacted(OperationProfilingRecord record)
    {
        var json = JsonSerializer.Serialize(record);
        foreach (var secret in new[] { "private-path", "private-partition", "private-key", "private-result-message", "private-token", "private-payload" }) { json.ShouldNotContain(secret); }
    }

    private static async Task<ServiceProvider> Services(bool enabled = true)
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddProfiling(options => options.Enabled()).WithOperationProfiling(options => options.Enabled(enabled));
        var provider = services.BuildServiceProvider();
        if (enabled) { await provider.GetRequiredService<OperationProfilingWriterService>().TickAsync(); }

        return provider;
    }

    private static async Task<IReadOnlyList<OperationProfilingRecord>> Stored(IServiceProvider services)
    {
        await services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        var result = await services.GetRequiredService<IOperationProfilingQueryService>().QueryAsync(new()
        {
            FromUtc = DateTimeOffset.UtcNow.AddHours(-1), ToUtc = DateTimeOffset.UtcNow.AddHours(1), Outcomes = Enum.GetValues<OperationProfilingOutcome>(), PageSize = 100,
        });
        result.IsSuccess.ShouldBeTrue(); return result.Value.Records;
    }

    /// <summary>Provides a public document type usable by dynamic client substitutes.</summary>
    /// <example><code>var document = new StoredDocument();</code></example>
    public sealed class StoredDocument;

    private sealed class UninspectableStream : Stream
    {
        /// <summary>Gets whether storage profiling disposed caller-owned content.</summary>
        /// <example><code>stream.Disposed.ShouldBeFalse();</code></example>
        public bool Disposed { get; private set; }
        /// <inheritdoc />
        public override bool CanRead => throw new InvalidOperationException("Stream inspection is not allowed.");
        /// <inheritdoc />
        public override bool CanSeek => throw new InvalidOperationException("Stream inspection is not allowed.");
        /// <inheritdoc />
        public override bool CanWrite => throw new InvalidOperationException("Stream inspection is not allowed.");
        /// <inheritdoc />
        public override long Length => throw new InvalidOperationException("Stream inspection is not allowed.");
        /// <inheritdoc />
        public override long Position { get => throw new InvalidOperationException(); set => throw new InvalidOperationException(); }
        /// <inheritdoc />
        public override void Flush() => throw new InvalidOperationException();
        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException();
        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new InvalidOperationException();
        /// <inheritdoc />
        public override void SetLength(long value) => throw new InvalidOperationException();
        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException();
        /// <inheritdoc />
        protected override void Dispose(bool disposing) { this.Disposed = true; base.Dispose(disposing); }
    }
}
