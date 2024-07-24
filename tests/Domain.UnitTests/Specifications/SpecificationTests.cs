// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Specifications;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Specifications;
using System;
using System.Collections.Generic;
using Xunit;
using Shouldly;

[UnitTest("Domain")]
public class SpecificationTests
{
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
        sut.IsSatisfiedBy(person).ShouldBeTrue();
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
        sut.IsSatisfiedBy(person).ShouldBeTrue();
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
        sut.IsSatisfiedBy(person).ShouldBeTrue();
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
        sut.IsSatisfiedBy(person).ShouldBeTrue();
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
        var sut = new List<ISpecification<PersonStub>>
        {
            new Specification<PersonStub>(p => p.Age > 18)
        };
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
        var sut = new List<ISpecification<PersonStub>>
        {
            new Specification<PersonStub>(p => p.Age > 30)
        };
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
        var sut = new List<ISpecification<PersonStub>>
        {
            new Specification<PersonStub>(p => p.Age > 18),
            new Specification<PersonStub>(p => p.FirstName == "John")
        };
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
        var sut = new List<ISpecification<PersonStub>>
        {
            new Specification<PersonStub>(p => p.Age > 18),
            new Specification<PersonStub>(p => p.FirstName == "Jane")
        };
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
        SpecificationExtensions.IsSatisfiedBy(sut, person1).ShouldBeTrue();
        SpecificationExtensions.IsSatisfiedBy(sut, person2).ShouldBeTrue();
        SpecificationExtensions.IsSatisfiedBy(sut, person3).ShouldBeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_SpecificationIncludingId_ShouldWorkCorrectly()
    {
        // Arrange
        var specificId = Guid.NewGuid();
        var sut = new List<ISpecification<PersonStub>>
        {
            new Specification<PersonStub>(p => p.Id == specificId),
            new Specification<PersonStub>(p => p.Age > 18)
        };
        var person1 = new PersonStub { Id = specificId, Age = 25, FirstName = "John" };
        var person2 = new PersonStub { Id = Guid.NewGuid(), Age = 25, FirstName = "John" };

        // Act & Assert
        SpecificationExtensions.IsSatisfiedBy(sut, person1).ShouldBeTrue();
        SpecificationExtensions.IsSatisfiedBy(sut, person2).ShouldBeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_AllUnsatisfiedSpecifications_ShouldReturnFalse()
    {
        // Arrange
        var sut = new List<ISpecification<PersonStub>>
        {
            new Specification<PersonStub>(p => p.Age > 30),
            new Specification<PersonStub>(p => p.FirstName == "Jane"),
            new Specification<PersonStub>(p => p.LastName == "Doe")
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
        var sut = new List<ISpecification<PersonStub>>
        {
            new Specification<PersonStub>(p => p.Age > 18).Not()
        };
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
}