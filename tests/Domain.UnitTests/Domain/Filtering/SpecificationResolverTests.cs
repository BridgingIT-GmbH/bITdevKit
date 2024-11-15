// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain;

using System;
using Xunit;

public class SpecificationResolverTests
{
     [Fact]
    public void RegisterSpecification_WithValidInputAndGenerics_RegistersSuccessfully()
    {
        // Arrange & Act
        SpecificationResolver.Register<PersonStub, AdultSpecification>("AdultSpecification");

        // Assert
        Assert.True(SpecificationResolver.IsRegistered("AdultSpecification"));

        // Cleanup
        SpecificationResolver.Clear();
    }

    [Fact]
    public void RegisterSpecification_WithValidInput_RegistersSuccessfully()
    {
        // Arrange & Act
        SpecificationResolver.Register<PersonStub>(typeof(AdultSpecification), "AdultSpecification");

        // Assert
        Assert.True(SpecificationResolver.IsRegistered("AdultSpecification"));

        // Cleanup
        SpecificationResolver.Clear();
    }

    [Fact]
    public void RegisterSpecification_WithInvalidType_ThrowsArgumentException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SpecificationResolver.Register<PersonStub>(typeof(string), "InvalidSpec"));
    }

    [Fact]
    public void RegisterSpecification_WithDuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange
        SpecificationResolver.Register<PersonStub, AdultSpecification>("AdultSpecification");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            SpecificationResolver.Register<PersonStub, AdultSpecification>("AdultSpecification"));

        // Cleanup
        SpecificationResolver.Clear();
    }

    [Fact]
    public void ResolveSpecification_WithRegisteredSpecification_ReturnsCorrectInstance()
    {
        // Arrange
        SpecificationResolver.Register<PersonStub, AdultSpecification>("AdultSpecification");

        // Act
        var result = SpecificationResolver.Resolve<PersonStub>("AdultSpecification", [18]);

        // Assert
        Assert.IsType<AdultSpecification>(result);

        // Cleanup
        SpecificationResolver.Clear();
    }

    [Fact]
    public void ResolveSpecification_WithUnregisteredSpecification_ThrowsArgumentException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SpecificationResolver.Resolve<PersonStub>("UnregisteredSpec", []));
    }

    [Fact]
    public void GetSpecificationType_WithRegisteredSpecification_ReturnsCorrectType()
    {
        // Arrange
        SpecificationResolver.Register<PersonStub, AdultSpecification>("AdultSpecification");

        // Act
        var result = SpecificationResolver.GetType("AdultSpecification");

        // Assert
        Assert.Equal(typeof(AdultSpecification), result);

        // Cleanup
        SpecificationResolver.Clear();
    }

    [Fact]
    public void GetSpecificationType_WithUnregisteredSpecification_ThrowsArgumentException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SpecificationResolver.GetType("UnregisteredSpec"));
    }

    [Fact]
    public void IsSpecificationRegistered_WithRegisteredSpecification_ReturnsTrue()
    {
        // Arrange
        SpecificationResolver.Register<PersonStub, AdultSpecification>("AdultSpecification");

        // Act
        var result = SpecificationResolver.IsRegistered("AdultSpecification");

        // Assert
        Assert.True(result);

        // Cleanup
        SpecificationResolver.Clear();
    }

    [Fact]
    public void IsSpecificationRegistered_WithUnregisteredSpecification_ReturnsFalse()
    {
        // Act
        var result = SpecificationResolver.IsRegistered("UnregisteredSpec");

        // Assert
        Assert.False(result);
    }
}