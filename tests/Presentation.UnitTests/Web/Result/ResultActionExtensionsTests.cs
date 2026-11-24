// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using Microsoft.AspNetCore.Mvc;
using Presentation.Web;

[UnitTest("Presentation")]
public class ResultActionExtensionsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MapPaged_Success_PreservesPagination(bool useOk)
    {
        var sut = ResultPaged<int>.Success([42], count: 95, page: 3, pageSize: 20)
            .WithMessage("Page loaded");
        var mapper = Substitute.For<IMapper>();
        var person = PersonStub.Create(42);
        mapper.Map<int, PersonStub>(42).Returns(person);

        var response = useOk
            ? sut.ToOkActionResult<int, PersonStub>(mapper)
            : sut.ToObjectActionResult<PersonStub, int>(mapper, 207);

        var body = response.Result.ShouldBeAssignableTo<ObjectResult>();
        body.StatusCode.ShouldBe(useOk ? 200 : 207);
        var page = body.Value.ShouldBeOfType<ResultPaged<PersonStub>>();
        page.TotalCount.ShouldBe(95);
        page.CurrentPage.ShouldBe(3);
        page.PageSize.ShouldBe(20);
        page.Value.ShouldBe([person]);
        page.Messages.ShouldBe(["Page loaded"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MapPaged_Failure_MapsTypedErrorWithoutMappingValues(bool useOk)
    {
        var sut = ResultPaged<int>.Success([42], count: 95, page: 3, pageSize: 20)
            .WithError(new ValidationError("Invalid page"));
        var mapper = Substitute.For<IMapper>();

        var response = useOk
            ? sut.ToOkActionResult<int, PersonStub>(mapper)
            : sut.ToObjectActionResult<PersonStub, int>(mapper, 207);

        var body = await ResultActionResultExecution.ExecuteAsync(response.Result, 400);
        body.GetProperty("data").GetProperty("errors").GetProperty("validation")[0].GetString().ShouldBe("Invalid page");
        mapper.DidNotReceiveWithAnyArgs().Map<int, PersonStub>(0);
    }

    [Fact]
    public async Task ToOkActionResult_FailureWithValue_SkipsMapping()
    {
        var sut = Result<int>.Failure(42).WithError(new ValidationError("Invalid item"));
        var mapper = Substitute.For<IMapper>();

        var response = sut.ToOkActionResult<int, PersonStub>(mapper);

        _ = await ResultActionResultExecution.ExecuteAsync(response.Result, 400);
        mapper.DidNotReceiveWithAnyArgs().Map<int, PersonStub>(0);
    }
}
