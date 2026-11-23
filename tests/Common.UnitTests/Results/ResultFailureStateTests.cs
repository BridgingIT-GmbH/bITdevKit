// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Results;

using FluentValidation;

[UnitTest("Common")]
[Collection(ProcessWideStateTestCollection.Name)]
public class ResultFailureStateTests
{
    [Fact]
    public Task EndOperationAsync_FailedCommitWithSuppressedDiagnostic_ReturnsFailureAfterRollback() =>
        WithExceptionErrorFactoryAsync(static _ => null, async () =>
        {
            var sut = new FailedCommitScope(new InvalidOperationException("commit failed"));
            var result = await Result<int>.Success(7, "input").StartOperation(sut).EndOperationAsync();

            result.IsFailure.ShouldBeTrue();
            result.Value.ShouldBe(7);
            result.Messages.ShouldBe(["input"]);
            result.Errors.ShouldBeEmpty();
            sut.RollbackCalls.ShouldBe(1);
        });

    [Fact]
    public async Task EndOperationAsync_ThrowingDiagnosticFactory_RollsBackBeforePropagatingTheException()
    {
        var sut = new FailedCommitScope(new InvalidOperationException("commit failed"));
        var diagnosticFailure = new InvalidOperationException("diagnostic factory failed");

        await WithExceptionErrorFactoryAsync(_ => throw diagnosticFailure, async () =>
        {
            var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
                Result<int>.Success(7).StartOperation(sut).EndOperationAsync());

            exception.ShouldBeSameAs(diagnosticFailure);
            sut.RollbackCalls.ShouldBe(1);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EndOperationAsync_ThrowingStepDiagnosticFactory_RollsBackBeforePropagatingTheException(bool useAsyncStep)
    {
        var sut = new FailedCommitScope(new InvalidOperationException("commit must not run"));
        var diagnosticFailure = new InvalidOperationException("diagnostic factory failed");
        var stepFailure = new ApplicationException("step failed");
        var nextStepCalled = false;

        await WithExceptionErrorFactoryAsync(_ => throw diagnosticFailure, async () =>
        {
            var scope = Result<int>.Success(7).StartOperation(sut);
            var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            {
                var failed = useAsyncStep
                    ? await scope.TapAsync((_, _) => Task.FromException(stepFailure))
                    : scope.Tap(_ => throw stepFailure);
                await failed.MapAsync((_, _) =>
                {
                    nextStepCalled = true;
                    return Task.FromResult("unexpected");
                }).EndOperationAsync();
            });

            exception.ShouldBeSameAs(diagnosticFailure);
            sut.RollbackCalls.ShouldBe(1);
            nextStepCalled.ShouldBeFalse();
        });
    }

    [Fact]
    public async Task EndOperationAsync_ThrowingStepDiagnosticFactory_UsesCustomRollback()
    {
        var sut = new FailedCommitScope(new InvalidOperationException("commit must not run"));
        var diagnosticFailure = new InvalidOperationException("diagnostic factory failed");
        Exception rollbackReason = null;

        await WithExceptionErrorFactoryAsync(_ => throw diagnosticFailure, async () =>
        {
            var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
                Result<int>.Success(7).StartOperation(_ => Task.FromResult(sut))
                    .TapAsync(static (_, _) => Task.FromException(new ApplicationException("step failed")))
                    .EndOperationAsync(
                        static (_, _) => throw new InvalidOperationException("custom commit must not run"),
                        (operation, error, cancellationToken) =>
                        {
                            rollbackReason = error;
                            cancellationToken.ShouldBe(CancellationToken.None);
                            return operation.RollbackAsync(cancellationToken);
                        }));

            exception.ShouldBeSameAs(diagnosticFailure);
            rollbackReason.ShouldBeSameAs(diagnosticFailure);
            sut.RollbackCalls.ShouldBe(1);
        });
    }

