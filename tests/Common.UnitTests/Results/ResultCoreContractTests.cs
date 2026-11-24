// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Results;

using System.Text.Json;

[UnitTest("Common")]
public class ResultCoreContractTests
{
    [Theory]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    public void WithDiagnostics_LargeSequence_PreservesOrderAndCount(int count)
    {
        var messages = Enumerable.Range(1, count).Select(i => $"message {i}").ToArray();
        var errors = messages.Select(message => new Error(message)).ToArray();

        IResult[] results =
        [
            Result.Success().WithMessages(messages).WithErrors(errors),
            Result<int>.Success(7).WithMessages(messages).WithErrors(errors),
            ResultPaged<int>.Success([7]).WithMessages(messages).WithErrors(errors)
        ];

        foreach (var sut in results)
        {
            sut.IsFailure.ShouldBeTrue();
            sut.HasError().ShouldBeTrue();
            sut.Messages.ShouldBe(messages);
            sut.Errors.Select(error => error.Message).ShouldBe(messages);
        }
    }

    [Fact]
    public void WithErrors_SingleUseSequenceWithNulls_OnlyAddsRealErrors()
    {
        var error = new ValidationError("invalid", "id");
        var plain = Result.Success();
        var scalar = Result<int>.Success(7);
        var paged = ResultPaged<int>.Success([7], count: 35, page: 2, pageSize: 3);

        IResult[] results =
        [
            plain.WithErrors(Once<IResultError>([null, error, null])),
            scalar.WithErrors(Once<IResultError>([null, error, null])),
            paged.WithErrors(Once<IResultError>([null, error, null]))
        ];

        foreach (var sut in results)
        {
            sut.IsFailure.ShouldBeTrue();
            sut.Errors.ShouldHaveSingleItem().ShouldBeSameAs(error);
            sut.HasError<ValidationError>().ShouldBeTrue();
        }

        plain.WithErrors([null]).IsSuccess.ShouldBeTrue();
        scalar.WithErrors([null]).IsSuccess.ShouldBeTrue();
        paged.WithErrors([null]).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ErrorLookups_DerivedError_MatchAssignableType()
    {
        var error = new MappingError(new InvalidOperationException("map failed"));
        var plain = Result.Failure(error);
        var scalar = Result<int>.Failure(error);
        var paged = ResultPaged<int>.Failure(error);

        foreach (var sut in new IResult[] { plain, scalar, paged })
        {
            sut.HasError<ExceptionError>().ShouldBeTrue();
            sut.TryGetError<ExceptionError>(out var match).ShouldBeTrue();
            match.ShouldBeSameAs(error);
            sut.TryGetErrors<ExceptionError>(out var matches).ShouldBeTrue();
            matches.ShouldHaveSingleItem().ShouldBeSameAs(error);
        }

        plain.GetError<ExceptionError>().ShouldBeSameAs(error);
        scalar.GetError<ExceptionError>().ShouldBeSameAs(error);
        paged.GetError<ExceptionError>().ShouldBeSameAs(error);
        plain.GetErrors<ExceptionError>().ShouldHaveSingleItem().ShouldBeSameAs(error);
        scalar.GetErrors<ExceptionError>().ShouldHaveSingleItem().ShouldBeSameAs(error);
        paged.GetErrors<ExceptionError>().ShouldHaveSingleItem().ShouldBeSameAs(error);
    }

    [Fact]
    public void WithError_Exception_PreservesScalarAndPagedPayloads()
    {
        var sut = Result<int>.Success(42, "loaded");
        var exception = new InvalidOperationException("processing failed");

        var result = sut.WithError(exception);

        result.IsFailure.ShouldBeTrue();
        result.Value.ShouldBe(42);
        result.Messages.ShouldBe(["loaded"]);
        result.GetError<ExceptionError>().Exception.ShouldBeSameAs(exception);

        var paged = ResultPaged<int>.Success([42], "loaded", count: 35, page: 2, pageSize: 3).WithError(exception);
        paged.IsFailure.ShouldBeTrue();
        paged.Value.ShouldBe([42]);
        paged.Messages.ShouldBe(["loaded"]);
        paged.TotalCount.ShouldBe(35L);
        paged.CurrentPage.ShouldBe(2);
        paged.PageSize.ShouldBe(3);
        paged.GetError<ExceptionError>().Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public async Task ValueFactories_SameOperation_ProduceEquivalentOutcomes()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        static string LoadValue() => "loaded";
        static Task<string> LoadAsync(CancellationToken token) => Task.FromCanceled<string>(token);

        var scalar = Result.Bind(LoadValue);
        var retyped = Result<int>.Bind<string>(LoadValue);
        var alias = Result<string>.Try(LoadValue);

        foreach (var result in new[] { scalar, retyped, alias })
        {
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe("loaded");
        }

        var cancellationResults = new[]
        {
            await Result.BindAsync(LoadAsync, cancellation.Token),
            await Result<int>.BindAsync<string>(LoadAsync, cancellation.Token),
            await Result<string>.TryAsync(LoadAsync, cancellation.Token)
        };

        foreach (var result in cancellationResults)
        {
            result.IsFailure.ShouldBeTrue();
            result.HasError<OperationCancelledError>().ShouldBeTrue();
            result.Messages.ShouldBe(["Operation was cancelled"]);
        }
    }

    [Fact]
    public async Task BindAsync_SingleUsePage_ReturnsSameMetadataAsBind()
    {
        var sync = ResultPaged<int>.Bind(() => (Once<int>([1, 2]), 101L));
        var sut = await ResultPaged<int>.BindAsync(_ => Task.FromResult((Once<int>([1, 2]), 101L)));

        foreach (var result in new[] { sync, sut })
        {
            result.IsSuccess.ShouldBeTrue();
            result.TotalCount.ShouldBe(101L);
            result.CurrentPage.ShouldBe(1);
            result.PageSize.ShouldBe(2);
            result.TotalPages.ShouldBe(51);
            result.Value.ShouldBe([1, 2]);
            result.Value.ShouldBe([1, 2]);
        }
    }

    [Fact]
    public void TotalPages_DefaultResultAndLargeCount_CalculatesWithoutRounding()
    {
        var empty = default(ResultPaged<int>);
        var sut = ResultPaged<int>.Success([], (1_000_000_000L * int.MaxValue) + 1, pageSize: int.MaxValue);

        empty.IsFailure.ShouldBeTrue();
        empty.CurrentPage.ShouldBe(1);
        empty.PageSize.ShouldBe(10);
        empty.TotalPages.ShouldBe(0);
        empty.HasNextPage.ShouldBeFalse();
        empty.HasPreviousPage.ShouldBeFalse();
        sut.TotalPages.ShouldBe(1_000_000_001);
        ResultPaged<int>.Success([], long.MaxValue, pageSize: 1).TotalPages.ShouldBe(int.MaxValue);
    }

    [Fact]
    public void For_FailedPage_PreservesMetadataAndDiagnostics()
    {
        var error = new Error("processing failed");
        var sut = ResultPaged<int>.Success([7], "loaded", count: 37, page: 3, pageSize: 4).WithError(error);

        var result = sut.For<string>(["mapped"]);

        result.IsFailure.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
        result.TotalCount.ShouldBe(37L);
        result.CurrentPage.ShouldBe(3);
        result.PageSize.ShouldBe(4);
        result.Messages.ShouldBe(["loaded"]);
        result.Errors.ShouldHaveSingleItem().ShouldBeSameAs(error);
    }

    [Fact]
    public void Merge_EmptyPageWithStatusOnlyResults_PreservesAuthoritativeMetadata()
    {
        var page = ResultPaged<int>.Success([], "no rows", count: 37, page: 3, pageSize: 4);
        var before = Result.Success("before").ToResultPaged<int>();
        var after = Result.Failure("after").ToResultPaged<int>();

        var sut = ResultPaged<int>.Merge(before, page, after);

        sut.IsFailure.ShouldBeTrue();
        sut.Value.ShouldBeEmpty();
        sut.TotalCount.ShouldBe(37L);
        sut.CurrentPage.ShouldBe(3);
        sut.PageSize.ShouldBe(4);
        sut.Messages.ShouldBe(["before", "no rows", "after"]);
    }

    [Fact]
    public void Merge_MultiplePages_SnapshotsRowsOnceAndKeepsAggregateMetadata()
    {
        var first = ResultPaged<int>.Success(Once<int>([1, 2]), count: 37, page: 1, pageSize: 2);
        var second = ResultPaged<int>.Success(Once<int>([3]), count: 37, page: 2, pageSize: 3);

        var sut = ResultPaged<int>.Merge(first, second);

        sut.IsSuccess.ShouldBeTrue();
        sut.Value.ShouldBe([1, 2, 3]);
        sut.TotalCount.ShouldBe(3L);
        sut.CurrentPage.ShouldBe(2);
        sut.PageSize.ShouldBe(3);
    }

    [Fact]
    public void WithOutcome_MessageOnlyFailure_PreservesPayloadAndFailState()
    {
        var sut = ResultPaged<int>.Success([7], "loaded", count: 37, page: 3, pageSize: 4);
        var outcome = Result.Failure("post-processing failed");

        var result = sut.WithOutcome(outcome);

        result.IsFailure.ShouldBeTrue();
        result.Value.ShouldBe([7]);
        result.TotalCount.ShouldBe(37L);
        result.CurrentPage.ShouldBe(3);
        result.PageSize.ShouldBe(4);
        result.Messages.ShouldBe(["loaded", "post-processing failed"]);
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Serialize_ThrownException_WritesVariantEnvelopeAndSafeError()
    {
        var error = new ExternalLookupError(Should.Throw<InvalidOperationException>(() =>
            throw new InvalidOperationException("mapping failed")));
        var serializer = new SystemTextJsonSerializer();

        using var plain = JsonDocument.Parse(serializer.SerializeToString(Result.Failure(error)));
        using var scalar = JsonDocument.Parse(serializer.SerializeToString(Result<int>.Failure(7).WithError(error)));
        using var paged = JsonDocument.Parse(serializer.SerializeToString(ResultPaged<int>.Success([7], count: 37, page: 3, pageSize: 4).WithError(error)));

        AssertExceptionError(plain.RootElement, "type");
        plain.RootElement.TryGetProperty("value", out _).ShouldBeFalse();
        AssertExceptionError(scalar.RootElement, "exceptionType");
        scalar.RootElement.TryGetProperty("value", out _).ShouldBeFalse();
        scalar.RootElement.GetProperty("errors")[0].GetProperty("service").GetString().ShouldBe("inventory");
        AssertExceptionError(paged.RootElement, "exceptionType");
        paged.RootElement.GetProperty("errors")[0].GetProperty("service").GetString().ShouldBe("inventory");
        paged.RootElement.GetProperty("value")[0].GetInt32().ShouldBe(7);
        paged.RootElement.GetProperty("totalCount").GetInt64().ShouldBe(37L);
    }

    private static void AssertExceptionError(JsonElement result, string typeProperty)
    {
        result.GetProperty("isSuccess").GetBoolean().ShouldBeFalse();
        var error = result.GetProperty("errors")[0];
        error.GetProperty("message").GetString().ShouldContain("mapping failed");
        error.GetProperty(typeProperty).GetString().ShouldBe(typeof(InvalidOperationException).FullName);
        error.GetProperty("stackTrace").GetString().ShouldNotBeNullOrWhiteSpace();
        error.TryGetProperty("exception", out _).ShouldBeFalse();
    }

    private sealed class ExternalLookupError(Exception exception) : ExceptionError(exception)
    {
        public string Service => "inventory";
    }

    private static IEnumerable<T> Once<T>(T[] items)
    {
        var started = false;
        return Enumerate();

        IEnumerable<T> Enumerate()
        {
            if (started)
            {
                throw new InvalidOperationException("Sequence can only be enumerated once.");
            }

            started = true;
            foreach (var item in items)
            {
                yield return item;
            }
        }
    }
}
