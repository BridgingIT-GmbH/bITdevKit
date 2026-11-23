// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Results;

using FluentValidation;

[UnitTest("Common")]
[Collection(ProcessWideStateTestCollection.Name)]
public class ResultPagedOutcomeTests
{
    [Fact]
    public async Task ItemOperations_WithStatusOnlyInput_PreserveMergeMetadata()
    {
        var sut = Result.Success("validated").ToResultPaged<int>();
        var page = ResultPaged<int>.Success([1, 2], count: 95, page: 3, pageSize: 2);
        var task = Task.FromResult(sut);
        static int MapItem(int item) => throw new InvalidOperationException($"Unexpected item {item}");
        static bool FilterItem(int item) => MapItem(item) > 0;
        static Result<int> CollectItem(int item) => Result<int>.Success(MapItem(item));
        static Result<IEnumerable<int>> BindItem(int item) => Result<IEnumerable<int>>.Success([MapItem(item)]);

        var outcomes = new[]
        {
            sut.For<int>(null),
            sut.MapItems(MapItem),
            await sut.MapItemsAsync((item, _) => Task.FromResult(MapItem(item))),
            await task.MapItems(MapItem),
            await task.MapItemsAsync((item, _) => Task.FromResult(MapItem(item))),
            sut.Filter(FilterItem),
            await sut.FilterAsync((int item, CancellationToken _) => Task.FromResult(FilterItem(item))),
            await task.Filter(FilterItem),
            await task.FilterAsync((int item, CancellationToken _) => Task.FromResult(FilterItem(item))),
            sut.FilterItems(FilterItem),
            await sut.FilterItemsAsync((item, _) => Task.FromResult(FilterItem(item))),
            sut.Collect(CollectItem),
            await sut.CollectAsync((item, _) => Task.FromResult(CollectItem(item))),
            await task.Collect(CollectItem),
            await task.CollectAsync((item, _) => Task.FromResult(CollectItem(item))),
            sut.BindItems(BindItem),
            await sut.BindItemsAsync((item, _) => Task.FromResult(BindItem(item))),
            await task.BindItems(BindItem),
            await task.BindItemsAsync((item, _) => Task.FromResult(BindItem(item)))
        };

        foreach (var outcome in outcomes)
        {
            var merged = ResultPaged<int>.Merge(page, outcome);

            merged.IsSuccess.ShouldBeTrue();
            merged.Value.ShouldBe([1, 2]);
            merged.TotalCount.ShouldBe(95);
            merged.CurrentPage.ShouldBe(3);
            merged.PageSize.ShouldBe(2);
            merged.Messages.ShouldBe(["validated"]);
        }
    }

    [Fact]
    public void MapItems_WithAnEmptyPage_PreservesThePage()
    {
        var sut = ResultPaged<int>.Success([], count: 95, page: 5, pageSize: 2);
        var nextPage = ResultPaged<int>.Success([3], count: 95, page: 6, pageSize: 2);

        var projected = sut.MapItems(static item => item);
        var merged = ResultPaged<int>.Merge(nextPage, projected);

        projected.IsSuccess.ShouldBeTrue();
        projected.Value.ShouldBeEmpty();
        projected.TotalCount.ShouldBe(95);
        projected.CurrentPage.ShouldBe(5);
        projected.PageSize.ShouldBe(2);
        merged.TotalCount.ShouldBe(1);
    }

