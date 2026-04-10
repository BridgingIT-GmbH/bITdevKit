// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests;

using BridgingIT.DevKit.Domain.Repositories;
using NSubstitute;

public class RepositoryExtensionsTests
{
    private readonly IGenericRepository<PersonStub> repository = Substitute.For<IGenericRepository<PersonStub>>();

    [Fact]
    public async Task UpdateSetAsync_WithExpression_ForwardsToRepositorySpecificationOverload()
    {
        // Arrange
        this.repository.UpdateSetAsync(
                Arg.Any<ISpecification<PersonStub>>(),
                Arg.Any<Action<IEntityUpdateSet<PersonStub>>>(),
                Arg.Any<IFindOptions<PersonStub>>(),
                Arg.Any<CancellationToken>())
            .Returns(2L);

        Action<IEntityUpdateSet<PersonStub>> set = _ => { };

        // Act
        var result = await this.repository.UpdateSetAsync(
            p => p.Age >= 18,
            set,
            cancellationToken: CancellationToken.None);

        // Assert
        result.ShouldBe(2L);
        await this.repository.Received(1).UpdateSetAsync(
            Arg.Is<ISpecification<PersonStub>>(s =>
                s.IsSatisfiedBy(new PersonStub { Age = 18 }) &&
                !s.IsSatisfiedBy(new PersonStub { Age = 17 })),
            Arg.Any<Action<IEntityUpdateSet<PersonStub>>>(),
            Arg.Any<IFindOptions<PersonStub>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateSetAsync_WithFilterModel_ForwardsBuiltSpecificationsAndFindOptions()
    {
        // Arrange
        var filter = new FilterModel
        {
            Page = 2,
            PageSize = 10,
            Filters = [new FilterCriteria { Field = nameof(PersonStub.Age), Operator = FilterOperator.GreaterThanOrEqual, Value = 18 }],
            Orderings = [new FilterOrderCriteria { Field = nameof(PersonStub.LastName), Direction = OrderDirection.Ascending }]
        };
        var extraSpecification = new Specification<PersonStub>(p => p.FirstName == "John");

        this.repository.UpdateSetAsync(
                Arg.Any<IEnumerable<ISpecification<PersonStub>>>(),
                Arg.Any<Action<IEntityUpdateSet<PersonStub>>>(),
                Arg.Any<IFindOptions<PersonStub>>(),
                Arg.Any<CancellationToken>())
            .Returns(3L);

        Action<IEntityUpdateSet<PersonStub>> set = _ => { };

        // Act
        var result = await this.repository.UpdateSetAsync(filter, set, [extraSpecification], CancellationToken.None);

        // Assert
        result.ShouldBe(3L);
        await this.repository.Received(1).UpdateSetAsync(
            Arg.Is<IEnumerable<ISpecification<PersonStub>>>(s => s.Count() == 2),
            Arg.Any<Action<IEntityUpdateSet<PersonStub>>>(),
            Arg.Is<IFindOptions<PersonStub>>(o =>
                o.Skip == 10 &&
                o.Take == 10 &&
                o.Orders.Count() == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSetAsync_WithExpression_ForwardsToRepositorySpecificationOverload()
    {
        // Arrange
        this.repository.DeleteSetAsync(
                Arg.Any<ISpecification<PersonStub>>(),
                Arg.Any<IFindOptions<PersonStub>>(),
                Arg.Any<CancellationToken>())
            .Returns(4L);

        // Act
        var result = await this.repository.DeleteSetAsync(
            p => p.Age >= 18,
            cancellationToken: CancellationToken.None);

        // Assert
        result.ShouldBe(4L);
        await this.repository.Received(1).DeleteSetAsync(
            Arg.Is<ISpecification<PersonStub>>(s =>
                s.IsSatisfiedBy(new PersonStub { Age = 21 }) &&
                !s.IsSatisfiedBy(new PersonStub { Age = 17 })),
            Arg.Any<IFindOptions<PersonStub>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSetAsync_WithFilterModel_ForwardsBuiltSpecificationsAndFindOptions()
    {
        // Arrange
        var filter = new FilterModel
        {
            Page = 3,
            PageSize = 5,
            Filters = [new FilterCriteria { Field = nameof(PersonStub.Age), Operator = FilterOperator.LessThan, Value = 30 }],
            Orderings = [new FilterOrderCriteria { Field = nameof(PersonStub.FirstName), Direction = OrderDirection.Descending }]
        };
        var extraSpecification = new Specification<PersonStub>(p => p.LastName == "Doe");

        this.repository.DeleteSetAsync(
                Arg.Any<IEnumerable<ISpecification<PersonStub>>>(),
                Arg.Any<IFindOptions<PersonStub>>(),
                Arg.Any<CancellationToken>())
            .Returns(5L);

        // Act
        var result = await this.repository.DeleteSetAsync(filter, [extraSpecification], CancellationToken.None);

        // Assert
        result.ShouldBe(5L);
        await this.repository.Received(1).DeleteSetAsync(
            Arg.Is<IEnumerable<ISpecification<PersonStub>>>(s => s.Count() == 2),
            Arg.Is<IFindOptions<PersonStub>>(o =>
                o.Skip == 10 &&
                o.Take == 5 &&
                o.Orders.Count() == 1),
            Arg.Any<CancellationToken>());
    }
}
