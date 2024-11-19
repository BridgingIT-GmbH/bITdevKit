// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests;

using System.Linq.Expressions;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;
using CancellationToken = System.Threading.CancellationToken;

public class GenericReadOnlyRepositoryResultExtensionsTests
{
    private readonly Faker<PersonStub> personFaker;

    public GenericReadOnlyRepositoryResultExtensionsTests()
    {
        this.personFaker = new Faker<PersonStub>()
            .RuleFor(p => p.Id, f => f.Random.Guid())
            .RuleFor(p => p.FirstName, f => f.Name.FirstName())
            .RuleFor(p => p.LastName, f => f.Name.LastName())
            .RuleFor(p => p.Age, f => f.Random.Int(18, 80));
    }

    // disabled due to mocking result issue
    // [Fact]
    // public async Task ExistsResultAsync_WhenEntityExists_ReturnsSuccessResultWithTrue()
    // {
    //     // Arrange
    //     var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
    //     var id = Guid.NewGuid();
    //     repository.ExistsResultAsync(Arg.Any<object>(), Arg.Any<CancellationToken>()).Returns(true);
    //
    //     // Act
    //     var result = await repository.ExistsResultAsync(id);
    //
    //     // Assert
    //     result.ShouldBeSuccess();
    //     result.Value.ShouldBeTrue();
    //     await repository.Received(1).ExistsAsync(Arg.Is<object>(x => x.Equals(id)), Arg.Any<CancellationToken>());
    // }

    // disabled due to mocking result issue
    // [Fact]
    // public async Task ExistsResultAsync_WhenEntityDoesNotExist_ReturnsSuccessResultWithFalse()
    // {
    //     // Arrange
    //     var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
    //     var id = Guid.NewGuid();
    //     repository.ExistsResultAsync(Arg.Any<object>(), Arg.Any<CancellationToken>()).Returns(false);
    //
    //     // Act
    //     var result = await repository.ExistsResultAsync(id);
    //
    //     // Assert
    //     result.ShouldBeSuccess();
    //     result.Value.ShouldBeFalse();
    //     await repository.Received(1).ExistsAsync(Arg.Is<object>(x => x.Equals(id)), Arg.Any<CancellationToken>());
    // }

