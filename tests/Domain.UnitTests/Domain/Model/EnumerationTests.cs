// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using System;
using System.Collections.Generic;
using System.Linq;
using BridgingIT.DevKit.Domain.Model;

[UnitTest("Domain")]
[Trait("Category", "Domain")]
public class EnumerationTests
{
    [Fact]
    public void GetAllTest()
    {
        // Arrange & Act
        var sut = Enumeration.GetAll<StubEnumeration>();

        // Assert
        sut.ShouldNotBeEmpty();
        sut.Count().ShouldBe(3);
    }

    [Fact]
    public void GetByIdTest()
    {
        // Arrange & Act
        var sut = Enumeration.From<StubEnumeration>(2);

        // Assert
        sut.ShouldNotBeNull();
        sut.Id.ShouldBe(2);
    }

    [Fact]
    public void GetByInvalidIdTest()
    {
        // Arrange & Act/assert
        Should.Throw<InvalidOperationException>(() => Enumeration.From<StubEnumeration>(0))
            .Message.ShouldBe("'0' is not a valid id for BridgingIT.DevKit.Domain.UnitTests.Domain.Model.StubEnumeration");
    }

    [Fact]
    public void GetByNameTest()
    {
        // Arrange & Act
        var sut = Enumeration.From<StubEnumeration>("Stub03");

        // Assert
        sut.ShouldNotBeNull();
        sut.Id.ShouldBe(3);
    }

    [Fact]
    public void GetByInvalidNameTest()
    {
        // Arrange & Act/assert
        Should.Throw<InvalidOperationException>(() => Enumeration.From<StubEnumeration>("Stub00"))
            .Message.ShouldBe("'Stub00' is not a valid name for BridgingIT.DevKit.Domain.UnitTests.Domain.Model.StubEnumeration");
    }

    [Fact]
    public void EqualsTest()
    {
        // Arrange & Act
        var sut = Enumeration.From<StubEnumeration>("Stub03")
            .Equals(Enumeration.From<StubEnumeration>("Stub03"));

        // Assert
        sut.ShouldBeTrue();
    }

    [Fact]
    public void NotEqualsTest()
    {
        // Arrange & Act
        var sut = Enumeration.From<StubEnumeration>("Stub01")
            .Equals(Enumeration.From<StubEnumeration>("Stub03"));

        // Assert
        sut.ShouldBeFalse();
    }
}

public class StubEnumeration : Enumeration
{
    public static StubEnumeration Stub01 = new(1, "Stub01");
    public static StubEnumeration Stub02 = new(2, "Stub02");
    public static StubEnumeration Stub03 = new(3, "Stub03");

    public StubEnumeration(int id, string name)
        : base(id, name)
    {
    }

    public static IEnumerable<StubEnumeration> GetAll() => GetAll<StubEnumeration>();
}