    [Fact]
    public async Task EndOperationAsync_ThrowingStepDiagnosticFactoryAndRollback_PropagatesBothExceptions()
    {
        var diagnosticFailure = new InvalidOperationException("diagnostic factory failed");
        var rollbackFailure = new InvalidOperationException("rollback failed");
        var sut = new FailedCommitScope(new InvalidOperationException("commit must not run"), rollbackFailure);

        await WithExceptionErrorFactoryAsync(_ => throw diagnosticFailure, async () =>
        {
            var exception = await Should.ThrowAsync<AggregateException>(() =>
                Result<int>.Success(7).StartOperation(sut)
                    .TapAsync(static (_, _) => Task.FromException(new ApplicationException("step failed")))
                    .EndOperationAsync());

            exception.InnerExceptions.ShouldBe([diagnosticFailure, rollbackFailure]);
            sut.RollbackCalls.ShouldBe(1);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TapAsync_CancellationWithAcquiredScope_SkipsCallbackAndRollsBack(bool cancelDuringAcquisition)
    {
        using var cancellation = new CancellationTokenSource();
        var operation = new FailedCommitScope(new InvalidOperationException("commit must not run"));
        var input = Result<int>.Success(7, "source");
        var callbackCalls = 0;
        var sut = cancelDuringAcquisition
            ? input.StartOperation(_ =>
            {
                cancellation.Cancel();
                return Task.FromResult(operation);
            })
            : input.StartOperation(operation);
        if (!cancelDuringAcquisition)
        {
            cancellation.Cancel();
        }

        var result = await sut.TapAsync((_, _) =>
        {
            callbackCalls++;
            return Task.CompletedTask;
        }, cancellation.Token).EndOperationAsync();

        callbackCalls.ShouldBe(0);
        operation.RollbackCalls.ShouldBe(1);
        result.IsFailure.ShouldBeTrue();
        result.Value.ShouldBe(7);
        result.Messages.ShouldBe(["source"]);
        result.Errors.ShouldHaveSingleItem().ShouldBeOfType<OperationCancelledError>();
    }

    [Fact]
    public async Task EndOperationAsync_FailedCommitAndRollback_PreservesBothDiagnostics()
    {
        var commitFailure = new InvalidOperationException("commit failed");
        var rollbackFailure = new InvalidOperationException("rollback failed");
        var sut = new FailedCommitScope(commitFailure, rollbackFailure);

        var result = await Result<int>.Success(7, "input").StartOperation(sut).EndOperationAsync();

        result.IsFailure.ShouldBeTrue();
        result.Value.ShouldBe(7);
        result.Messages.ShouldBe(["input"]);
        result.Errors.OfType<ExceptionError>().Select(error => error.Exception)
            .ShouldBe([commitFailure, rollbackFailure]);
        sut.RollbackCalls.ShouldBe(1);
    }

    [Fact]
    public Task StartOperation_FailedAcquisitionWithSuppressedDiagnostic_SkipsTheNextCallback() =>
        WithExceptionErrorFactoryAsync(static _ => null, async () =>
        {
            var sut = Result<int>.Success(7, "input").StartOperation(
                static _ => Task.FromException<FailedCommitScope>(new InvalidOperationException("acquisition failed")));
            var callbackCalls = 0;

            var scope = await sut.TapAsync((_, _) =>
            {
                callbackCalls++;
                return Task.CompletedTask;
            });
            var result = await scope.EndOperationAsync();

            callbackCalls.ShouldBe(0);
            result.IsFailure.ShouldBeTrue();
            result.Value.ShouldBe(7);
            result.Messages.ShouldBe(["input"]);
            result.Errors.ShouldBeEmpty();
        });

    [Fact]
    public Task BindResult_ThrowingBinderWithSuppressedDiagnostic_PreservesFailedInputOutcome() =>
        WithExceptionErrorFactoryAsync(static _ => null, async () =>
        {
            var sut = Result<int>.Success(7, "input");
            var sync = sut.BindResult<int, int>(static _ => throw new InvalidOperationException(), static (outer, inner) => outer + inner);
            var async = await sut.BindResultAsync<int, int>(
                static (_, _) => Task.FromException<Result<int>>(new InvalidOperationException()), static (outer, inner) => outer + inner);
            var task = await Task.FromResult(sut).BindResultAsync<int, int>(
                static (_, _) => Task.FromException<Result<int>>(new InvalidOperationException()), static (outer, inner) => outer + inner);

            foreach (var result in new[] { sync, async, task })
            {
                result.IsFailure.ShouldBeTrue();
                result.Value.ShouldBe(7);
                result.Messages.ShouldBe(["input"]);
                result.Errors.ShouldBeEmpty();
            }
        });

    [Fact]
    public Task ProcessSequenceAsync_FailedItemWithSuppressedDiagnostic_ReturnsFailure() =>
        WithExceptionErrorFactoryAsync(static _ => null, async () =>
        {
            var sut = new[] { 1, 2 };
            var result = await sut.ProcessSequenceAsync(static (item, _) =>
                item == 1 ? Task.FromResult(item) : Task.FromException<int>(new InvalidOperationException()));

            result.IsFailure.ShouldBeTrue();
            result.Errors.ShouldBeEmpty();
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidateItems_MessageOnlyFailure_FailsAccordingToProcessingLimit(bool strict)
    {
        var sut = Result<IEnumerable<int>>.Success([1, 2, 3], "input");
        var options = strict ? ProcessingOptions.Strict : ProcessingOptions.Default;

        var sync = sut.ValidateItems(Validate, options);
        var async = await sut.ValidateItemsAsync(static (item, _) => Task.FromResult(Validate(item)), options);

        foreach (var result in new[] { sync, async })
        {
            result.IsFailure.ShouldBeTrue();
            result.Errors.ShouldBeEmpty();
            if (strict)
            {
                result.Value.ShouldBeNull();
                result.Messages.ShouldBe(["input", "item 2 failed validation", "Validation aborted after 1 failures"]);
            }
            else
            {
                result.Value.ShouldBe([1, 3]);
                result.Messages.ShouldBe(["input", "item 2 failed validation"]);
            }
        }

        static Result<int> Validate(int item) =>
            item != 2 ? Result<int>.Success(item) : Result<int>.Failure(message: "item 2 failed validation");
    }

    [Fact]
    public async Task ValidateItemsWhere_InvalidPredicateWithNoDiagnostic_ReturnsFailure()
    {
        var sut = Result<IEnumerable<int>>.Success([1, 2, 3], "input");

        var sync = sut.ValidateItemsWhere(static item => item != 2, static _ => null);
        var async = await sut.ValidateItemsWhereAsync(static (item, _) => Task.FromResult(item != 2), static _ => null);

        foreach (var result in new[] { sync, async })
        {
            result.IsFailure.ShouldBeTrue();
            result.Value.ShouldBe([1, 3]);
            result.Messages.ShouldBe(["input"]);
            result.Errors.ShouldBeEmpty();
        }
    }

    [Fact]
    public Task CollectionOperators_ThrowingCallbacksWithSuppressedDiagnostic_ReturnFailure() =>
        WithExceptionErrorFactoryAsync(static _ => null, async () =>
        {
            var sut = Result<IEnumerable<int>>.Success([1, 2, 3], "input");
            var validator = new InlineValidator<int>();
            validator.RuleFor(static item => item).Must(Validate);

            var results = new[]
            {
                sut.ForEach(static item => Validate(item)),
                await sut.ForEachAsync(static (item, _) => Task.FromResult(Validate(item))),
                sut.SelectMany(static item => Validate(item) ? new[] { item } : []),
                sut.ValidateItems(validator),
                await sut.ValidateItemsAsync(validator)
            };

            foreach (var result in results)
            {
                result.IsFailure.ShouldBeTrue();
                result.Value.ShouldBe([1, 3]);
                result.Messages.ShouldContain("input");
                result.Errors.ShouldBeEmpty();
            }

            static bool Validate(int item) =>
                item != 2 ? true : throw new InvalidOperationException("item 2 failed");
        });

    private static async Task WithExceptionErrorFactoryAsync(Func<Exception, ExceptionError> factory, Func<Task> test)
    {
        var originalFactory = Result.Settings.ExceptionErrorFactory;
        try
        {
            Result.Settings.ExceptionErrorFactory = factory;
            await test();
        }
        finally
        {
            Result.Settings.ExceptionErrorFactory = originalFactory;
        }
    }

    private sealed class FailedCommitScope(Exception commitFailure, Exception rollbackFailure = null) : IOperationScope
    {
        public int RollbackCalls { get; private set; }

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.FromException(commitFailure);

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            this.RollbackCalls++;
            return rollbackFailure is null ? Task.CompletedTask : Task.FromException(rollbackFailure);
        }
    }
}
