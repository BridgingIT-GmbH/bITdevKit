// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Results;

[UnitTest("Common")]
[Collection(ProcessWideStateTestCollection.Name)]
public class ResultResilienceContractTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ProcessSequenceAsync_WithCallerCancellation_StopsReadingTheSource(bool cancelBeforeStart, bool itemIgnoresCancellation)
    {
        using var cancellation = new CancellationTokenSource();
        var itemsRead = 0;
        var sut = ReadItems();
        if (cancelBeforeStart)
        {
            cancellation.Cancel();
        }

        var result = await sut.ProcessSequenceAsync((item, token) =>
        {
            if (item == 1)
            {
                return Task.FromException<int>(new InvalidOperationException("first item failed"));
            }

            cancellation.Cancel();
            return itemIgnoresCancellation ? Task.FromResult(item) : Task.FromCanceled<int>(token);
        }, cancellationToken: cancellation.Token);

        result.IsFailure.ShouldBeTrue();
        itemsRead.ShouldBe(cancelBeforeStart ? 0 : 2);
        result.GetErrors<OperationCancelledError>().Count().ShouldBe(1);
        result.HasError<ExceptionError>().ShouldBe(!cancelBeforeStart);

        IEnumerable<int> ReadItems()
        {
            itemsRead++;
            yield return 1;
            itemsRead++;
            yield return 2;
            throw new InvalidOperationException("Source was read after cancellation");
        }
    }

    [Fact]
    public async Task ExecuteAllBatchedAsync_CallerCancelsBeforeNextBatch_SkipsLaterOperations()
    {
        using var cancellation = new CancellationTokenSource();
        var finishFirstBatch = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstBatchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var laterOperationInvoked = false;
        Func<CancellationToken, Task<int>>[] operations =
        [
            _ => Task.FromResult(1),
            _ =>
            {
                firstBatchStarted.SetResult();
                return finishFirstBatch.Task;
            },
            _ =>
            {
                laterOperationInvoked = true;
                return Task.FromResult(3);
            }
        ];
        var waitLimit = TimeSpan.FromSeconds(5);

        var execution = ResultResilientExtensions.ExecuteAllBatchedAsync(operations, 2, cancellationToken: cancellation.Token);
        try
        {
            await firstBatchStarted.Task.WaitAsync(waitLimit);
            cancellation.Cancel();
        }
        finally
        {
            finishFirstBatch.SetResult(2);
        }

        var result = await execution.WaitAsync(waitLimit);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().ShouldBeOfType<OperationCancelledError>();
        laterOperationInvoked.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAllBatchedAsync_LazySourceWithCancellation_DoesNotReadLaterBatches(bool cancelBeforeStart)
    {
        using var cancellation = new CancellationTokenSource();
        var reads = 0;
        var starts = 0;
        var sut = ReadOperations();
        if (cancelBeforeStart)
        {
            cancellation.Cancel();
        }

        var result = await ResultResilientExtensions.ExecuteAllBatchedAsync(sut, 1, cancellationToken: cancellation.Token);

        reads.ShouldBe(cancelBeforeStart ? 0 : 1);
        starts.ShouldBe(cancelBeforeStart ? 0 : 1);
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().ShouldBeOfType<OperationCancelledError>();

        IEnumerable<Func<CancellationToken, Task<int>>> ReadOperations()
        {
            reads++;
            yield return _ =>
            {
                starts++;
                cancellation.Cancel();
                return Task.FromResult(1);
            };
            throw new InvalidOperationException("Another batch was read after cancellation");
        }
    }

    [Fact]
    public async Task ExecuteAllBatchedAsync_LazySourceThrowsAfterFirstBatch_ReportsFailureAfterRunningTheFirstBatch()
    {
        var sourceError = new InvalidOperationException("Source failed");
        var starts = 0;
        var sut = ReadOperations();

        var result = await ResultResilientExtensions.ExecuteAllBatchedAsync(sut, 1);

        starts.ShouldBe(1);
        result.IsFailure.ShouldBeTrue();
        result.Messages.ShouldBe([sourceError.Message]);
        result.Errors.ShouldHaveSingleItem().ShouldBeOfType<ExceptionError>().Exception.ShouldBeSameAs(sourceError);

        IEnumerable<Func<CancellationToken, Task<int>>> ReadOperations()
        {
            yield return _ =>
            {
                starts++;
                return Task.FromResult(1);
            };
            throw sourceError;
        }
    }

    [Fact]
    public async Task ExecuteAllBatchedAsync_LazySourceWithPartialLastBatch_ReturnsValuesInOrder()
    {
        var completed = 0;
        var sut = ReadOperations();

        var result = await ResultResilientExtensions.ExecuteAllBatchedAsync(sut, 2);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe([1, 2, 3]);

        IEnumerable<Func<CancellationToken, Task<int>>> ReadOperations()
        {
            yield return _ => Complete(1);
            yield return _ => Complete(2);
            if (Volatile.Read(ref completed) != 2)
            {
                throw new InvalidOperationException("The source was read before the first batch finished");
            }

            yield return _ => Complete(3);
        }

        Task<int> Complete(int value)
        {
            Interlocked.Increment(ref completed);
            return Task.FromResult(value);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_TimeoutOrCallerCancellation_ReportsTheCancellationSource(bool cancelCaller)
    {
        using var cancellation = new CancellationTokenSource();
        var options = new ResultExecuteOptions
        {
            Timeout = cancelCaller ? TimeSpan.FromMinutes(1) : TimeSpan.Zero
        };

        var result = await ResultResilientExtensions.ExecuteAsync(async token =>
        {
            if (cancelCaller)
            {
                cancellation.Cancel();
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return 1;
        }, options, cancellation.Token).WaitAsync(TimeSpan.FromSeconds(5));

        result.IsFailure.ShouldBeTrue();
        var error = result.Errors.ShouldHaveSingleItem();
        if (cancelCaller)
        {
            error.ShouldBeOfType<OperationCancelledError>();
        }
        else
        {
            error.ShouldBeOfType<TimeoutError>();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteWithBackgroundAsync_IndependentlyCanceledBackground_PreservesMainOutcome(bool throwsCancellation)
    {
        var backgroundStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var backgroundCancellation = new CancellationToken(true);
        var sut = async (CancellationToken cancellationToken) =>
        {
            await backgroundStarted.Task.WaitAsync(cancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            return Result.Success().WithMessage("main finished");
        };

        var result = await ResultAsyncExtensions.ExecuteWithBackgroundAsync(sut, _ =>
        {
            backgroundStarted.SetResult();
            if (throwsCancellation)
            {
                throw new OperationCanceledException(backgroundCancellation);
            }

            return Task.FromCanceled(backgroundCancellation);
        });

        result.IsSuccess.ShouldBeTrue();
        result.Messages.ShouldBe(["main finished"]);
    }

    [Fact]
    public async Task ExecuteWithBackgroundAsync_FaultedBackground_PropagatesException()
    {
        var backgroundStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("background failed");
        var sut = async (CancellationToken cancellationToken) =>
        {
            await backgroundStarted.Task.WaitAsync(cancellationToken);
            return Result.Success();
        };

        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            ResultAsyncExtensions.ExecuteWithBackgroundAsync(sut, _ =>
            {
                backgroundStarted.SetResult();
                return Task.FromException(failure);
            }));

        exception.ShouldBeSameAs(failure);
    }

    [Fact]
    public async Task ExecuteWithBackgroundAsync_BackgroundFaultsDuringCleanup_AwaitsAndPropagatesException()
    {
        var backgroundStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("background cleanup failed");
        var waitLimit = TimeSpan.FromSeconds(5);

        var execution = ResultAsyncExtensions.ExecuteWithBackgroundAsync(
            async token =>
            {
                await backgroundStarted.Task.WaitAsync(token);
                return Result.Success();
            },
            async token =>
            {
                using var registration = token.Register(() => cleanupStarted.TrySetResult());
                backgroundStarted.SetResult();
                await cleanupStarted.Task;
                await finishCleanup.Task;
                throw failure;
            });

        try
        {
            await cleanupStarted.Task.WaitAsync(waitLimit);
            execution.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            finishCleanup.SetResult();
        }

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => execution.WaitAsync(waitLimit));
        exception.ShouldBeSameAs(failure);
    }

    [Fact]
    public async Task ExecuteWithBackgroundAsync_CanceledCaller_ReportsOperationCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var sut = (CancellationToken cancellationToken) =>
        {
            cancellation.Cancel();
            return Task.FromCanceled<int>(cancellationToken);
        };

        var result = await ResultResilientExtensions.ExecuteWithBackgroundAsync(
            sut,
            cancellationToken => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken),
            cancellationToken: cancellation.Token);

        result.IsFailure.ShouldBeTrue();
        result.HasError<OperationCancelledError>().ShouldBeTrue();
    }

    [Fact]
    public async Task WithRetryPolicy_RedactedThrownException_RetriesAndPreservesTerminalDiagnostic()
    {
        var originalFactory = Result.Settings.ExceptionErrorFactory;
        var redactedError = new ExceptionError(null, "An operation failed");
        var timeout = new TimeoutException("retryable");
        var callbacks = new List<RetryContext>();
        var attempts = 0;
        var sut = (CancellationToken _) =>
        {
            attempts++;
            return Task.FromException<Result>(timeout);
        };
        var policy = RetryPolicy.Create()
            .RetryOn<TimeoutException>()
            .WithInitialDelay(TimeSpan.Zero)
            .WithMaxRetries(1)
            .OnRetry(callbacks.Add);

        try
        {
            Result.Settings.ExceptionErrorFactory = _ => redactedError;
            var result = await sut.WithRetryPolicy(policy);

            attempts.ShouldBe(2);
            callbacks.Single().Exception.ShouldBeSameAs(timeout);
            result.IsFailure.ShouldBeTrue();
            result.Errors.ShouldBe([redactedError]);
        }
        finally
        {
            Result.Settings.ExceptionErrorFactory = originalFactory;
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task WithRetryPolicy_AggregateException_MatchesAggregateOrInnerException(bool throwAggregate, bool matchAggregate)
    {
        var timeout = new TimeoutException("retryable");
        var aggregate = new AggregateException(new InvalidOperationException(), new AggregateException(timeout));
        var callbacks = new List<RetryContext>();
        var attempts = 0;
        var policy = RetryPolicy.Create().WithInitialDelay(TimeSpan.Zero).WithMaxRetries(1).OnRetry(callbacks.Add);
        if (matchAggregate)
        {
            policy.RetryOn<AggregateException>();
        }
        else
        {
            policy.RetryOn<TimeoutException>();
        }

        var sut = (CancellationToken _) =>
        {
            if (++attempts > 1)
            {
                return Task.FromResult(Result<int>.Success(42));
            }

            return throwAggregate
                ? Task.FromException<Result<int>>(aggregate)
                : Task.FromResult(Result<int>.Failure(new ExceptionError(aggregate)));
        };

        var result = await sut.WithRetryPolicy(policy);

        attempts.ShouldBe(2);
        callbacks.Single().Exception.ShouldBeSameAs(matchAggregate ? (Exception)aggregate : timeout);
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }
}
