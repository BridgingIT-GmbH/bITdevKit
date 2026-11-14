// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Presentation.Web.Caching;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies bounded HTTP source ownership, downstream completion, and finite request input.</summary>
public class CacheHttpStreamingTests
{
    /// <summary>A downstream flush failure aborts delivery after a validated source without reporting destination success.</summary>
    [Fact]
    public async Task FailedDestinationFlush_PreservesSeparateSourceAndDestinationOutcomes()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationCaching().WithBinaryContract("raw").WithBinaryCollection("reports");
        await using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Response.Body = new FailingFlushStream();
        var lifetime = new Lifetime();
        context.Features.Set<IHttpRequestLifetimeFeature>(lifetime);
        var handle = Substitute.For<ICacheReadHandle>();
        handle.Stream.Returns(new MemoryStream([1, 2, 3]));
        handle.Metadata.Returns(new CacheEntryMetadata { Version = new("version"), LogicalSizeBytes = 3 });
        handle.WaitForCompletionAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(Result<CacheReadCompletion>.Success(CacheReadCompletion.Validated)));
        var sut = new CacheStreamingResult(handle, provider.GetRequiredService<CacheCatalog>().Collections["reports"],
            provider.GetRequiredService<CacheRuntimeResources>(), CacheHttpCachePolicy.NoStore);

        await sut.ExecuteAsync(context);

        lifetime.Aborted.ShouldBeTrue();
        await handle.Received(1).WaitForCompletionAsync(Arg.Any<CancellationToken>());
        await handle.Received(1).DisposeAsync();
        var snapshot = provider.GetRequiredService<CacheDiagnostics>().ForCollection("reports").Snapshot();
        snapshot.Counters[CacheObservationKind.HttpDestinationCompleted].ShouldBe(0);
        snapshot.Counters[CacheObservationKind.HttpDestinationFailed].ShouldBe(1);
    }

    /// <summary>Verifies stream result uses bounded transfer and disposes the protected source.</summary>
    [Fact]
    public async Task StreamResult_UsesBoundedTransferAndDisposesTheProtectedSource()
    {
        var services = new ServiceCollection();
        services.AddApplicationCaching().WithBinaryContract("raw").WithBinaryCollection("reports");
        await using var provider = services.BuildServiceProvider();
        var collection = provider.GetRequiredService<IApplicationCache>().GetBinaryCollection("reports");
        await collection.SetAsync(new("report"), new MemoryStream([1, 2, 3]));
        var opened = await collection.OpenReadAsync("report");
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Response.Body = new MemoryStream();
        var catalog = provider.GetRequiredService<CacheCatalog>();
        var resources = provider.GetRequiredService<CacheRuntimeResources>();
        var sut = new CacheStreamingResult(opened.Value.Value, catalog.Collections["reports"], resources, CacheHttpCachePolicy.Revalidate);

        await sut.ExecuteAsync(context);

        context.Response.StatusCode.ShouldBe(200);
        context.Response.ContentLength.ShouldBe(3);
        context.Response.Headers.CacheControl.ToString().ShouldBe("private, no-cache");
        context.Response.Headers.ContentEncoding.ToString().ShouldBe("identity");
        ((MemoryStream)context.Response.Body).ToArray().ShouldBe(new byte[] { 1, 2, 3 });
        resources.Transfers.Active.ShouldBe(0);
        resources.Transient.Used.ShouldBe(0);
    }

    /// <summary>Verifies failed source completion after start aborts and never appends problem json.</summary>
    [Fact]
    public async Task FailedSourceCompletion_AfterStartAbortsAndNeverAppendsProblemJson()
    {
        var services = new ServiceCollection();
        services.AddApplicationCaching().WithBinaryContract("raw").WithBinaryCollection("reports");
        await using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Response.Body = new MemoryStream();
        var lifetime = new Lifetime();
        context.Features.Set<IHttpRequestLifetimeFeature>(lifetime);
        var handle = Substitute.For<ICacheReadHandle>();
        handle.Stream.Returns(new MemoryStream([1, 2, 3]));
        handle.Metadata.Returns(new CacheEntryMetadata { Version = new("version"), LogicalSizeBytes = 3 });
        handle.WaitForCompletionAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(
            Result<CacheReadCompletion>.Failure(new CacheError(CacheErrorCode.Corruption, "failed"))));
        var sut = new CacheStreamingResult(handle, provider.GetRequiredService<CacheCatalog>().Collections["reports"],
            provider.GetRequiredService<CacheRuntimeResources>(), CacheHttpCachePolicy.NoStore);

        await sut.ExecuteAsync(context);

        lifetime.Aborted.ShouldBeTrue();
        ((MemoryStream)context.Response.Body).ToArray().ShouldBe(new byte[] { 1, 2, 3 });
        await handle.Received(1).DisposeAsync();
    }

    /// <summary>Verifies bounded request stream leaves the http stream owned by the caller and rejects excess.</summary>
    [Fact]
    public async Task BoundedRequestStream_LeavesTheHttpStreamOwnedByTheCallerAndRejectsExcess()
    {
        using var inner = new MemoryStream([1, 2, 3, 4]);
        using (var sut = new CacheHttpBoundedReadStream(inner, 3))
        {
            var first = new byte[3];
            await sut.ReadExactlyAsync(first);
            await Should.ThrowAsync<InvalidDataException>(async () => await sut.ReadExactlyAsync(new byte[1]));
        }

        inner.CanRead.ShouldBeTrue();
    }

    /// <summary>A body that exceeded its bound cannot consume more bytes on subsequent attempts.</summary>
    [Fact]
    public async Task BoundedRequestStream_RepeatedReadsAfterOverflowDoNotConsumeInput()
    {
        using var input = new MemoryStream([1, 2, 3, 4, 5]);
        using var sut = new CacheHttpBoundedReadStream(input, 2);
        await Should.ThrowAsync<InvalidDataException>(async () => await sut.ReadExactlyAsync(new byte[5]));
        input.Position.ShouldBe(3);

        await Should.ThrowAsync<InvalidDataException>(async () => await sut.ReadExactlyAsync(new byte[1]));

        input.Position.ShouldBe(3);
    }

    /// <summary>A maximum Int64 byte limit remains readable without arithmetic overflow.</summary>
    [Fact]
    public async Task BoundedRequestStream_LargeLimitDoesNotOverflowItsReadWindow()
    {
        using var input = new MemoryStream([1, 2, 3]);
        using var sut = new CacheHttpBoundedReadStream(input, long.MaxValue);
        var bytes = new byte[3];

        await sut.ReadExactlyAsync(bytes);

        bytes.ShouldBe(new byte[] { 1, 2, 3 });
    }

    private sealed class Lifetime : IHttpRequestLifetimeFeature
    {
        internal bool Aborted { get; private set; }

        /// <summary>Verifies public contract.</summary>
        public CancellationToken RequestAborted { get; set; }

        /// <summary>Verifies abort.</summary>
        public void Abort() => this.Aborted = true;
    }

    private sealed class FailingFlushStream : MemoryStream
    {
        private int flushes;

        /// <inheritdoc />
        public override Task FlushAsync(CancellationToken cancellationToken) => ++this.flushes == 1 ? Task.CompletedTask :
            Task.FromException(new IOException("The downstream response is unavailable."));
    }
}