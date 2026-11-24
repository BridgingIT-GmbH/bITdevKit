// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Results;

[UnitTest("Common")]
public class ResultCompositionContractTests
{
    [Fact]
    public async Task Collect_WithMessageOnlyFailure_PreservesStateAndOrderedDiagnostics()
    {
        var sut = Result<IEnumerable<int>>.Success([1, 2, 3]).WithMessage("source");
        static Result<int> CollectItem(int item) => item == 2
            ? Result<int>.Failure("item 2")
            : Result<int>.Success(item, $"item {item}");

        var sync = sut.Collect(CollectItem);
        var async = await sut.CollectAsync((item, _) => Task.FromResult(CollectItem(item)));
        var task = await Task.FromResult(sut).Collect(CollectItem);

        foreach (var result in new[] { sync, async, task })
        {
            result.IsFailure.ShouldBeTrue();
            result.Messages.ShouldBe(["source", "item 1", "item 2", "item 3"]);
            result.Errors.ShouldBeEmpty();
        }
    }

    [Fact]
    public void Collect_WhenSourceEnumerationThrows_PreservesAccumulatedDiagnostics()
    {
        static IEnumerable<int> Items()
        {
            yield return 1;
            throw new InvalidOperationException("enumeration failed");
        }

        var sut = Result<IEnumerable<int>>.Success(Items()).WithMessage("source");

        var result = sut.Collect(value => Result<int>.Success(value, "child"));

        result.IsFailure.ShouldBeTrue();
        result.Messages.ShouldBe(["source", "child"]);
        result.GetError<ExceptionError>().Exception.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task CollectAsync_WithMessageOnlyFailure_PreservesPageMetadata()
    {
        var sut = ResultPaged<int>.Success([1, 2], count: 42, page: 3, pageSize: 5).WithMessage("page");
        static Result<string> CollectItem(int item) => item == 2
            ? Result<string>.Failure(message: "item failed")
            : Result<string>.Success("one", "item succeeded");

        var sync = sut.Collect(CollectItem);
        var async = await sut.CollectAsync((item, _) => Task.FromResult(CollectItem(item)));
        var task = await Task.FromResult(sut).CollectAsync((item, _) => Task.FromResult(CollectItem(item)));

        foreach (var result in new[] { sync, async, task })
        {
            result.IsFailure.ShouldBeTrue();
            result.Value.ShouldBeEmpty();
            result.Messages.ShouldBe(["page", "item succeeded", "item failed"]);
            result.TotalCount.ShouldBe(42);
            result.CurrentPage.ShouldBe(3);
            result.PageSize.ShouldBe(5);
        }
    }

    [Fact]
    public async Task Filter_WithEmptyPageProjection_PreservesPageMetadata()
    {
        var sut = ResultPaged<int>.Success([1, 2], count: 42, page: 3, pageSize: 5).WithMessage("page");

        var sync = sut.Filter((int item) => item > 2);
        var async = await sut.FilterAsync((int item, CancellationToken _) => Task.FromResult(item > 2));

        foreach (var result in new[] { sync, async })
        {
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBeEmpty();
            result.Messages.ShouldBe(["page"]);
            result.TotalCount.ShouldBe(42);
            result.CurrentPage.ShouldBe(3);
            result.PageSize.ShouldBe(5);
        }
    }

    [Fact]
    public async Task Map_WhenCallbackThrowsOrTaskSuppliesFailedPage_RetainsPageMetadata()
    {
        var sut = ResultPaged<int>.Success([1, 2], count: 42, page: 3, pageSize: 5).WithMessage("page");
        var error = new Error("already failed");

        var thrown = sut.Map<int, string>(_ => throw new InvalidOperationException());
        var failed = await Task.FromResult(sut.WithError(error)).Map<int, string>(
            _ => throw new InvalidOperationException("mapper should not run"));

        foreach (var result in new[] { thrown, failed })
        {
            result.IsFailure.ShouldBeTrue();
            result.Value.ShouldBeEmpty();
            result.Messages.ShouldBe(["page"]);
            result.TotalCount.ShouldBe(42);
            result.CurrentPage.ShouldBe(3);
            result.PageSize.ShouldBe(5);
        }

        thrown.HasError<ExceptionError>().ShouldBeTrue();
        failed.Errors.ShouldBe([error]);
    }

    [Fact]
    public async Task BindAsync_WhenChildSuppliesPage_AdoptsChildMetadataAndKeepsDiagnosticOrder()
    {
        var sut = ResultPaged<int>.Success([1, 2], count: 42, page: 3, pageSize: 5).WithMessage("source");
        var child = ResultPaged<string>.Success(["one"], count: 80, page: 2, pageSize: 7).WithMessage("child");

        var result = await Task.FromResult(sut).BindAsync((_, _) => Task.FromResult(child));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(["one"]);
        result.TotalCount.ShouldBe(80);
        result.CurrentPage.ShouldBe(2);
        result.PageSize.ShouldBe(7);
        result.Messages.ShouldBe(["source", "child"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Bind_WhenChildSuppliesOnlyStatus_DoesNotReplaceMergedPageMetadata(bool childFails)
    {
        var sut = ResultPaged<int>.Success([1, 2], count: 95, page: 3, pageSize: 2).WithMessage("source");
        var status = (childFails ? Result.Failure("child") : Result.Success("child")).ToResultPaged<int>();

        var sync = sut.Bind(_ => status);
        var async = await sut.BindAsync((_, _) => Task.FromResult(status));

        foreach (var bound in new[] { sync, async })
        {
            var merged = ResultPaged<int>.Merge(sut, bound);

            merged.IsFailure.ShouldBe(childFails);
            merged.Value.ShouldBe([1, 2]);
            merged.TotalCount.ShouldBe(95);
            merged.CurrentPage.ShouldBe(3);
            merged.PageSize.ShouldBe(2);
            bound.Messages.ShouldBe(["source", "child"]);
            merged.Errors.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task MapItems_WithEmptyCollection_ReturnsSuccessfulEmptyCollection()
    {
        var sut = Result<IEnumerable<int>>.Success([]).WithMessage("source");
        var page = ResultPaged<int>.Success([], count: 42, page: 9, pageSize: 5);

        var mapped = sut.MapItems(value => value.ToString());
        var mappedPage = await page.MapItemsAsync((value, _) => Task.FromResult(value.ToString()));

        mapped.IsSuccess.ShouldBeTrue();
        mapped.Value.ShouldBeEmpty();
        mapped.Messages.ShouldBe(["source"]);
        mappedPage.IsSuccess.ShouldBeTrue();
        mappedPage.Value.ShouldBeEmpty();
        mappedPage.TotalCount.ShouldBe(42);
        mappedPage.CurrentPage.ShouldBe(9);
        mappedPage.PageSize.ShouldBe(5);
    }

    [Fact]
    public void BindItems_WithMessageOnlyFailure_ReturnsFailedPartialCollection()
    {
        var sut = Result<IEnumerable<int>>.Success([1, 2]).WithMessage("source");
        static Result<IEnumerable<string>> BindItem(int item) => item == 1
            ? Result<IEnumerable<string>>.Success(["one"])
            : Result<IEnumerable<string>>.Failure("could not bind");

        var result = sut.BindItems(BindItem);

        result.IsFailure.ShouldBeTrue();
        result.Value.ShouldBe(["one"]);
        result.Messages.ShouldBe(["source", "could not bind"]);
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task BindResult_WhenCallbackFails_PreservesDiagnosticsAndSkipsMerge()
    {
        var sut = Result<int>.Success(42, "outer");
        var error = new Error("inner error");
        var inner = Result<string>.Failure("inner", error);
        var mergeCalls = 0;
        int Merge(int value, string _) { mergeCalls++; return value; }

        var sync = sut.BindResult(_ => inner, Merge);
        var async = await sut.BindResultAsync((_, _) => Task.FromResult(inner), Merge);

        foreach (var result in new[] { sync, async })
        {
            result.IsFailure.ShouldBeTrue();
            result.Messages.ShouldBe(["outer", "inner"]);
            result.Errors.ShouldBe([error]);
        }

        mergeCalls.ShouldBe(0);
    }

    [Fact]
    public async Task BindResultAsync_WhenMergeThrows_PreservesBothSetsOfDiagnostics()
    {
        var sut = Result<int>.Success(42, "outer");

        var result = await Task.FromResult(sut).BindResultAsync(
            (_, _) => Task.FromResult(Result<string>.Success("value", "inner")),
            (_, _) => throw new InvalidOperationException());

        result.IsFailure.ShouldBeTrue();
        result.Messages.ShouldBe(["outer", "inner"]);
        result.HasError<ExceptionError>().ShouldBeTrue();
    }

    [Fact]
    public async Task AndThenResult_WithFailedCallbacks_PropagatesFailureAndRetainsValue()
    {
        var sut = Result<int>.Success(42, "outer");
        var error = new Error("child error");
        var sourceTask = Task.FromResult(sut);

        var sync = sut.AndThenResult(_ => Result.Failure("child", error));
        var async = await sut.AndThenResultAsync((_, _) =>
            Task.FromResult(Result.Failure("child", error)));
        var taskSync = await sourceTask.AndThenResult(_ => Result<string>.Failure("child", error));
        var taskAsync = await sourceTask.AndThenResultAsync((_, _) =>
            Task.FromResult(Result<string>.Failure("child", error)));

        foreach (var result in new[] { sync, async, taskSync, taskAsync })
        {
            result.IsFailure.ShouldBeTrue();
            result.Value.ShouldBe(42);
            result.Messages.ShouldBe(["outer", "child"]);
            result.Errors.ShouldBe([error]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AndThen_WhenRequiredCallbackIsMissing_PreservesValueAndReportsFailure(bool sourceFails)
    {
        var error = new NotFoundError("Missing item");
        var source = Result<int>.Success(42, "source");
        var result = sourceFails ? source.WithError(error) : source;
        var sut = Task.FromResult(result);

        var sync = await sut.AndThen(null);
        var async = await sut.AndThenAsync(null);
        var syncResult = await sut.AndThenResult(null);
        var asyncResult = await sut.AndThenResultAsync<int, string>(null);
        var directResult = result.AndThenResult(null);
        var directAsyncResult = await result.AndThenResultAsync<int, string>(null);

        foreach (var outcome in new[] { sync, async, syncResult, asyncResult, directResult, directAsyncResult })
        {
            outcome.IsFailure.ShouldBeTrue();
            outcome.Value.ShouldBe(42);
            outcome.Messages.ShouldBe(["source"]);

            var actualError = outcome.Errors.ShouldHaveSingleItem();
            if (sourceFails)
            {
                actualError.ShouldBeSameAs(error);
            }
            else
            {
                actualError.ShouldBeOfType<Error>().Message.ShouldBe("Operation cannot be null");
            }
        }
    }

    [Fact]
    public async Task AndThen_WithResultReturningSideEffect_RetainsOriginalOutcome()
    {
        var sut = Result<int>.Success(42, "source");
        var sourceTask = Task.FromResult(sut);
        var calls = 0;

        var sync = sut.AndThen(value => Record(value));
        var async = await sut.AndThenAsync((value, _) => Task.FromResult(Record(value)));
        var taskSync = await sourceTask.AndThen(value => Record(value));
        var taskAsync = await sourceTask.AndThenAsync((value, _) => Task.FromResult(Record(value)));

        calls.ShouldBe(4);
        foreach (var result in new[] { sync, async, taskSync, taskAsync })
        {
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe(42);
            result.Messages.ShouldBe(["source"]);
            result.Errors.ShouldBeEmpty();
        }

        Result Record(int value)
        {
            calls++;
            return Result.Failure($"Could not record {value}");
        }
    }

    [Fact]
    public void AndThen_WithBooleanReturningSideEffect_RetainsOriginalOutcome()
    {
        var sut = Result<int>.Success(42);
        var ran = false;
        bool Record(int _) { ran = true; return false; }

        var result = sut.AndThen(value => Record(value));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        ran.ShouldBeTrue();
    }

    [Fact]
    public async Task WithRetryPolicy_WhenSecondExceptionErrorIsRetryable_RunsFactoryAgain()
    {
        var attempts = 0;
        var timeout = new TimeoutException();
        var callbacks = new List<RetryContext>();
        var policy = RetryPolicy.Create()
            .RetryOn<TimeoutException>()
            .WithInitialDelay(TimeSpan.Zero)
            .WithMaxRetries(1)
            .OnRetry(callbacks.Add);
        Func<CancellationToken, Task<Result<int>>> sut = _ => Task.FromResult(
            ++attempts == 1
                ? Result<int>.Failure().WithError(new ExceptionError(new ArgumentException())).WithError(new ExceptionError(timeout))
                : Result<int>.Success(42, "retried"));

        var result = await sut.WithRetryPolicy(policy);

        attempts.ShouldBe(2);
        callbacks.Single().Exception.ShouldBeSameAs(timeout);
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        result.Messages.ShouldBe(["retried"]);
    }

    [Fact]
    public async Task WithRetryPolicy_WhenRetriesAreExhausted_ReturnsTerminalResult()
    {
        var attempts = 0;
        var timeout = new ExceptionError(new TimeoutException());
        var policy = RetryPolicy.Create().RetryOn<TimeoutException>().WithInitialDelay(TimeSpan.Zero).WithMaxRetries(1);
        Func<CancellationToken, Task<Result>> sut = _ =>
            Task.FromResult(Result.Failure($"attempt {++attempts}", timeout));

        var result = await sut.WithRetryPolicy(policy);

        attempts.ShouldBe(2);
        result.IsFailure.ShouldBeTrue();
        result.Messages.ShouldBe(["attempt 2"]);
        result.Errors.ShouldBe([timeout]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationHasNoTimeout_ReportsCancellation()
    {
        var options = new ResultExecuteOptions { Timeout = null };
        var canceled = new CancellationToken(true);

        var bare = await ResultAsyncExtensions.ExecuteAsync(_ => Task.FromCanceled<Result>(canceled), options);
        var typed = await ResultResilientExtensions.ExecuteAsync(_ => Task.FromCanceled<int>(canceled), options);

        bare.HasError<OperationCancelledError>().ShouldBeTrue();
        typed.HasError<OperationCancelledError>().ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRetryDelayIsCanceled_DoesNotCancelCompletedAttempt()
    {
        using var cancellation = new CancellationTokenSource();
        var attemptToken = CancellationToken.None;
        var attempts = 0;
        var options = new ResultExecuteOptions
        {
            MaxRetries = 1,
            RetryDelay = TimeSpan.FromMinutes(1),
            RetryableException = _ => { cancellation.Cancel(); return true; }
        };

        var result = await ResultAsyncExtensions.ExecuteAsync(
            ct =>
            {
                attempts++;
                attemptToken = ct;
                throw new InvalidOperationException();
            },
            options,
            cancellation.Token);

        attempts.ShouldBe(1);
        attemptToken.CanBeCanceled.ShouldBeTrue();
        attemptToken.IsCancellationRequested.ShouldBeFalse();
        result.HasError<OperationCancelledError>().ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAllBatchedAsync_WithInvalidBatchSize_RejectsInput(int batchSize)
    {
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            ResultResilientExtensions.ExecuteAllBatchedAsync<int>([], batchSize));
    }

    [Fact]
    public async Task EndOperationAsync_WhenSourceFailedBeforeStart_ReturnsOriginalFailure()
    {
        var error = new Error("original");
        var sut = Result<int>.Failure(error).WithMessage("source");
        var startCalls = 0;

        var result = await sut.StartOperation(_ =>
        {
            startCalls++;
            return Task.FromResult(new CancelOnCommitScope(null));
        }).EndOperationAsync();

        startCalls.ShouldBe(0);
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldBe([error]);
        result.Messages.ShouldBe(["source"]);
    }

    [Fact]
    public async Task EndOperationAsync_WhenCommitIsCanceled_RollsBackWithIndependentToken()
    {
        using var cancellation = new CancellationTokenSource();
        var scope = new CancelOnCommitScope(cancellation);
        var sut = Result<int>.Success(42, "source");

        var result = await sut.StartOperation(_ => Task.FromResult(scope))
            .EndOperationAsync(cancellation.Token);

        result.IsFailure.ShouldBeTrue();
        result.HasError<OperationCancelledError>().ShouldBeTrue();
        result.Messages.ShouldBe(["source"]);
        scope.RollbackCalls.ShouldBe(1);
    }

    [Fact]
    public async Task EndOperationAsync_WithAcquiredScopeAndUpstreamFailure_RollsBackScope()
    {
        var scope = new CancelOnCommitScope(null);
        var error = new Error("original");
        var sut = Result<int>.Failure(error).WithMessage("source");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await sut.StartOperation(scope).EndOperationAsync(cancellation.Token);

        result.IsFailure.ShouldBeTrue();
        result.Messages.ShouldBe(["source"]);
        result.Errors.ShouldBe([error]);
        scope.RollbackCalls.ShouldBe(1);
    }

    [Fact]
    public async Task EndOperationAsync_WithAcquiredScopeAndFaultedSourceTask_RollsBackScope()
    {
        var scope = new CancelOnCommitScope(null);
        var exception = new InvalidOperationException();
        var sut = Task.FromException<Result<int>>(exception);

        var result = await sut.StartOperation(scope).EndOperationAsync();

        result.IsFailure.ShouldBeTrue();
        result.GetError<ExceptionError>().Exception.ShouldBeSameAs(exception);
        scope.RollbackCalls.ShouldBe(1);
    }

    [Fact]
    public async Task FlattenAsync_WhenLaterTaskFaults_RetainsObservedDiagnostics()
    {
        var exception = new InvalidOperationException("faulted");
        var earlier = new Error("earlier");
        Task<Result<int>>[] scalars =
        [
            Task.FromResult(Result<int>.Failure("source", earlier)),
            Task.FromException<Result<int>>(exception)
        ];
        Task<Result<IEnumerable<int>>>[] collections =
        [
            Task.FromResult(Result<IEnumerable<int>>.Failure("source", earlier)),
            Task.FromException<Result<IEnumerable<int>>>(exception)
        ];
        Task<ResultPaged<int>>[] pages =
        [
            Task.FromResult(ResultPaged<int>.Failure("source", earlier)),
            Task.FromException<ResultPaged<int>>(exception)
        ];

        var scalar = await scalars.FlattenAsync();
        var collection = await collections.FlattenAsync();
        var paged = await pages.FlattenAsync();

        foreach (var result in new IResult[] { scalar, collection, paged })
        {
            result.IsFailure.ShouldBeTrue();
            result.Messages.ShouldBe(["source", "faulted"]);
            result.Errors.First().ShouldBeSameAs(earlier);
            result.TryGetError<ExceptionError>(out var error).ShouldBeTrue();
            error.Exception.ShouldBeSameAs(exception);
        }
    }

    [Fact]
    public async Task FlattenAsync_WhenCanceledDuringPendingTask_ReturnsFailureWithObservedDiagnostics()
    {
        using var cancellation = new CancellationTokenSource();
        var scalarPending = new TaskCompletionSource<Result<int>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var itemsPending = new TaskCompletionSource<Result<IEnumerable<int>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pagePending = new TaskCompletionSource<ResultPaged<int>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var scalars = new[] { Task.FromResult(Result<int>.Success(1).WithMessage("observed")), scalarPending.Task };
        var items = new[] { Task.FromResult(Result<IEnumerable<int>>.Success([1]).WithMessage("observed")), itemsPending.Task };
        var pages = new[] { Task.FromResult(ResultPaged<int>.Success([1]).WithMessage("observed")), pagePending.Task };

        var scalarTask = scalars.FlattenAsync(cancellationToken: cancellation.Token);
        var itemsTask = items.FlattenAsync(cancellationToken: cancellation.Token);
        var pageTask = pages.FlattenAsync(cancellationToken: cancellation.Token);
        cancellation.Cancel();

        var timeout = TimeSpan.FromSeconds(5);
        var outcomes = new IResult[]
        {
            await scalarTask.WaitAsync(timeout),
            await itemsTask.WaitAsync(timeout),
            await pageTask.WaitAsync(timeout)
        };

        foreach (var outcome in outcomes)
        {
            outcome.IsFailure.ShouldBeTrue();
            outcome.Messages.ShouldBe(["observed"]);
            outcome.HasError<OperationCancelledError>().ShouldBeTrue();
        }

        scalarPending.Task.IsCompleted.ShouldBeFalse();
        itemsPending.Task.IsCompleted.ShouldBeFalse();
        pagePending.Task.IsCompleted.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task FlattenAsync_WhenFailureLimitIsReached_StopsEnumeratingTasks(bool continueOnFailure, int expectedCount)
    {
        var options = continueOnFailure ? new ProcessingOptions { MaxFailures = 1 } : ProcessingOptions.Strict;
        var scalarCount = 0;
        var itemsCount = 0;
        var pageCount = 0;

        var scalar = await Tasks(Result<int>.Failure("failed"), () => scalarCount++).FlattenAsync(options);
        var collection = await Tasks(Result<IEnumerable<int>>.Failure("failed"), () => itemsCount++).FlattenAsync(options);
        var paged = await Tasks(ResultPaged<int>.Failure("failed"), () => pageCount++).FlattenAsync(options);

        foreach (var outcome in new IResult[] { scalar, collection, paged })
        {
            outcome.IsFailure.ShouldBeTrue();
            outcome.Errors.ShouldBeEmpty();
            outcome.Messages.ShouldContain($"Flattening aborted after {expectedCount} failures");
        }

        scalarCount.ShouldBe(expectedCount);
        itemsCount.ShouldBe(expectedCount);
        pageCount.ShouldBe(expectedCount);

        static IEnumerable<Task<TResult>> Tasks<TResult>(TResult failure, Action observe)
        {
            for (var index = 0; index < 3; index++)
            {
                observe();
                yield return Task.FromResult(failure);
            }
        }
    }

    private sealed class CancelOnCommitScope(CancellationTokenSource source) : IOperationScope
    {
        public int RollbackCalls { get; private set; }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            source.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.RollbackCalls++;
            return Task.CompletedTask;
        }
    }
}
