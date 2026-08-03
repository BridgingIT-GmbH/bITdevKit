// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;

[UnitTest("Application")]
public sealed class DocumentStoreClientBehaviorTests
{
    [Fact]
    public async Task TimeoutBehavior_WhenDeadlineElapses_CancelsAndAwaitsInnerOperation()
    {
        var inner = new BlockingClient();
        var sut = new TimeoutDocumentStoreClientBehavior<TestDocument>(null, inner, new() { Timeout = TimeSpan.FromMilliseconds(20) });

        var result = await sut.GetAsync(new("p", "r"));

        result.Errors.ShouldContain(x => x is DocumentStoreTimeoutError);
        inner.WasCanceled.ShouldBeTrue();
        inner.HasCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task RetryBehavior_WithValidationFailure_DoesNotRetry()
    {
        var inner = new CountingClient(Result<DocumentEntry<TestDocument>>.Failure(new DocumentStoreInvalidQueryError()));
        var sut = new RetryDocumentStoreClientBehavior<TestDocument>(null, inner, new() { Attempts = 3, Backoff = TimeSpan.Zero });

        await sut.GetAsync(new("p", "r"));

        inner.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task TimeoutBehavior_Delete_WhenDeadlineElapses_CancelsAndAwaitsInnerOperation()
    {
        var inner = new BlockingDeleteClient();
        var sut = new TimeoutDocumentStoreClientBehavior<TestDocument>(null, inner, new() { Timeout = TimeSpan.FromMilliseconds(20) });

        var result = await sut.DeleteAsync(new("p", "r"));

        result.Errors.ShouldContain(x => x is DocumentStoreTimeoutError);
        inner.WasCanceled.ShouldBeTrue();
        inner.HasCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task RetryBehavior_Delete_WithTransientProviderFailure_Retries()
    {
        var inner = new CountingDeleteClient();
        var sut = new RetryDocumentStoreClientBehavior<TestDocument>(null, inner, new() { Attempts = 3, Backoff = TimeSpan.Zero });

        var result = await sut.DeleteAsync(new("p", "r"));

        result.IsSuccess.ShouldBeTrue();
        inner.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task CacheBehavior_WithTwoNamedClients_UsesIsolatedKeys()
    {
        var cache = Substitute.For<ICacheProvider>();
        var primary = new CacheDocumentStoreClientBehavior<TestDocument>(null,
            new DocumentStoreClient<TestDocument>(new InMemoryDocumentStoreProvider(), clientName: "primary"), cache);
        var archive = new CacheDocumentStoreClientBehavior<TestDocument>(null,
            new DocumentStoreClient<TestDocument>(new InMemoryDocumentStoreProvider(), clientName: "archive"), cache);
        var key = new DocumentKey("p", "r");

        await primary.UpsertAsync(key, new());
        await archive.UpsertAsync(key, new());

        var removedKeys = cache.ReceivedCalls()
            .Where(x => x.GetMethodInfo().Name == nameof(ICacheProvider.RemoveAsync))
            .Select(x => (string)x.GetArguments()[0])
            .ToArray();
        removedKeys.Length.ShouldBe(2);
        removedKeys[0].ShouldContain("primary");
        removedKeys[1].ShouldContain("archive");
    }

    [Fact]
    public async Task CacheBehavior_WithDelimiterAmbiguousDocumentKeys_UsesIsolatedKeys()
    {
        var cache = Substitute.For<ICacheProvider>();
        var sut = new CacheDocumentStoreClientBehavior<TestDocument>(null,
            new DocumentStoreClient<TestDocument>(new InMemoryDocumentStoreProvider(), clientName: "primary"), cache);

        await sut.UpsertAsync(new("a_b", "c"), new());
        await sut.UpsertAsync(new("a", "b_c"), new());

        var removedKeys = cache.ReceivedCalls()
            .Where(x => x.GetMethodInfo().Name == nameof(ICacheProvider.RemoveAsync))
            .Select(x => (string)x.GetArguments()[0])
            .ToArray();
        removedKeys.Distinct().Count().ShouldBe(2);
    }

    public sealed class TestDocument { public string Value { get; set; } }

    private class ForwardingClient : DocumentStoreClientBehaviorBase<TestDocument>
    {
        public ForwardingClient() : base(Substitute.For<IDocumentStoreClient<TestDocument>>()) { }
    }

    private sealed class BlockingClient : ForwardingClient
    {
        public bool WasCanceled { get; private set; }
        public bool HasCompleted { get; private set; }
        public override async Task<Result<DocumentEntry<TestDocument>>> GetAsync(DocumentKey key, CancellationToken cancellationToken = default)
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            catch (OperationCanceledException) { this.WasCanceled = true; await Task.Delay(10); throw; }
            finally { this.HasCompleted = true; }
            return Result<DocumentEntry<TestDocument>>.Failure(new DocumentStoreProviderError());
        }
    }

    private sealed class CountingClient(Result<DocumentEntry<TestDocument>> result) : ForwardingClient
    {
        public int Calls { get; private set; }
        public override Task<Result<DocumentEntry<TestDocument>>> GetAsync(DocumentKey key, CancellationToken cancellationToken = default) { this.Calls++; return Task.FromResult(result); }
    }

    private sealed class BlockingDeleteClient : ForwardingClient
    {
        public bool WasCanceled { get; private set; }
        public bool HasCompleted { get; private set; }
        public override async Task<Result> DeleteAsync(DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default)
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            catch (OperationCanceledException) { this.WasCanceled = true; await Task.Delay(10); throw; }
            finally { this.HasCompleted = true; }
            return Result.Failure(new DocumentStoreProviderError());
        }
    }

    private sealed class CountingDeleteClient : ForwardingClient
    {
        public int Calls { get; private set; }
        public override Task<Result> DeleteAsync(DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default)
        {
            this.Calls++;
            return Task.FromResult(this.Calls == 1
                ? Result.Failure(new DocumentStoreProviderError())
                : Result.Success());
        }
    }
}
