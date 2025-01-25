// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests;

using System.Linq.Dynamic.Core.Exceptions;
using BridgingIT.DevKit.Domain;

[UnitTest("Domain")]
public class SpecificationTests
{
    private readonly Faker faker = new();

    [Fact]
    public void Specification_ExpressionCtorIsSatisfiedBy_ShouldReturnTrue()
    {
        // Arrange
        var sut = new Specification<PersonStub>(e => e.FirstName == "John");
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };

        // Act
        var result = sut.IsSatisfiedBy(person);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Specification_ExpressionCtorIsNotSatisfiedBy_ShouldReturnFalse()
    {
        // Arrange
        var sut = new Specification<PersonStub>(e => e.FirstName == "John");
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "Jane" };

        // Act
        var result = sut.IsSatisfiedBy(person);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Specification_And_ShouldCombineSpecifications()
    {
        // Arrange
        var spec1 = new Specification<PersonStub>(p => p.Age > 18);
        var spec2 = new Specification<PersonStub>(p => p.FirstName == "John");
        var person = new PersonStub { Id = Guid.NewGuid(), Age = 25, FirstName = "John" };

        // Act
        var sut = spec1.And(spec2);

        // Assert
        sut.IsSatisfiedBy(person)
            .ShouldBeTrue();
    }

    [Fact]
    public void Specification_Or_ShouldCombineSpecifications()
    {
        // Arrange
        var spec1 = new Specification<PersonStub>(p => p.Age > 18);
        var spec2 = new Specification<PersonStub>(p => p.FirstName == "John");
        var person = new PersonStub { Id = Guid.NewGuid(), Age = 15, FirstName = "John" };

        // Act
        var sut = spec1.Or(spec2);

        // Assert
        sut.IsSatisfiedBy(person)
            .ShouldBeTrue();
    }

    [Fact]
    public void Specification_Not_ShouldNegateSpecification()
    {
        // Arrange
        var spec = new Specification<PersonStub>(p => p.Age > 18);
        var person = new PersonStub { Id = Guid.NewGuid(), Age = 15 };

        // Act
        var sut = spec.Not();

        // Assert
        sut.IsSatisfiedBy(person)
            .ShouldBeTrue();
    }