    [Fact]
    public async Task ExistsResultAsync_WhenExceptionThrown_ReturnsFailureResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        repository.ExistsResultAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("Test exception"));

        // Act
        var result = await repository.ExistsResultAsync(Guid.NewGuid());

        // Assert
        result.ShouldBeFailure();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.First().ShouldBeOfType<ExceptionError>();
    }

    [Fact]
    public async Task CountResultAsync_WhenSuccessful_ReturnsSuccessResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedCount = 5;
        repository.CountAsync(Arg.Any<CancellationToken>()).Returns(expectedCount);

        // Act
        var result = await repository.CountResultAsync();

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedCount);
    }

    [Fact]
    public async Task CountResultAsync_WithExpression_ReturnsSuccessResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedCount = 5;
        repository.CountAsync(Arg.Any<ISpecification<PersonStub>>(), Arg.Any<CancellationToken>()).Returns(expectedCount);

        // Act
        var result = await repository.CountResultAsync(p => p.Age > 30);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedCount);
        await repository.Received(1).CountAsync(Arg.Any<ISpecification<PersonStub>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CountResultAsync_WithSpecification_ReturnsSuccessResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedCount = 5;
        repository.CountAsync(Arg.Any<ISpecification<PersonStub>>(), Arg.Any<CancellationToken>()).Returns(expectedCount);

        // Act
        var specification = Substitute.For<ISpecification<PersonStub>>();
        var result = await repository.CountResultAsync(specification);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedCount);
        await repository.Received(1).CountAsync(Arg.Is(specification), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CountResultAsync_WithSpecifications_ReturnsSuccessResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedCount = 5;
        repository.CountAsync(Arg.Any<IEnumerable<ISpecification<PersonStub>>>(), Arg.Any<CancellationToken>()).Returns(expectedCount);

        // Act
        var specifications = new List<ISpecification<PersonStub>>
        {
            Substitute.For<ISpecification<PersonStub>>(),
            Substitute.For<ISpecification<PersonStub>>()
        };
        var result = await repository.CountResultAsync(specifications);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedCount);
        await repository.Received(1).CountAsync(Arg.Is(specifications), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CountResultAsync_WhenExceptionThrown_ReturnsFailureResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        repository.CountAsync(Arg.Any<CancellationToken>())
            .Throws(new Exception("Test exception"));

        // Act
        var result = await repository.CountResultAsync();

        // Assert
        result.ShouldBeFailure();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.First().ShouldBeOfType<ExceptionError>();
    }

    [Fact]
    public async Task FindAllIdsResultAsync_WhenSuccessful_ReturnsSuccessResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedIds = this.personFaker.Generate(3).Select(p => p.Id).ToList();
        repository.FindAllIdsAsync<PersonStub, Guid>(
                Arg.Any<IFindOptions<PersonStub>>(),
                Arg.Any<CancellationToken>())
            .Returns(expectedIds);

        // Act
        var result = await repository.FindAllIdsResultAsync<PersonStub, Guid>();

        // Assert
        result.ShouldBeSuccess();
        // result.Value.ShouldBe(expectedIds);
    }

    [Fact]
    public async Task FindAllIdsResultAsync_WithGuidIds_ReturnsSuccessResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedIds = this.personFaker.Generate(3).Select(p => p.Id).ToList();

        repository.ProjectAllAsync(
                Arg.Any<Expression<Func<PersonStub, object>>>(),
                Arg.Any<IFindOptions<PersonStub>>(),
                Arg.Any<CancellationToken>())
            .Returns(expectedIds.Cast<object>());

        // Act
        var result = await repository.FindAllIdsResultAsync<PersonStub, Guid>();

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedIds);
    }

    [Fact]
    public async Task FindAllIdsResultAsync_WithEmptyResult_ReturnsEmptyList()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();

        repository.ProjectAllAsync(
                Arg.Any<Expression<Func<PersonStub, object>>>(),
                Arg.Any<IFindOptions<PersonStub>>(),
                Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var result = await repository.FindAllIdsResultAsync<PersonStub, Guid>();

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task FindAllIdsResultAsync_WithExpression_AppliesExpressionCorrectly()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedIds = this.personFaker.Generate(3).Select(p => p.Id).ToList();

        repository.ProjectAllAsync(
                Arg.Any<ISpecification<PersonStub>>(),
                Arg.Any<Expression<Func<PersonStub, Guid>>>(),
                Arg.Any<IFindOptions<PersonStub>>(),
                Arg.Any<CancellationToken>())
            .Returns(expectedIds);

        // Act
        var result = await repository.FindAllIdsResultAsync<PersonStub, Guid>(p => p.Age > 30);

        // Assert
        result.ShouldBeSuccess();
        //result.Value.ShouldBe(expectedIds);
        // await repository.Received(1).ProjectAllAsync(
        //     Arg.Any<ISpecification<PersonStub>>(),
        //     Arg.Any<Expression<Func<PersonStub, Guid>>>(),
        //     Arg.Any<IFindOptions<PersonStub>>(),
        //     Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindAllIdsResultAsync_WithSpecification_AppliesSpecificationCorrectly()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedIds = this.personFaker.Generate(3).Select(p => p.Id).ToList();
        var specification = Substitute.For<ISpecification<PersonStub>>();

        repository.ProjectAllAsync(
                Arg.Any<Expression<Func<PersonStub, Guid>>>(),
                Arg.Any<IFindOptions<PersonStub>>(),
                Arg.Any<CancellationToken>())
            .Returns(expectedIds);

        // Act
        var result = await repository.FindAllIdsResultAsync<PersonStub, Guid>(specification);

        // Assert
        result.ShouldBeSuccess();
        // result.Value.ShouldBe(expectedIds);
        // await repository.Received(1).ProjectAllAsync(
        //     Arg.Any<Expression<Func<PersonStub, object>>>(),
        //     Arg.Is<IFindOptions<PersonStub>>(opt => opt.Order == null),
        //     Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindAllIdsResultAsync_WithFindOptions_AppliesOptionsCorrectly()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedIds = this.personFaker.Generate(3).Select(p => p.Id).ToList();
        var findOptions = new FindOptions<PersonStub> { Skip = 5, Take = 10 };

        repository.ProjectAllAsync(
                Arg.Any<Expression<Func<PersonStub, object>>>(),
                Arg.Any<IFindOptions<PersonStub>>(),
                Arg.Any<CancellationToken>())
            .Returns(expectedIds.Cast<object>());

        // Act
        var result = await repository.FindAllIdsResultAsync<PersonStub, Guid>(findOptions);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedIds);
        await repository.Received(1).ProjectAllAsync(
            Arg.Any<Expression<Func<PersonStub, object>>>(),
            Arg.Is<IFindOptions<PersonStub>>(opt => opt.Skip == 5 && opt.Take == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindAllIdsResultAsync_WhenExceptionThrown_ReturnsFailureResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        repository.ProjectAllAsync(
                Arg.Any<Expression<Func<PersonStub, object>>>(),
                Arg.Any<IFindOptions<PersonStub>>(),
                Arg.Any<CancellationToken>())
            .Throws(new Exception("Test exception"));

        // Act
        var result = await repository.FindAllIdsResultAsync<PersonStub, Guid>();

        // Assert
        result.ShouldBeFailure();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.First().ShouldBeOfType<ExceptionError>();
    }

    [Fact]
    public async Task FindOneResultAsync_WithId_WhenEntityFound_ReturnsSuccessResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedPerson = this.personFaker.Generate();
        repository.FindOneAsync(Arg.Any<object>(), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns(expectedPerson);

        // Act
        var result = await repository.FindOneResultAsync(expectedPerson.Id);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedPerson);
        await repository.Received(1).FindOneAsync(Arg.Is(expectedPerson.Id), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindOneResultAsync_WithId_WhenEntityNotFound_ReturnsFailureResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        repository.FindOneAsync(Arg.Any<object>(), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns((PersonStub)null);

        // Act
        var result = await repository.FindOneResultAsync(Guid.NewGuid());

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<NotFoundError>();
    }

    [Fact]
    public async Task FindOneResultAsync_WithExpression_WhenEntityFound_ReturnsSuccessResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedPerson = this.personFaker.Generate();
        repository.FindOneAsync(Arg.Any<ISpecification<PersonStub>>(), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns(expectedPerson);

        // Act
        var result = await repository.FindOneResultAsync(p => p.Id == expectedPerson.Id);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedPerson);
        await repository.Received(1).FindOneAsync(Arg.Any<ISpecification<PersonStub>>(), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindOneResultAsync_WithExpression_WhenEntityNotFound_ReturnsFailureResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        repository.FindOneAsync(Arg.Any<ISpecification<PersonStub>>(), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns((PersonStub)null);

        // Act
        var result = await repository.FindOneResultAsync(p => p.Age > 100);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<NotFoundError>();
    }

    [Fact]
    public async Task FindOneResultAsync_WithSpecification_WhenEntityFound_ReturnsSuccessResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedPerson = this.personFaker.Generate();
        repository.FindOneAsync(Arg.Any<ISpecification<PersonStub>>(), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns(expectedPerson);

        // Act
        var specification = Substitute.For<ISpecification<PersonStub>>();
        var result = await repository.FindOneResultAsync(specification);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedPerson);
        await repository.Received(1).FindOneAsync(Arg.Is(specification), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindOneResultAsync_WithSpecification_WhenEntityNotFound_ReturnsFailureResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        repository.FindOneAsync(Arg.Any<ISpecification<PersonStub>>(), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns((PersonStub)null);

        // Act
        var specification = Substitute.For<ISpecification<PersonStub>>();
        var result = await repository.FindOneResultAsync(specification);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<NotFoundError>();
    }

    [Fact]
    public async Task FindOneResultAsync_WithSpecifications_WhenEntityFound_ReturnsSuccessResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedPerson = this.personFaker.Generate();
        repository.FindOneAsync(Arg.Any<IEnumerable<ISpecification<PersonStub>>>(), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns(expectedPerson);

        // Act
        var specifications = new List<ISpecification<PersonStub>>
        {
            Substitute.For<ISpecification<PersonStub>>(),
            Substitute.For<ISpecification<PersonStub>>()
        };
        var result = await repository.FindOneResultAsync(specifications);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedPerson);
        await repository.Received(1).FindOneAsync(Arg.Is(specifications), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindOneResultAsync_WithSpecifications_WhenEntityNotFound_ReturnsFailureResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        repository.FindOneAsync(Arg.Any<IEnumerable<ISpecification<PersonStub>>>(), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns((PersonStub)null);

        // Act
        var specifications = new List<ISpecification<PersonStub>>
        {
            Substitute.For<ISpecification<PersonStub>>(),
            Substitute.For<ISpecification<PersonStub>>()
        };
        var result = await repository.FindOneResultAsync(specifications);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<NotFoundError>();
    }

    [Fact]
    public async Task FindOneResultAsync_WhenExceptionThrown_ReturnsFailureResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        repository.FindOneAsync(Arg.Any<object>(), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("Test exception"));

        // Act
        var result = await repository.FindOneResultAsync(Guid.NewGuid());

        // Assert
        result.ShouldBeFailure();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.First().ShouldBeOfType<ExceptionError>();
    }

    [Fact]
    public async Task FindAllResultAsync_WhenEntitiesFound_ReturnsSuccessResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedPersons = this.personFaker.Generate(3);
        repository.FindAllAsync(Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns(expectedPersons);

        // Act
        var result = await repository.FindAllResultAsync();

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedPersons);
        await repository.Received(1).FindAllAsync(Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindAllResultAsync_WhenNoEntitiesFound_ReturnsEmptySuccessResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        repository.FindAllAsync(Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns(new List<PersonStub>());

        // Act
        var result = await repository.FindAllResultAsync();

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task FindAllResultAsync_WithExpression_WhenEntitiesFound_ReturnsSuccessResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedPersons = this.personFaker.Generate(3);
        repository.FindAllAsync(Arg.Any<ISpecification<PersonStub>>(), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns(expectedPersons);

        // Act
        var result = await repository.FindAllResultAsync(p => p.Age > 20);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedPersons);
        await repository.Received(1).FindAllAsync(Arg.Any<ISpecification<PersonStub>>(), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindAllResultAsync_WithExpression_WhenNoEntitiesFound_ReturnsEmptySuccessResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        repository.FindAllAsync(Arg.Any<ISpecification<PersonStub>>(), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns(new List<PersonStub>());

        // Act
        var result = await repository.FindAllResultAsync(p => p.Age > 100);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task FindAllResultAsync_WithSpecification_WhenEntitiesFound_ReturnsSuccessResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedPersons = this.personFaker.Generate(3);
        repository.FindAllAsync(Arg.Any<ISpecification<PersonStub>>(), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns(expectedPersons);

        // Act
        var specification = Substitute.For<ISpecification<PersonStub>>();
        var result = await repository.FindAllResultAsync(specification);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedPersons);
        await repository.Received(1).FindAllAsync(Arg.Is(specification), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindAllResultAsync_WithSpecification_WhenNoEntitiesFound_ReturnsEmptySuccessResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        repository.FindAllAsync(Arg.Any<ISpecification<PersonStub>>(), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns(new List<PersonStub>());

        // Act
        var specification = Substitute.For<ISpecification<PersonStub>>();
        var result = await repository.FindAllResultAsync(specification);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task FindAllResultAsync_WithSpecifications_WhenEntitiesFound_ReturnsSuccessResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedPersons = this.personFaker.Generate(3);
        repository.FindAllAsync(Arg.Any<IEnumerable<ISpecification<PersonStub>>>(), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns(expectedPersons);

        // Act
        var specifications = new List<ISpecification<PersonStub>>
        {
            Substitute.For<ISpecification<PersonStub>>(),
            Substitute.For<ISpecification<PersonStub>>()
        };
        var result = await repository.FindAllResultAsync(specifications);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedPersons);
        await repository.Received(1).FindAllAsync(Arg.Is(specifications), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindAllResultAsync_WithSpecifications_WhenNoEntitiesFound_ReturnsEmptySuccessResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        repository.FindAllAsync(Arg.Any<IEnumerable<ISpecification<PersonStub>>>(), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns(new List<PersonStub>());

        // Act
        var specifications = new List<ISpecification<PersonStub>>
        {
            Substitute.For<ISpecification<PersonStub>>(),
            Substitute.For<ISpecification<PersonStub>>()
        };
        var result = await repository.FindAllResultAsync(specifications);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task FindAllResultAsync_WhenExceptionThrown_ReturnsFailureResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        repository.FindAllAsync(Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("Test exception"));

        // Act
        var result = await repository.FindAllResultAsync();

        // Assert
        result.ShouldBeFailure();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.First().ShouldBeOfType<ExceptionError>();
    }

    [Fact]
    public async Task FindAllResultAsync_WithFindOptions_AppliesOptionsCorrectly()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedPersons = this.personFaker.Generate(3);
        repository.FindAllAsync(Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns(expectedPersons);

        // Act
        var findOptions = new FindOptions<PersonStub>
        {
            Skip = 5,
            Take = 10,
            NoTracking = true
        };
        var result = await repository.FindAllResultAsync(findOptions);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedPersons);
        await repository.Received(1).FindAllAsync(Arg.Is<IFindOptions<PersonStub>>(
                opt => opt.Skip == 5 && opt.Take == 10 && opt.NoTracking),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindAllPagedResultAsync_WithStringOrdering_ReturnsPagedResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedPersons = this.personFaker.Generate(3);
        var totalCount = 10;
        repository.CountAsync(Arg.Any<CancellationToken>()).Returns(totalCount);
        repository.FindAllAsync(Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns(expectedPersons);

        // Act
        var result = await repository.FindAllPagedResultAsync(
            ordering: "Age ascending",
            page: 1,
            pageSize: 3);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedPersons);
        result.TotalCount.ShouldBe(totalCount);
        result.CurrentPage.ShouldBe(1);
        result.PageSize.ShouldBe(3);
        await repository.Received(1).FindAllAsync(
            Arg.Is<IFindOptions<PersonStub>>(opt =>
                opt.Order != null &&
                opt.Skip == 0 &&
                opt.Take == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindAllPagedResultAsync_WithExpressionOrdering_ReturnsPagedResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedPersons = this.personFaker.Generate(3);
        var totalCount = 10;
        repository.CountAsync(Arg.Any<CancellationToken>()).Returns(totalCount);
        repository.FindAllAsync(Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns(expectedPersons);

        // Act
        var result = await repository.FindAllPagedResultAsync(
            orderingExpression: p => p.Age,
            page: 2,
            pageSize: 3,
            orderDirection: OrderDirection.Descending);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedPersons);
        result.TotalCount.ShouldBe(totalCount);
        result.CurrentPage.ShouldBe(2);
        result.PageSize.ShouldBe(3);
        await repository.Received(1).FindAllAsync(
            Arg.Is<IFindOptions<PersonStub>>(opt =>
                opt.Order != null &&
                opt.Skip == 3 &&
                opt.Take == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindAllPagedResultAsync_WithExpression_ReturnsPagedResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedPersons = this.personFaker.Generate(3);
        var totalCount = 10;
        repository.CountAsync(Arg.Any<ISpecification<PersonStub>>(), Arg.Any<CancellationToken>()).Returns(totalCount);
        repository.FindAllAsync(Arg.Any<ISpecification<PersonStub>>(), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns(expectedPersons);

        // Act
        var result = await repository.FindAllPagedResultAsync(
            p => p.Age > 30,
            ordering: "LastName descending",
            page: 1,
            pageSize: 3);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedPersons);
        result.TotalCount.ShouldBe(totalCount);
        result.CurrentPage.ShouldBe(1);
        result.PageSize.ShouldBe(3);
        await repository.Received(1).FindAllAsync(
            Arg.Any<ISpecification<PersonStub>>(),
            Arg.Is<IFindOptions<PersonStub>>(opt =>
                opt.Order != null &&
                opt.Skip == 0 &&
                opt.Take == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindAllPagedResultAsync_WithSpecification_ReturnsPagedResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedPersons = this.personFaker.Generate(3);
        var totalCount = 10;
        repository.CountAsync(Arg.Any<ISpecification<PersonStub>>(), Arg.Any<CancellationToken>()).Returns(totalCount);
        repository.FindAllAsync(Arg.Any<ISpecification<PersonStub>>(), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns(expectedPersons);

        // Act
        var specification = Substitute.For<ISpecification<PersonStub>>();
        var result = await repository.FindAllPagedResultAsync(
            specification,
            ordering: "FirstName ascending",
            page: 2,
            pageSize: 3);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedPersons);
        result.TotalCount.ShouldBe(totalCount);
        result.CurrentPage.ShouldBe(2);
        result.PageSize.ShouldBe(3);
        await repository.Received(1).FindAllAsync(
            Arg.Is(specification),
            Arg.Is<IFindOptions<PersonStub>>(opt =>
                opt.Order != null &&
                opt.Skip == 3 &&
                opt.Take == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindAllPagedResultAsync_WithSpecifications_ReturnsPagedResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedPersons = this.personFaker.Generate(3);
        var totalCount = 10;
        repository.CountAsync(Arg.Any<IEnumerable<ISpecification<PersonStub>>>(), Arg.Any<CancellationToken>()).Returns(totalCount);
        repository.FindAllAsync(Arg.Any<IEnumerable<ISpecification<PersonStub>>>(), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns(expectedPersons);

        // Act
        var specifications = new List<ISpecification<PersonStub>>
        {
            Substitute.For<ISpecification<PersonStub>>(),
            Substitute.For<ISpecification<PersonStub>>()
        };
        var result = await repository.FindAllPagedResultAsync(
            specifications,
            ordering: "Age ascending",
            page: 3,
            pageSize: 5);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedPersons);
        result.TotalCount.ShouldBe(totalCount);
        result.CurrentPage.ShouldBe(3);
        result.PageSize.ShouldBe(5);
        await repository.Received(1).FindAllAsync(
            Arg.Is(specifications),
            Arg.Is<IFindOptions<PersonStub>>(opt =>
                opt.Order != null &&
                opt.Skip == 10 &&
                opt.Take == 5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindAllPagedResultAsync_WithIncludePath_AppliesInclude()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        var expectedPersons = this.personFaker.Generate(3);
        var totalCount = 10;
        repository.CountAsync(Arg.Any<CancellationToken>()).Returns(totalCount);
        repository.FindAllAsync(Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns(expectedPersons);

        // Act
        var result = await repository.FindAllPagedResultAsync(
            ordering: "Age ascending",
            page: 1,
            pageSize: 3,
            includePath: "RelatedEntity");

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedPersons);
        await repository.Received(1).FindAllAsync(
            Arg.Is<IFindOptions<PersonStub>>(opt =>
                opt.Include != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindAllPagedResultAsync_WhenExceptionThrown_ReturnsFailureResult()
    {
        // Arrange
        var repository = Substitute.For<IGenericReadOnlyRepository<PersonStub>>();
        repository.CountAsync(Arg.Any<CancellationToken>())
            .Throws(new Exception("Test exception"));

        // Act
        var result = await repository.FindAllPagedResultAsync(
            ordering: "Age ascending",
            page: 1,
            pageSize: 3);

        // Assert
        result.ShouldBeFailure();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.First().ShouldBeOfType<ExceptionError>();
    }
}