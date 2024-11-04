// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests;

using BridgingIT.DevKit.Domain.Repositories;

public class RepositoryResultFilterExtensionsTests
{
    private readonly IGenericReadOnlyRepository<PersonStub> repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();

    [Fact]
    public async Task FindAllPagedResultAsync_WithBasicFilters_ReturnsCorrectResult()
    {
        // Arrange
        var filter = new FilterModel
        {
            Page = 1,
            PageSize = 10,
            Filters = [new FilterCriteria { Field = "Age", Operator = FilterOperator.Equal, Value = 18 }],
            Orderings = [new FilterOrderCriteria { Field = "LastName", Direction = OrderDirection.Ascending }],
            Includes = ["Orders"]
        };

        var expectedPersons = new List<PersonStub>
        {
            new() { FirstName = "John", LastName = "Doe", Age = 30 },
            new() { FirstName = "Jane", LastName = "Smith", Age = 25 }
        };

        this.SetupRepository(expectedPersons, 2);

        // Act
        var result = await this.repository.FindAllPagedResultAsync(filter);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expectedPersons);
        result.TotalCount.ShouldBe(2);
        result.CurrentPage.ShouldBe(1);
        result.PageSize.ShouldBe(10);

        // Assert FindOptions
        await this.repository.Received(1).FindAllAsync(
            Arg.Any<IEnumerable<ISpecification<PersonStub>>>(),
            Arg.Any<IFindOptions<PersonStub>>(),
            // Arg.Is<FindOptions<PersonStub>>(options =>
            //     options.Skip == 0 &&
            //     options.Take == 10 &&
            //     options.Orders.Count() == 1 &&
            //     options.Orders.First().Ordering == "LastName ascending" &&
            //     options.Includes.Count() == 1 &&
            //     ((MemberExpression)options.Includes.First().Expression.Body).Member.Name == "Orders"
            // ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task FindAllPagedResultAsync_WithCustomSpecificationFilter_ReturnsCorrectResult()
    {
        // Arrange
        var filter = new FilterModel
        {
            Page = 1,
            PageSize = 10,
            Filters =
            [
                new FilterCriteria
                {
                    CustomType = FilterCustomType.NamedSpecification,
                    SpecificationName = "IsAdult",
                    SpecificationArguments = [21]
                }
            ]
        };

        var expectedPersons = new List<PersonStub>
        {
            new() { FirstName = "John", LastName = "Doe", Age = 30 },
            new() { FirstName = "Jane", LastName = "Smith", Age = 25 }
        };

        this.SetupRepository(expectedPersons, 2);

        // Register the specification
        SpecificationResolver.Clear();
        SpecificationResolver.Register<PersonStub, AdultSpecification>("IsAdult");

        // Act
        var result = await this.repository.FindAllPagedResultAsync(filter);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expectedPersons);
        result.TotalCount.ShouldBe(2);

        // Assert FindOptions
        await this.repository.Received(1).FindAllAsync(
            Arg.Any<IEnumerable<ISpecification<PersonStub>>>(),
            Arg.Any<IFindOptions<PersonStub>>(),
            // Arg.Is<FindOptions<PersonStub>>(options =>
            //     options.Skip == 0 &&
            //     options.Take == 10 &&
            //     !options.Orders.Any() &&
            //     !options.Includes.Any()
            // ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task FindAllPagedResultAsync_WithAnyCustomFilter_ReturnsCorrectResult()
    {
        // Arrange
        var filter = new FilterModel
        {
            Page = 1,
            PageSize = 10,
            Filters =
            [
                new FilterCriteria
                {
                    Field = "Orders",
                    Operator = FilterOperator.Any,
                    Value = new FilterCriteria
                    {
                        Field = "TotalAmount",
                        Operator = FilterOperator.GreaterThan,
                        Value = 100.0m
                    }
                }
            ]
        };

        var expectedPersons = new List<PersonStub>
        {
            new()
            {
                FirstName = "John",
                LastName = "Doe",
                Orders = [new OrderStub { TotalAmount = 150.0m }]
            },
            new()
            {
                FirstName = "Jane",
                LastName = "Smith",
                Orders = [new OrderStub { TotalAmount = 200.0m }]
            }
        };

        this.SetupRepository(expectedPersons, 2);

        // Act
        var result = await this.repository.FindAllPagedResultAsync(filter);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expectedPersons);
        result.TotalCount.ShouldBe(2);

        // Assert FindOptions
        await this.repository.Received(1).FindAllAsync(
            Arg.Any<IEnumerable<ISpecification<PersonStub>>>(),
            Arg.Any<IFindOptions<PersonStub>>(),
            // Arg.Is<FindOptions<PersonStub>>(options =>
            //     options.Skip == 0 &&
            //     options.Take == 10 &&
            //     !options.Orders.Any() &&
            //     !options.Includes.Any()
            // ),
            Arg.Any<CancellationToken>()
        );
    }

    private void SetupRepository(IEnumerable<PersonStub> persons, int totalCount)
    {
        this.repository.CountAsync(Arg.Any<IEnumerable<ISpecification<PersonStub>>>(), Arg.Any<CancellationToken>())
            .Returns(totalCount);

        this.repository.FindAllAsync(
                Arg.Any<IEnumerable<ISpecification<PersonStub>>>(),
                Arg.Any<FindOptions<PersonStub>>(),
                Arg.Any<CancellationToken>())
            .Returns(persons);
    }
}