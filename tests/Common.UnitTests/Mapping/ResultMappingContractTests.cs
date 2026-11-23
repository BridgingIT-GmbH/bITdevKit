// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Mapping;

[UnitTest("Common")]
public class ResultMappingContractTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MapResult_FailedSourceWithMissingMapper_PreservesFailure(bool useAsyncSuffix)
    {
        var error = new NotFoundError("Missing item");
        var sut = Task.FromResult(Result<int>.Failure(42, "From repository", error));

        var mapped = useAsyncSuffix
            ? await sut.MapResultAsync<int, string>(null)
            : await sut.MapResult<int, string>(null);

        mapped.IsFailure.ShouldBeTrue();
        mapped.Messages.ShouldBe(["From repository"]);
        mapped.Errors.ShouldHaveSingleItem().ShouldBeSameAs(error);
    }

    [Fact]
    public void MapResult_SuccessfulSource_PreservesMessagesFromBothSteps()
    {
        var mapper = Substitute.For<IMapper>();
        mapper.MapResult<int, string>(42).Returns(Result<string>.Success("mapped", "From mapper"));
        var sut = Result<int>.Success(42, "From source");

        var mapped = sut.MapResult<int, string>(mapper);

        mapped.IsSuccess.ShouldBeTrue();
        mapped.Messages.ShouldBe(["From source", "From mapper"]);
        mapped.Value.ShouldBe("mapped");
    }

    [Fact]
    public void MapResult_SuccessfulSourceWithMissingMapper_ReportsMissingMapper()
    {
        var sut = Result<int>.Success(42, "From source");

        var mapped = sut.MapResult<int, string>(null);

        mapped.IsFailure.ShouldBeTrue();
        mapped.Messages.ShouldBe(["From source"]);
        mapped.Errors.ShouldHaveSingleItem().Message.ShouldBe("Mapper cannot be null");
    }

    [Fact]
    public async Task MapResult_WhenMapperCancels_PreservesCancellationAndSourceMessages()
    {
        var mapper = Substitute.For<IMapper>();
        mapper.MapResult<int, string>(42).Returns(_ => throw new OperationCanceledException());
        var sut = Result<int>.Success(42, "From source");

        var sync = sut.MapResult<int, string>(mapper);
        var task = await Task.FromResult(sut).MapResult<int, string>(mapper);
        var async = await Task.FromResult(sut).MapResultAsync<int, string>(mapper);

        foreach (var result in new[] { sync, task, async })
        {
            result.IsFailure.ShouldBeTrue();
            result.Errors.ShouldHaveSingleItem().ShouldBeOfType<OperationCancelledError>();
            result.Messages.ShouldBe(["From source"]);
        }
    }
}