    [Fact]
    public void Specification_ComplexCombination_ShouldWorkCorrectly()
    {
        // Arrange
        var spec1 = new Specification<PersonStub>(p => p.Age > 18);
        var spec2 = new Specification<PersonStub>(p => p.FirstName == "John");
        var spec3 = new Specification<PersonStub>(p => p.LastName == "Doe");
        var person = new PersonStub { Id = Guid.NewGuid(), Age = 25, FirstName = "John", LastName = "Smith" };

        // Act
        var sut = spec1.And(spec2.Or(spec3));

        // Assert
        sut.IsSatisfiedBy(person)
            .ShouldBeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_EmptySpecificationCollection_ShouldReturnTrue()
    {
        // Arrange
        var sut = new List<ISpecification<PersonStub>>();
        var person = new PersonStub { Id = Guid.NewGuid(), Age = 25, FirstName = "John" };

        // Act
        var result = SpecificationExtensions.IsSatisfiedBy(sut, person);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_SingleSatisfiedSpecification_ShouldReturnTrue()
    {
        // Arrange
        var sut = new List<ISpecification<PersonStub>> { new Specification<PersonStub>(p => p.Age > 18) };
        var person = new PersonStub { Id = Guid.NewGuid(), Age = 25, FirstName = "John" };

        // Act
        var result = SpecificationExtensions.IsSatisfiedBy(sut, person);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_SingleUnsatisfiedSpecification_ShouldReturnFalse()
    {
        // Arrange
        var sut = new List<ISpecification<PersonStub>> { new Specification<PersonStub>(p => p.Age > 30) };
        var person = new PersonStub { Id = Guid.NewGuid(), Age = 25, FirstName = "John" };

        // Act
        var result = SpecificationExtensions.IsSatisfiedBy(sut, person);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_MultipleAllSatisfiedSpecifications_ShouldReturnTrue()
    {
        // Arrange
        var sut = new List<ISpecification<PersonStub>> { new Specification<PersonStub>(p => p.Age > 18), new Specification<PersonStub>(p => p.FirstName == "John") };
        var person = new PersonStub { Id = Guid.NewGuid(), Age = 25, FirstName = "John" };

        // Act
        var result = SpecificationExtensions.IsSatisfiedBy(sut, person);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_MultipleSomeUnsatisfiedSpecifications_ShouldReturnFalse()
    {
        // Arrange
        var sut = new List<ISpecification<PersonStub>> { new Specification<PersonStub>(p => p.Age > 18), new Specification<PersonStub>(p => p.FirstName == "Jane") };
        var person = new PersonStub { Id = Guid.NewGuid(), Age = 25, FirstName = "John" };

        // Act
        var result = SpecificationExtensions.IsSatisfiedBy(sut, person);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_NullSpecificationCollection_ShouldReturnTrue()
    {
        // Arrange
        List<ISpecification<PersonStub>> sut = null;
        var person = new PersonStub { Id = Guid.NewGuid(), Age = 25, FirstName = "John" };

        // Act
        var result = SpecificationExtensions.IsSatisfiedBy(sut, person);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_ComplexSpecifications_ShouldWorkCorrectly()
    {
        // Arrange
        var sut = new List<ISpecification<PersonStub>>
        {
            new Specification<PersonStub>(p => p.Age > 18),
            new Specification<PersonStub>(p => p.FirstName == "John").Or(new Specification<PersonStub>(p => p.LastName == "Doe")),
            new Specification<PersonStub>(p => p.Age < 60)
        };
        var person1 = new PersonStub { Id = Guid.NewGuid(), Age = 25, FirstName = "John", LastName = "Smith" };
        var person2 = new PersonStub { Id = Guid.NewGuid(), Age = 35, FirstName = "Jane", LastName = "Doe" };
        var person3 = new PersonStub { Id = Guid.NewGuid(), Age = 65, FirstName = "John", LastName = "Doe" };

        // Act & Assert
        SpecificationExtensions.IsSatisfiedBy(sut, person1)
            .ShouldBeTrue();
        SpecificationExtensions.IsSatisfiedBy(sut, person2)
            .ShouldBeTrue();
        SpecificationExtensions.IsSatisfiedBy(sut, person3)
            .ShouldBeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_SpecificationIncludingId_ShouldWorkCorrectly()
    {
        // Arrange
        var specificId = Guid.NewGuid();
        var sut = new List<ISpecification<PersonStub>> { new Specification<PersonStub>(p => p.Id == specificId), new Specification<PersonStub>(p => p.Age > 18) };
        var person1 = new PersonStub { Id = specificId, Age = 25, FirstName = "John" };
        var person2 = new PersonStub { Id = Guid.NewGuid(), Age = 25, FirstName = "John" };

        // Act & Assert
        SpecificationExtensions.IsSatisfiedBy(sut, person1)
            .ShouldBeTrue();
        SpecificationExtensions.IsSatisfiedBy(sut, person2)
            .ShouldBeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_AllUnsatisfiedSpecifications_ShouldReturnFalse()
    {
        // Arrange
        var sut = new List<ISpecification<PersonStub>>
        {
            new Specification<PersonStub>(p => p.Age > 30), new Specification<PersonStub>(p => p.FirstName == "Jane"), new Specification<PersonStub>(p => p.LastName == "Doe")
        };
        var person = new PersonStub { Id = Guid.NewGuid(), Age = 25, FirstName = "John", LastName = "Smith" };

        // Act
        var result = SpecificationExtensions.IsSatisfiedBy(sut, person);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_ComplexUnsatisfiedSpecification_ShouldReturnFalse()
    {
        // Arrange
        var sut = new List<ISpecification<PersonStub>>
        {
            new Specification<PersonStub>(p => p.Age > 18)
                .And(new Specification<PersonStub>(p => p.FirstName == "John"))
                .And(new Specification<PersonStub>(p => p.LastName == "Doe"))
        };
        var person = new PersonStub { Id = Guid.NewGuid(), Age = 25, FirstName = "John", LastName = "Smith" };

        // Act
        var result = SpecificationExtensions.IsSatisfiedBy(sut, person);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_NotSpecificationUnsatisfied_ShouldReturnFalse()
    {
        // Arrange
        var sut = new List<ISpecification<PersonStub>> { new Specification<PersonStub>(p => p.Age > 18).Not() };
        var person = new PersonStub { Id = Guid.NewGuid(), Age = 25, FirstName = "John" };

        // Act
        var result = SpecificationExtensions.IsSatisfiedBy(sut, person);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_OrSpecificationUnsatisfied_ShouldReturnFalse()
    {
        // Arrange
        var sut = new List<ISpecification<PersonStub>>
        {
            new Specification<PersonStub>(p => p.Age < 18)
                .Or(new Specification<PersonStub>(p => p.FirstName == "Jane"))
        };
        var person = new PersonStub { Id = Guid.NewGuid(), Age = 25, FirstName = "John" };

        // Act
        var result = SpecificationExtensions.IsSatisfiedBy(sut, person);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Specification_DynamicExpressionCtorIsSatisfiedBy_ShouldReturnTrue()
    {
        // Arrange
        var firstName = this.faker.Person.FirstName;
        var sut = new Specification<PersonStub>("FirstName == @0", firstName);
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = firstName };

        // Act
        var result = sut.IsSatisfiedBy(person);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Specification_DynamicExpressionOrCtorIsSatisfiedBy_ShouldReturnTrue()
    {
        // Arrange
        var firstName = this.faker.Person.FirstName;
        var age = 10;
        var sut = new Specification<PersonStub>("FirstName == @0 OR Age == @1", firstName, age);
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = firstName, Age = age };

        // Act
        var result = sut.IsSatisfiedBy(person);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Specification_DynamicExpressionAndCtorIsSatisfiedBy_ShouldReturnTrue()
    {
        // Arrange
        var firstName = this.faker.Person.FirstName;
        var age = 10;
        var sut = new Specification<PersonStub>("FirstName == @0 AND Age == @1", firstName, age);
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = firstName, Age = age };

        // Act
        var result = sut.IsSatisfiedBy(person);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Specification_DynamicExpressionNoValuesCtorIsSatisfiedBy_ShouldReturnTrue()
    {
        // Arrange
        var firstName = this.faker.Person.FirstName;
        var sut = new Specification<PersonStub>($"FirstName == \"{firstName}\"");
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = firstName };

        // Act
        var result = sut.IsSatisfiedBy(person);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Specification_DynamicExpressionCtorIsNotSatisfiedBy_ShouldReturnFalse()
    {
        // Arrange
        var firstName = this.faker.Person.FirstName;
        var sut = new Specification<PersonStub>("FirstName == @0", firstName);
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "unknown" };

        // Act
        var result = sut.IsSatisfiedBy(person);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Specification_DynamicExpressionCtorWithMultipleParameters_ShouldWorkCorrectly()
    {
        // Arrange
        var minAge = this.faker.Random.Number(18, 30);
        var maxAge = this.faker.Random.Number(31, 60);
        var sut = new Specification<PersonStub>("Age >= @0 && Age <= @1", minAge, maxAge);
        var person = new PersonStub { Id = Guid.NewGuid(), Age = this.faker.Random.Number(minAge, maxAge) };

        // Act
        var result = sut.IsSatisfiedBy(person);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Specification_DynamicExpressionCtorWithInvalidExpression_ShouldThrowException()
    {
        // Arrange
        var invalidExpression = "InvalidProperty == @0";

        // Act & Assert
        Should.Throw<ParseException>(()
            => new Specification<PersonStub>(invalidExpression, "value").ToExpression());
    }

    [Fact]
    public void Specification_DynamicExpressionCtorWithComplexExpression_ShouldWorkCorrectly()
    {
        // Arrange
        var minAge = this.faker.Random.Number(18, 30);
        var firstName = this.faker.Person.FirstName;
        var sut = new Specification<PersonStub>("Age > @0 || (FirstName == @1 && LastName.StartsWith(\"D\"))", minAge, firstName);
        var person = new PersonStub { Id = Guid.NewGuid(), Age = minAge - 1, FirstName = firstName, LastName = "Doe" };

        // Act
        var result = sut.IsSatisfiedBy(person);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ToExpression_DynamicExpressionCtor_ShouldReturnCorrectExpression()
    {
        // Arrange
        var age = this.faker.Random.Number(18, 60);
        var sut = new Specification<PersonStub>("Age == @0", age);

        // Act
        var expression = sut.ToExpression();

        // Assert
        expression.ShouldNotBeNull();
        expression.Body.NodeType.ShouldBe(System.Linq.Expressions.ExpressionType.Equal);
    }

    [Fact]
    public void ToPredicate_DynamicExpressionCtor_ShouldReturnCorrectPredicate()
    {
        // Arrange
        var age = this.faker.Random.Number(18, 60);
        var sut = new Specification<PersonStub>("Age == @0", age);

        // Act
        var predicate = sut.ToPredicate();

        // Assert
        predicate.ShouldNotBeNull();
        predicate(new PersonStub { Age = age }).ShouldBeTrue();
        predicate(new PersonStub { Age = age + 1 }).ShouldBeFalse();
    }
}