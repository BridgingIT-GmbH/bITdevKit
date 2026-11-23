// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.ActiveEntity;

using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Repositories;

public class ActiveEntityPagedResultTests
{
    [Theory]
    [InlineData(int.MaxValue, 10)]
    [InlineData(int.MaxValue, 0)]
    public async Task FilterPagedQueries_WithAnOverflowingOffset_ReturnFailureBeforeCallingTheProvider(int page, int pageSize)
    {
        var provider = Substitute.For<IActiveEntityEntityProvider<PagedEntity, Guid>>();
        var behavior = Substitute.For<IActiveEntityBehavior<PagedEntity>>();
        var sut = new ActiveEntityContext<PagedEntity, Guid>(provider, [behavior]);
        var filter = new FilterModel { Page = page, PageSize = pageSize };

        var entities = await PagedEntity.FindAllPagedAsync(sut, filter);
        var ids = await PagedEntity.FindAllIdsPagedAsync(sut, filter);
        var projection = await PagedEntity.ProjectAllPagedAsync(sut, filter, entity => entity.Id);

        IResult[] results = [entities, ids, projection];
        foreach (var result in results)
        {
            result.IsFailure.ShouldBeTrue();
            result.Errors.OfType<ExceptionError>().Single().Exception.ShouldBeOfType<OverflowException>();
        }

        provider.ReceivedCalls().ShouldBeEmpty();
        behavior.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task FilterPagedQueries_WithAnOmittedPageSize_UseTheDefaultSizeAndRequestedPage()
    {
        var provider = Substitute.For<IActiveEntityEntityProvider<PagedEntity, Guid>>();
        var sut = new ActiveEntityContext<PagedEntity, Guid>(provider, []);
        var filter = new FilterModel { Page = 2, PageSize = 0 };

        await PagedEntity.FindAllPagedAsync(sut, filter);
        await PagedEntity.FindAllIdsPagedAsync(sut, filter);
        await PagedEntity.ProjectAllPagedAsync(sut, filter, entity => entity.Id);

        await provider.Received(1).FindAllPagedAsync(
            Arg.Any<IEnumerable<ISpecification<PagedEntity>>>(),
            Arg.Is<IFindOptions<PagedEntity>>(option => option.Skip == 10 && option.Take == 10));
        await provider.Received(1).FindAllIdsPagedAsync(
            Arg.Any<IEnumerable<ISpecification<PagedEntity>>>(),
            Arg.Is<IFindOptions<PagedEntity>>(option => option.Skip == 10 && option.Take == 10));
        await provider.Received(1).ProjectAllPagedAsync<Guid>(
            Arg.Any<IEnumerable<ISpecification<PagedEntity>>>(),
            Arg.Any<Expression<Func<PagedEntity, Guid>>>(),
            Arg.Is<IFindOptions<PagedEntity>>(option => option.Skip == 10 && option.Take == 10));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FindAllPagedAsync_WithBehaviorHooks_PreservesProviderPage(bool afterSucceeds)
    {
        var options = new FindOptions<PagedEntity> { Skip = 40, Take = 2 };
        var provider = Substitute.For<IActiveEntityEntityProvider<PagedEntity, Guid>>();
        provider.FindAllPagedAsync(Arg.Any<ISpecification<PagedEntity>>(), options)
            .Returns(ResultPaged<PagedEntity>.Success([], count: 39, page: 21, pageSize: 2).WithMessage("provider"));
        var behavior = Substitute.For<IActiveEntityBehavior<PagedEntity>>();
        behavior.BeforeFindAllPagedAsync(options).Returns(Result.Success("before"));
        behavior.AfterFindAllPagedAsync(options, Arg.Any<ResultPaged<PagedEntity>>(), true)
            .Returns(afterSucceeds ? Result.Success("after") : Result.Failure("after"));
        var context = new ActiveEntityContext<PagedEntity, Guid>(provider, [behavior]);

        var result = await PagedEntity.FindAllPagedAsync(context, entity => true, options);

        result.IsSuccess.ShouldBe(afterSucceeds);
        result.Value.ShouldBeEmpty();
        result.TotalCount.ShouldBe(39);
        result.CurrentPage.ShouldBe(21);
        result.PageSize.ShouldBe(2);
        result.Messages.ShouldBe(["before", "provider", "after"]);
        result.Errors.ShouldBeEmpty();
    }

    public sealed class PagedEntity : ActiveEntity<PagedEntity, Guid>;
}
