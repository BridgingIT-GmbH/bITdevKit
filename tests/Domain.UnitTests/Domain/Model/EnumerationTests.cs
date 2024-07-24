// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using System;
using System.Collections.Generic;
using System.Linq;
using BridgingIT.DevKit.Domain.Model;
using Xunit;
using Shouldly;

[UnitTest("Domain")]
[Trait("Category", "Domain")]
public class EnumerationTests
{
    [Fact]
    public void GetAll_ShouldReturnAllEnumerations()
    {
        // Arrange & Act
        var sut = Enumeration.GetAll<StubStatus>();

        // Assert
        sut.ShouldNotBeEmpty();
        sut.Count().ShouldBe(3);
        sut.ShouldContain(e => e.Id == 1 && e.Value == "Stub01" && e.Code == "S1");
        sut.ShouldContain(e => e.Id == 2 && e.Value == "Stub02" && e.Code == "S2");
        sut.ShouldContain(e => e.Id == 3 && e.Value == "Stub03" && e.Code == "S3");
    }

    [Fact]
    public void From_WithValidId_ShouldReturnCorrectEnumeration()
    {
        // Arrange & Act
        var sut = Enumeration.FromId<StubStatus>(2);

        // Assert
        sut.ShouldNotBeNull();
        sut.Id.ShouldBe(2);
        sut.Value.ShouldBe("Stub02");
        sut.Code.ShouldBe("S2");
        sut.Description.ShouldBe("Lorem Ipsum");
    }

    [Fact]
    public void From_WithInvalidId_ShouldThrowInvalidOperationException()
    {
        // Arrange & Act & Assert
        Should.Throw<InvalidOperationException>(() => Enumeration.FromId<StubStatus>(0))
            .Message.ShouldBe("'0' is not a valid id for BridgingIT.DevKit.Domain.UnitTests.Domain.Model.StubStatus");
    }

    [Fact]
    public void From_WithValidName_ShouldReturnCorrectEnumeration()
    {
        // Arrange & Act
        var sut = Enumeration.FromValue<StubStatus>("Stub03");

        // Assert
        sut.ShouldNotBeNull();
        sut.Id.ShouldBe(3);
        sut.Value.ShouldBe("Stub03");
        sut.Code.ShouldBe("S3");
    }

    [Fact]
    public void From_WithValidNameCaseInsensitive_ShouldReturnCorrectEnumeration()
    {
        // Arrange & Act
        var sut = Enumeration.FromValue<StubStatus>("STUB03");

        // Assert
        sut.ShouldNotBeNull();
        sut.Id.ShouldBe(3);
        sut.Value.ShouldBe("Stub03");
        sut.Code.ShouldBe("S3");
    }

    [Fact]
    public void From_WithInvalidName_ShouldThrowInvalidOperationException()
    {
        // Arrange & Act & Assert
        Should.Throw<InvalidOperationException>(() => Enumeration.FromValue<StubStatus>("Stub00"))
            .Message.ShouldBe("'Stub00' is not a valid value for BridgingIT.DevKit.Domain.UnitTests.Domain.Model.StubStatus");
    }

    [Fact]
    public void Equals_WithSameEnumeration_ShouldReturnTrue()
    {
        // Arrange
        var sut = Enumeration.FromValue<StubStatus>("Stub03");
        var other = Enumeration.FromValue<StubStatus>("Stub03");

        // Act
        var result = sut.Equals(other);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Equals_WithDifferentEnumeration_ShouldReturnFalse()
    {
        // Arrange
        var sut = Enumeration.FromValue<StubStatus>("Stub01");
        var other = Enumeration.FromValue<StubStatus>("Stub03");

        // Act
        var result = sut.Equals(other);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithNull_ShouldReturnFalse()
    {
        // Arrange
        var sut = Enumeration.FromValue<StubStatus>("Stub01");

        // Act
        var result = sut.Equals(null);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithDifferentType_ShouldReturnFalse()
    {
        // Arrange
        var sut = Enumeration.FromValue<StubStatus>("Stub01");
        var differentType = new object();

        // Act
        var result = sut.Equals(differentType);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithSameEnumerationCaseInsensitive_ShouldReturnTrue()
    {
        // Arrange
        var sut = Enumeration.FromValue<StubStatus>("Stub03");
        var other = Enumeration.FromValue<StubStatus>("STUB03");

        // Act
        var result = sut.Equals(other);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void GetHashCode_ShouldBeConsistent()
    {
        // Arrange
        var sut = Enumeration.FromValue<StubStatus>("Stub01");
        var other = Enumeration.FromValue<StubStatus>("Stub01");

        // Act & Assert
        sut.GetHashCode().ShouldBe(other.GetHashCode());
    }

    [Fact]
    public void ToString_ShouldReturnName()
    {
        // Arrange
        var sut = Enumeration.FromValue<StubStatus>("Stub01");

        // Act
        var result = sut.ToString();

        // Assert
        result.ShouldBe("Stub01");
    }

    [Fact]
    public void CompareTo_ShouldOrderCorrectly()
    {
        // Arrange
        var sut1 = StubStatus.Stub01;
        var sut2 = StubStatus.Stub02;
        var sut3 = StubStatus.Stub03;

        // Act
        var orderedList = new List<StubStatus> { sut3, sut1, sut2 };
        orderedList.Sort();

        // Assert
        orderedList.ShouldBe([sut1, sut2, sut3]);
    }

    [Fact]
    public void Code_ShouldBeSetCorrectly()
    {
        // Arrange & Act
        var sut1 = StubStatus.Stub01;
        var sut2 = StubStatus.Stub02;
        var sut3 = StubStatus.Stub03;

        // Assert
        sut1.Code.ShouldBe("S1");
        sut2.Code.ShouldBe("S2");
        sut3.Code.ShouldBe("S3");
    }

    [Fact]
    public void Description_ShouldBeSetCorrectly()
    {
        // Arrange & Act
        var sut = StubStatus.Stub01;

        // Assert
        sut.Description.ShouldBe("Lorem Ipsum");
    }

    [Fact]
    public void GetByCode_WithValidCode_ShouldReturnCorrectEnumeration()
    {
        // Arrange & Act
        var sut = StubStatus.GetByCode("S2");

        // Assert
        sut.ShouldNotBeNull();
        sut.Id.ShouldBe(2);
        sut.Value.ShouldBe("Stub02");
        sut.Code.ShouldBe("S2");
        sut.Description.ShouldBe("Lorem Ipsum");
    }

    [Fact]
    public void GetByCode_WithInvalidCode_ShouldReturnNull()
    {
        // Arrange & Act
        var sut = StubStatus.GetByCode("InvalidCode");

        // Assert
        sut.ShouldBeNull();
    }

    [Fact]
    public void GetByCode_WithNullCode_ShouldReturnNull()
    {
        // Arrange & Act
        var sut = StubStatus.GetByCode(null);

        // Assert
        sut.ShouldBeNull();
    }
}

public class StubStatus(int id, string value, string code, string description)
    : Enumeration(id, value)
{
    public static StubStatus Stub01 = new(1, "Stub01", "S1", "Lorem Ipsum");
    public static StubStatus Stub02 = new(2, "Stub02", "S2", "Lorem Ipsum");
    public static StubStatus Stub03 = new(3, "Stub03", "S3", "Lorem Ipsum");

    public string Code { get; } = code;

    public string Description { get; } = description;

    public static IEnumerable<StubStatus> GetAll() =>
        GetAll<StubStatus>();

    public static StubStatus GetByCode(string code) =>
        GetAll<StubStatus>().FirstOrDefault(e => e.Code == code);
}