    [Fact]
    public void Map_WithAStatusOnlyInput_CanSupplyReplacementValues()
    {
        var sut = Result.Success("validated").ToResultPaged<int>();

        var projected = sut.Map(static _ => new[] { 7 });

        projected.IsSuccess.ShouldBeTrue();
        projected.Value.ShouldBe([7]);
        projected.Messages.ShouldBe(["validated"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Flatten_WithStatusBeforePage_UsesThePageMetadata(bool emptyPage)
    {
        var status = Result.Success("validated").ToResultPaged<int>();
        var items = emptyPage ? Array.Empty<int>() : [1, 2];
        var page = ResultPaged<int>.Success(items, count: 95, page: 3, pageSize: 2);
        var sut = new[] { status, page };

        var sync = sut.Flatten();
        var async = await sut.Select(Task.FromResult).FlattenAsync();

        foreach (var flattened in new[] { sync, async })
        {
            flattened.IsSuccess.ShouldBeTrue();
            flattened.Value.ShouldBe(items);
            flattened.TotalCount.ShouldBe(95);
            flattened.CurrentPage.ShouldBe(1);
            flattened.PageSize.ShouldBe(2);
            flattened.Messages.ShouldBe(["validated"]);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Flatten_WhenTotalsReachOrExceedTheLongLimit_ReportsTheCorrectOutcome(int secondTotal)
    {
        var sut = new[]
        {
            ResultPaged<int>.Success([1], count: long.MaxValue, pageSize: 2).WithMessage("first"),
            ResultPaged<int>.Success([2], count: secondTotal, pageSize: 2).WithMessage("second")
        };

        var sync = sut.Flatten();
        var async = await sut.Select(Task.FromResult).FlattenAsync();

        foreach (var result in new[] { sync, async })
        {
            result.Messages.ShouldContain("first");
            result.Messages.ShouldContain("second");
            if (secondTotal == 0)
            {
                result.IsSuccess.ShouldBeTrue();
                result.TotalCount.ShouldBe(long.MaxValue);
                result.Value.ShouldBe([1, 2]);
                result.TotalPages.ShouldBe(int.MaxValue);
            }
            else
            {
                result.IsFailure.ShouldBeTrue();
                result.GetError<ExceptionError>().Exception.ShouldBeOfType<OverflowException>();
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Flatten_WithStatusOnlyInputs_ProducesAStatusOnlyOutcome(bool fail)
    {
        var status = (fail ? Result.Failure("validated") : Result.Success("validated")).ToResultPaged<int>();
        var sut = new[] { status };
        var page = ResultPaged<int>.Success([1, 2], count: 95, page: 3, pageSize: 2);

        var sync = sut.Flatten();
        var async = await sut.Select(Task.FromResult).FlattenAsync();

        foreach (var flattened in new[] { sync, async })
        {
            var merged = ResultPaged<int>.Merge(page, flattened);

            merged.IsFailure.ShouldBe(fail);
            merged.Value.ShouldBe([1, 2]);
            merged.TotalCount.ShouldBe(95);
            merged.CurrentPage.ShouldBe(3);
            merged.PageSize.ShouldBe(2);
            merged.Messages.ShouldContain("validated");
        }
    }

    [Fact]
    public async Task AndThen_WhenPageCallbackThrowsOrCancels_PreservesPageMetadata()
    {
        var sut = ResultPaged<int>.Success([1, 2], count: 95, page: 3, pageSize: 2).WithMessage("page");
        var canceled = new CancellationToken(true);

        var sync = sut.AndThen(static _ => throw new InvalidOperationException("failed"));
        var async = await sut.AndThenAsync(static (_, _) => throw new InvalidOperationException("failed"));
        var cancellation = await sut.AndThenAsync((_, _) => Task.FromCanceled<ResultPaged<int>>(canceled));

        foreach (var outcome in new[] { sync, async, cancellation })
        {
            outcome.IsFailure.ShouldBeTrue();
            outcome.Value.ShouldBe([1, 2]);
            outcome.TotalCount.ShouldBe(95);
            outcome.CurrentPage.ShouldBe(3);
            outcome.PageSize.ShouldBe(2);
            outcome.Messages.ShouldBe(["page"]);
        }

        cancellation.HasError<OperationCancelledError>().ShouldBeTrue();
    }

    [Fact]
    public async Task AndThen_WhenStatusOnlyCallbackThrows_DoesNotAddAPage()
    {
        var sut = Result.Success("validated").ToResultPaged<int>();
        var page = ResultPaged<int>.Success([1, 2], count: 95, page: 3, pageSize: 2);

        var sync = sut.AndThen(static _ => throw new InvalidOperationException("failed"));
        var async = await sut.AndThenAsync(static (_, _) => throw new InvalidOperationException("failed"));

        foreach (var failure in new[] { sync, async })
        {
            var merged = ResultPaged<int>.Merge(page, failure);

            merged.IsFailure.ShouldBeTrue();
            merged.Value.ShouldBe([1, 2]);
            merged.TotalCount.ShouldBe(95);
            merged.CurrentPage.ShouldBe(3);
            merged.PageSize.ShouldBe(2);
            merged.Messages.ShouldBe(["validated"]);
            merged.HasError<ExceptionError>().ShouldBeTrue();
        }
    }

    [Fact]
    public async Task ItemOperations_WhenInputHasFailed_PreservePageAndDiagnostics()
    {
        var error = new Error("page failed");
        var sut = ResultPaged<int>.Success([1, 2], count: 95, page: 3, pageSize: 2)
            .WithMessage("page").WithError(error);
        var task = Task.FromResult(sut);

        var outcomes = new[]
        {
            sut.FilterItems(static _ => throw new InvalidOperationException()),
            await sut.FilterItemsAsync(static (_, _) => throw new InvalidOperationException()),
            await task.FilterItemsAsync(static (_, _) => throw new InvalidOperationException()),
            sut.MapItems<int, int>(static _ => throw new InvalidOperationException()),
            await sut.MapItemsAsync<int, int>(static (_, _) => throw new InvalidOperationException()),
            await task.MapItemsAsync<int, int>(static (_, _) => throw new InvalidOperationException())
        };

        foreach (var outcome in outcomes)
        {
            outcome.IsFailure.ShouldBeTrue();
            outcome.Value.ShouldBeEmpty();
            outcome.TotalCount.ShouldBe(95);
            outcome.CurrentPage.ShouldBe(3);
            outcome.PageSize.ShouldBe(2);
            outcome.Messages.ShouldBe(["page"]);
            outcome.Errors.ShouldBe([error]);
        }
    }

    [Fact]
    public async Task ValidateItems_WhenValidationHasOnlyMessages_ReportsFailure()
    {
        var sut = ResultPaged<int>.Success([1, 2], count: 95, page: 3, pageSize: 2);

        var sync = sut.ValidateItems(ValidateItem, ProcessingOptions.Strict);
        var async = await sut.ValidateItemsAsync(
            (item, _) => Task.FromResult(ValidateItem(item)), ProcessingOptions.Strict);
        var predicate = sut.ValidateItemsWhere(static _ => false, static _ => null, ProcessingOptions.Strict);

        foreach (var outcome in new[] { sync, async, predicate })
        {
            outcome.IsFailure.ShouldBeTrue();
            outcome.Errors.ShouldBeEmpty();
            outcome.TotalCount.ShouldBe(95);
            outcome.CurrentPage.ShouldBe(3);
            outcome.PageSize.ShouldBe(2);
        }

        sync.Messages.ShouldContain("item failed");
        async.Messages.ShouldContain("item failed");

        static Result<int> ValidateItem(int item) =>
            item == 1 ? Result<int>.Failure("item failed") : throw new InvalidOperationException("strict must stop");
    }

    [Fact]
    public async Task MapBindAndFilter_WhenExceptionFactoryReturnsNull_KeepFailureAndMetadata()
    {
        var sut = ResultPaged<int>.Success([1], count: 95, page: 3, pageSize: 2).WithMessage("page");
        var originalFactory = Result.Settings.ExceptionErrorFactory;
        Result.Settings.ExceptionErrorFactory = static _ => null;

        try
        {
            var failures = new[]
            {
                sut.Map<int, int>(static _ => throw new InvalidOperationException()),
                await sut.MapAsync<int, int>(static (_, _) => throw new InvalidOperationException()),
                sut.Bind<int, int>(static _ => throw new InvalidOperationException()),
                await sut.BindAsync<int, int>(static (_, _) => throw new InvalidOperationException()),
                sut.Filter(static (IEnumerable<int> _) => throw new InvalidOperationException()),
                await sut.FilterAsync(static (IEnumerable<int> _, CancellationToken _) => throw new InvalidOperationException()),
                sut.Filter(static (int _) => throw new InvalidOperationException()),
                await sut.FilterAsync(static (int _, CancellationToken _) => throw new InvalidOperationException())
            };

            foreach (var outcome in failures)
            {
                outcome.IsFailure.ShouldBeTrue();
                outcome.TotalCount.ShouldBe(95);
                outcome.CurrentPage.ShouldBe(3);
                outcome.PageSize.ShouldBe(2);
                outcome.Messages.ShouldBe(["page"]);
                outcome.Errors.ShouldBeEmpty();
            }
        }
        finally
        {
            Result.Settings.ExceptionErrorFactory = originalFactory;
        }
    }

    [Fact]
    public async Task Validate_WhenPageTaskHasAnInvalidItem_PreservesPageMetadata()
    {
        var sut = Task.FromResult(ResultPaged<int>.Success([1, 2], count: 95, page: 3, pageSize: 2).WithMessage("page"));
        var validator = new InlineValidator<int>();
        validator.RuleFor(item => item).GreaterThan(1);

        var sync = await sut.Validate(validator);
        var async = await sut.ValidateAsync(validator);

        foreach (var outcome in new[] { sync, async })
        {
            outcome.IsFailure.ShouldBeTrue();
            outcome.Value.ShouldBe([1, 2]);
            outcome.TotalCount.ShouldBe(95);
            outcome.CurrentPage.ShouldBe(3);
            outcome.PageSize.ShouldBe(2);
            outcome.Messages.ShouldBe(["page"]);
            outcome.Errors.ShouldHaveSingleItem().ShouldBeOfType<FluentValidationError>();
        }
    }

    [Fact]
    public async Task SelectManyAndForEach_WhenExceptionFactoryReturnsNull_ReportFailure()
    {
        var sut = ResultPaged<int>.Success([1, 2], count: 95, page: 3, pageSize: 2);
        var originalFactory = Result.Settings.ExceptionErrorFactory;

        try
        {
            Result.Settings.ExceptionErrorFactory = static _ => null;
            var selected = sut.SelectMany<int, int>(static _ => throw new InvalidOperationException("failed"));
            var processed = sut.ForEach(static _ => throw new InvalidOperationException("failed"));
            var processedAsync = await sut.ForEachAsync(static (_, _) => throw new InvalidOperationException("failed"));

            foreach (var outcome in new[] { selected, processed, processedAsync })
            {
                outcome.IsFailure.ShouldBeTrue();
                outcome.Errors.ShouldBeEmpty();
                outcome.TotalCount.ShouldBe(95);
                outcome.CurrentPage.ShouldBe(3);
                outcome.PageSize.ShouldBe(2);
            }
        }
        finally
        {
            Result.Settings.ExceptionErrorFactory = originalFactory;
        }
    }
}
