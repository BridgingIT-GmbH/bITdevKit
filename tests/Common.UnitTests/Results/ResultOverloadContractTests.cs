// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Results;

[UnitTest("Common")]
public class ResultOverloadContractTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonGenericComposition_SuccessOrMessageOnlyFailure_PreservesOutcome(bool sourceFails)
    {
        var sut = sourceFails ? Result.Failure("source") : Result.Success("source");
        var task = Task.FromResult(sut);
        var callbacks = 0;

        var mapped = new[] { sut.Map(Map), await task.Map(Map) };
        var continued = new[]
        {
            sut.AndThen(Step),
            await sut.AndThenAsync(_ => Task.FromResult(Step())),
            await task.AndThen(Step),
            await task.AndThenAsync(_ => Task.FromResult(Step()))
        };

        foreach (var result in mapped)
        {
            result.IsFailure.ShouldBe(sourceFails);
            result.Value.ShouldBe(sourceFails ? null : "mapped");
            result.Messages.ShouldBe(["source"]);
            result.Errors.ShouldBeEmpty();
        }

        foreach (var result in continued)
        {
            result.IsFailure.ShouldBeTrue();
            result.Messages.ShouldBe(sourceFails ? ["source"] : ["source", "step"]);
            result.Errors.ShouldBeEmpty();
        }

        callbacks.ShouldBe(sourceFails ? 0 : 6);

        string Map()
        {
            callbacks++;
            return "mapped";
        }

        Result Step()
        {
            callbacks++;
            return Result.Failure("step");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TypedComposition_SuccessOrMessageOnlyFailure_PreservesOutcome(bool sourceFails)
    {
        var sut = sourceFails ? Result<int>.Failure(7, "source") : Result<int>.Success(7, "source");
        var sequence = sourceFails
            ? Result<IEnumerable<int>>.Failure([7, 8], "source")
            : Result<IEnumerable<int>>.Success([7, 8], "source");
        var task = Task.FromResult(sut);
        var sequenceTask = Task.FromResult(sequence);
        var callbackCalls = 0;
        var collectedInputs = new List<int>();

        var mapped = new[]
        {
            sut.Map(Map), await sut.MapAsync((value, _) => Task.FromResult(Map(value))),
            await task.Map(Map), await task.MapAsync((value, _) => Task.FromResult(Map(value)))
        };
        var bound = new[]
        {
            sut.Bind<int, string>(Bind), await sut.BindAsync((value, _) => Task.FromResult(Bind(value))),
            await task.Bind<int, string>(Bind), await task.BindAsync((value, _) => Task.FromResult(Bind(value)))
        };
        var continued = new[]
        {
            sut.AndThenResult(Step), await sut.AndThenResultAsync((value, _) => Task.FromResult(Step(value))),
            await task.AndThenResult(Step), await task.AndThenResultAsync((value, _) => Task.FromResult(Step(value)))
        };
        var collected = new[]
        {
            sequence.Collect(Collect), await sequence.CollectAsync((value, _) => Task.FromResult(Collect(value))),
            await sequenceTask.Collect(Collect), await sequenceTask.CollectAsync((value, _) => Task.FromResult(Collect(value)))
        };

        foreach (var result in mapped)
        {
            result.IsFailure.ShouldBe(sourceFails);
            result.Value.ShouldBe(sourceFails ? null : "mapped");
            result.Messages.ShouldBe(["source"]);
            result.Errors.ShouldBeEmpty();
        }

        foreach (var result in bound)
        {
            AssertMessageOnlyFailure(result, sourceFails ? ["source"] : ["source", "bind"]);
            result.Value.ShouldBe(sourceFails ? null : "partial");
        }

        foreach (var result in continued)
        {
            AssertMessageOnlyFailure(result, sourceFails ? ["source"] : ["source", "step"]);
            result.Value.ShouldBe(7);
        }

        foreach (var result in collected)
        {
            AssertMessageOnlyFailure(result, sourceFails ? ["source"] : ["source", "item 7", "item 8"]);
        }

        callbackCalls.ShouldBe(sourceFails ? 0 : 12);
        collectedInputs.ShouldBe(sourceFails ? [] : [7, 8, 7, 8, 7, 8, 7, 8]);

        string Map(int value)
        {
            callbackCalls++;
            value.ShouldBe(7);
            return "mapped";
        }

        Result<string> Bind(int value)
        {
            callbackCalls++;
            value.ShouldBe(7);
            return Result<string>.Failure(value: "partial", message: "bind");
        }

        Result Step(int value)
        {
            callbackCalls++;
            value.ShouldBe(7);
            return Result.Failure("step");
        }

        Result<string> Collect(int value)
        {
            collectedInputs.Add(value);
            return value == 7
                ? Result<string>.Failure(message: "item 7")
                : Result<string>.Success("second", "item 8");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PagedComposition_SuccessOrMessageOnlyFailure_PreservesTheAuthoritativePage(bool sourceFails)
    {
        var sut = ResultPaged<int>.Success([7, 8], count: 42, page: 3, pageSize: 5)
            .WithOutcome(sourceFails ? Result.Failure("source") : Result.Success("source"));
        var task = Task.FromResult(sut);
        var callbackCalls = 0;

        var mapped = new[]
        {
            sut.Map(Map), await sut.MapAsync((values, _) => Task.FromResult(Map(values))),
            await task.Map(Map), await task.MapAsync((values, _) => Task.FromResult(Map(values)))
        };
        var bound = new[]
        {
            sut.Bind(Bind), await sut.BindAsync((values, _) => Task.FromResult(Bind(values))),
            await task.Bind(Bind), await task.BindAsync((values, _) => Task.FromResult(Bind(values)))
        };
        var continued = new[]
        {
            sut.AndThen(Step), await sut.AndThenAsync((values, _) => Task.FromResult(Step(values))),
            await task.AndThen(Step), await task.AndThenAsync((values, _) => Task.FromResult(Step(values)))
        };
        var collected = new[]
        {
            sut.Collect(Collect), await sut.CollectAsync((value, _) => Task.FromResult(Collect(value))),
            await task.Collect(Collect), await task.CollectAsync((value, _) => Task.FromResult(Collect(value)))
        };

        foreach (var result in mapped)
        {
            result.IsFailure.ShouldBe(sourceFails);
            result.Value.ShouldBe(sourceFails ? [] : ["mapped"]);
            result.Messages.ShouldBe(["source"]);
            result.Errors.ShouldBeEmpty();
            AssertPage(result, 42, 3, 5);
        }

        foreach (var result in bound)
        {
            AssertMessageOnlyFailure(result, sourceFails ? ["source"] : ["source", "bind"]);
            result.Value.ShouldBe(sourceFails ? [] : ["partial"]);
            AssertPage(result, sourceFails ? 42 : 12, sourceFails ? 3 : 2, sourceFails ? 5 : 4);
        }

        foreach (var result in continued)
        {
            result.IsFailure.ShouldBeTrue();
            result.Value.ShouldBe(sourceFails ? [7, 8] : [9]);
            result.Messages.ShouldBe(sourceFails ? ["source"] : ["source", "step"], ignoreOrder: true);
            result.Errors.ShouldBeEmpty();
            AssertPage(result, sourceFails ? 42 : 12, sourceFails ? 3 : 2, sourceFails ? 5 : 4);
        }

        foreach (var result in collected)
        {
            AssertMessageOnlyFailure(result, sourceFails ? ["source"] : ["source", "item 7", "item 8"]);
            result.Value.ShouldBeEmpty();
            AssertPage(result, 42, 3, 5);
        }

        callbackCalls.ShouldBe(sourceFails ? 0 : 20);

        IEnumerable<string> Map(IEnumerable<int> values)
        {
            callbackCalls++;
            values.ShouldBe([7, 8]);
            return ["mapped"];
        }

        ResultPaged<string> Bind(IEnumerable<int> values)
        {
            callbackCalls++;
            values.ShouldBe([7, 8]);
            return ResultPaged<string>.Success(["partial"], count: 12, page: 2, pageSize: 4)
                .WithOutcome(Result.Failure("bind"));
        }

        ResultPaged<int> Step(IEnumerable<int> values)
        {
            callbackCalls++;
            values.ShouldBe([7, 8]);
            return ResultPaged<int>.Success([9], count: 12, page: 2, pageSize: 4).WithOutcome(Result.Failure("step"));
        }

        Result<string> Collect(int value)
        {
            callbackCalls++;
            return value == 7
                ? Result<string>.Failure(message: "item 7")
                : Result<string>.Success("second", "item 8");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TaskComposition_FaultedOrCanceledSource_SkipsEveryCallback(bool canceled)
    {
        var sourceError = new InvalidOperationException("source fault");
        var sut = CreateSource<Result>();
        var typed = CreateSource<Result<int>>();
        var sequence = CreateSource<Result<IEnumerable<int>>>();
        var paged = CreateSource<ResultPaged<int>>();
        var callbacks = 0;

        IResult[] results =
        [
            await sut.Map("mapped"),
            await sut.Map(() => Unexpected<string>()),
            await sut.AndThen(() => Unexpected<Result>()),
            await sut.AndThenAsync(_ => Unexpected<Task<Result>>()),
            await typed.Map(_ => Unexpected<string>()),
            await typed.MapAsync((_, _) => Unexpected<Task<string>>()),
            await typed.Bind<int, string>(_ => Unexpected<Result<string>>()),
            await typed.BindAsync((_, _) => Unexpected<Task<Result<string>>>()),
            await typed.AndThenResult(_ => Unexpected<Result>()),
            await typed.AndThenResultAsync((_, _) => Unexpected<Task<Result>>()),
            await sequence.Collect(_ => Unexpected<Result<string>>()),
            await sequence.CollectAsync((_, _) => Unexpected<Task<Result<string>>>()),
            await paged.Map(_ => Unexpected<IEnumerable<string>>()),
            await paged.MapAsync((_, _) => Unexpected<Task<IEnumerable<string>>>()),
            await paged.Bind(_ => Unexpected<ResultPaged<string>>()),
            await paged.BindAsync((_, _) => Unexpected<Task<ResultPaged<string>>>()),
            await paged.AndThen(_ => Unexpected<ResultPaged<int>>()),
            await paged.AndThenAsync((_, _) => Unexpected<Task<ResultPaged<int>>>()),
            await paged.Collect(_ => Unexpected<Result<string>>()),
            await paged.CollectAsync((_, _) => Unexpected<Task<Result<string>>>())
        ];

        callbacks.ShouldBe(0);
        foreach (var result in results)
        {
            result.IsFailure.ShouldBeTrue();
            if (canceled)
            {
                result.Errors.Single().ShouldBeOfType<OperationCancelledError>();
            }
            else
            {
                result.Errors.Single().ShouldBeOfType<ExceptionError>().Exception.ShouldBeSameAs(sourceError);
                result.Messages.ShouldContain(sourceError.Message);
            }
        }

        Task<T> CreateSource<T>() => canceled
            ? Task.FromCanceled<T>(new CancellationToken(true))
            : Task.FromException<T>(sourceError);

        T Unexpected<T>()
        {
            callbacks++;
            throw new InvalidOperationException("The source did not succeed");
        }
    }

    [Fact]
    public async Task MapAndAndThenAsync_PreCanceledToken_SkipCallbacks()
    {
        var cancellation = new CancellationToken(true);
        var sut = Result<int>.Success(7, "source");
        var page = ResultPaged<int>.Success([7], count: 30, page: 2, pageSize: 3).WithMessage("source");
        var callbackCalls = 0;

        var mapped = await sut.MapAsync(Map, cancellation);
        var mappedTask = await Task.FromResult(sut).MapAsync(Map, cancellation);
        var continued = await page.AndThenAsync(Continue, cancellation);
        var continuedTask = await Task.FromResult(page).AndThenAsync(Continue, cancellation);

        callbackCalls.ShouldBe(0);
        foreach (var result in new IResult[] { mapped, mappedTask, continued, continuedTask })
        {
            result.IsFailure.ShouldBeTrue();
            result.Errors.ShouldHaveSingleItem().ShouldBeOfType<OperationCancelledError>();
            result.Messages.ShouldBe(["source"]);
        }

        foreach (var result in new[] { continued, continuedTask })
        {
            result.Value.ShouldBe([7]);
            AssertPage(result, 30, 2, 3);
        }

        Task<string> Map(int _, CancellationToken __)
        {
            callbackCalls++;
            return Task.FromResult("mapped");
        }

        Task<ResultPaged<int>> Continue(IEnumerable<int> _, CancellationToken __)
        {
            callbackCalls++;
            return Task.FromResult(page);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Bind_ValueReturningCallback_ConvertsSourceTaskFailure(bool canceled)
    {
        var sourceError = new InvalidOperationException("source fault");
        var sut = canceled
            ? Task.FromCanceled<Result<int>>(new CancellationToken(true))
            : Task.FromException<Result<int>>(sourceError);
        var callbackCalls = 0;

        var sync = await sut.Bind(Transform);
        var async = await sut.BindAsync((value, _) => Task.FromResult(Transform(value)));

        callbackCalls.ShouldBe(0);
        foreach (var result in new[] { sync, async })
        {
            result.IsFailure.ShouldBeTrue();
            var error = result.Errors.ShouldHaveSingleItem();
            if (canceled)
            {
                error.ShouldBeOfType<OperationCancelledError>();
            }
            else
            {
                error.ShouldBeOfType<ExceptionError>().Exception.ShouldBeSameAs(sourceError);
                result.Messages.ShouldBe([sourceError.Message]);
            }
        }

        int Transform(int value)
        {
            callbackCalls++;
            return value + 1;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Bind_ValueReturningCallback_PreservesDiagnostics(bool callbackFails)
    {
        var sut = Result<int>.Success(7, "source");
        var callbackError = new InvalidOperationException("callback fault");

        var direct = sut.Bind(Transform);
        var directAsync = await sut.BindAsync(value => Task.FromResult(Transform(value)));
        var task = await Task.FromResult(sut).Bind(Transform);
        var taskAsync = await Task.FromResult(sut).BindAsync((value, _) => Task.FromResult(Transform(value)));

        foreach (var result in new[] { direct, directAsync, task, taskAsync })
        {
            result.IsFailure.ShouldBe(callbackFails);
            result.Messages.ShouldBe(["source"]);
            if (callbackFails)
            {
                result.Errors.ShouldHaveSingleItem().ShouldBeOfType<ExceptionError>().Exception.ShouldBeSameAs(callbackError);
            }
            else
            {
                result.Value.ShouldBe(8);
                result.Errors.ShouldBeEmpty();
            }
        }

        int Transform(int value) => callbackFails ? throw callbackError : value + 1;
    }

    [Fact]
    public async Task BindAsync_ValueReturningCallback_PreCanceledToken_SkipsCallback()
    {
        var sut = Task.FromResult(Result<int>.Success(7, "source"));
        var callbackCalls = 0;

        var result = await sut.BindAsync((value, _) =>
        {
            callbackCalls++;
            return Task.FromResult(value + 1);
        }, new CancellationToken(true));

        callbackCalls.ShouldBe(0);
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().ShouldBeOfType<OperationCancelledError>();
        result.Messages.ShouldBe(["source"]);
    }

    [Fact]
    public async Task Map_NullMapper_PreservesExistingFailureInEveryForm()
    {
        var sut = Result<int>.Failure(7, "original failure");
        var untyped = Result.Failure("original failure");

        var results = new[]
        {
            sut.Map<int, string>(null),
            await sut.MapAsync<int, string>(null),
            await Task.FromResult(sut).Map<int, string>(null),
            await Task.FromResult(sut).MapAsync<int, string>(null),
            untyped.Map<string>((Func<string>)null),
            await Task.FromResult(untyped).Map<string>((Func<string>)null)
        };

        foreach (var result in results)
        {
            AssertMessageOnlyFailure(result, ["original failure"]);
        }
    }

    [Fact]
    public async Task Map_NullMapperOnSuccessfulSource_ReturnsMapperDiagnostic()
    {
        var sut = Result<int>.Success(7, "source");
        var untyped = Result.Success("source");

        var results = new[]
        {
            sut.Map<int, string>(null),
            await sut.MapAsync<int, string>(null),
            await Task.FromResult(sut).Map<int, string>(null),
            await Task.FromResult(sut).MapAsync<int, string>(null),
            untyped.Map<string>((Func<string>)null),
            await Task.FromResult(untyped).Map<string>((Func<string>)null)
        };

        foreach (var result in results)
        {
            result.IsFailure.ShouldBeTrue();
            result.Messages.ShouldBe(["source"]);
            result.Errors.ShouldHaveSingleItem().Message.ShouldBe("Mapper cannot be null");
        }
    }

    private static void AssertMessageOnlyFailure(IResult result, string[] messages)
    {
        result.IsFailure.ShouldBeTrue();
        result.Messages.ShouldBe(messages);
        result.Errors.ShouldBeEmpty();
    }

    private static void AssertPage<T>(ResultPaged<T> result, long totalCount, int currentPage, int pageSize)
    {
        result.TotalCount.ShouldBe(totalCount);
        result.CurrentPage.ShouldBe(currentPage);
        result.PageSize.ShouldBe(pageSize);
    }
}
