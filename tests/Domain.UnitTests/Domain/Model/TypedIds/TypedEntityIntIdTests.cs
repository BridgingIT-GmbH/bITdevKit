// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using System;
using BridgingIT.DevKit.Domain.Model;
using static BridgingIT.DevKit.Domain.UnitTests.Domain.Model.TypedEntityGuidIdTests;
using static BridgingIT.DevKit.Domain.UnitTests.Domain.Model.TypedEntityIntIdTests;

[UnitTest("Domain")]
public class TypedEntityIntIdTests
{
    [Fact]
    public void Create_WithoutParameters_ShouldThrowNotImplementedException()
    {
        Assert.Throws<NotImplementedException>(() => StubIntEntityId.Create());
        //var id1 = StubIntEntityId.Create();
        //var id2 = StubIntEntityId.Create();

        //Assert.NotEqual(id1, id2);
        //Assert.NotEqual(0, id1.Value);
        //Assert.NotEqual(0, id2.Value);
    }

    [Fact]
    public void Create_WithInt_ShouldCreateIdWithSpecifiedValue()
    {
        const int intValue = 42;
        var id = StubIntEntityId.Create(intValue);

        Assert.Equal(intValue, id.Value);
    }

    [Fact]
    public void Create_WithValidString_ShouldCreateIdWithParsedValue()
    {
        const string intString = "42";
        var id = StubIntEntityId.Create(intString);

        Assert.Equal(int.Parse(intString), id.Value);
    }

    [Fact]
    public void Create_WithInvalidString_ShouldThrowFormatException()
    {
        Assert.Throws<FormatException>(() => StubIntEntityId.Create("invalid-int"));
    }

    [Fact]
    public void Create_WithNullOrWhiteSpace_ShouldThrowException()
    {
        Assert.Throws<ArgumentException>(() => StubIntEntityId.Create(null));
        Assert.Throws<ArgumentException>(() => StubIntEntityId.Create(string.Empty));
        Assert.Throws<FormatException>(() => StubIntEntityId.Create("   "));
    }

    [Fact]
    public void ImplicitCast_ToInt_ShouldReturnValue()
    {
        const int intValue = 42;
        int castedInt = StubIntEntityId.Create(intValue);

        Assert.Equal(intValue, castedInt);
    }

    [Fact]
    public void ImplicitCast_ToString_ShouldReturnStringRepresentation()
    {
        const int intValue = 42;
        string castedString = StubIntEntityId.Create(intValue);

        Assert.Equal(intValue.ToString(), castedString);
    }

    [Fact]
    public void ImplicitCast_FromInt_ShouldCreateStubIntEntityId()
    {
        const int intValue = 42;
        StubIntEntityId id = intValue;

        Assert.Equal(intValue, id.Value);
    }

    [Fact]
    public void IsEmpty_WithZero_ShouldReturnTrue()
    {
        var id = StubIntEntityId.Create(0);

        Assert.True(id.IsEmpty);
    }

    [Fact]
    public void IsEmpty_WithNonZero_ShouldReturnFalse()
    {
        var id = StubIntEntityId.Create(42);

        Assert.False(id.IsEmpty);
    }

    [Fact]
    public void Equals_SameValue_ShouldReturnTrue()
    {
        const int intValue = 42;
        var id1 = StubIntEntityId.Create(intValue);
        var id2 = StubIntEntityId.Create(intValue);

        Assert.True(id1.Equals(id2));
        Assert.True(id1 == id2);
        Assert.False(id1 != id2);
    }

    [Fact]
    public void Equals_DifferentValue_ShouldReturnFalse()
    {
        var id1 = StubIntEntityId.Create(42);
        var id2 = StubIntEntityId.Create(43);

        Assert.False(id1.Equals(id2));
        Assert.False(id1 == id2);
        Assert.True(id1 != id2);
    }

    [Fact]
    public void Equals_Null_ShouldReturnFalse()
    {
        var id = StubIntEntityId.Create(42);

        Assert.False(id.Equals(null));
        Assert.False(id == null);
        Assert.True(id != null);
    }

    [Fact]
    public void GetHashCode_SameValue_ShouldReturnSameHashCode()
    {
        const int intValue = 42;
        var id1 = StubIntEntityId.Create(intValue);
        var id2 = StubIntEntityId.Create(intValue);

        Assert.Equal(id1.GetHashCode(), id2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValue_ShouldReturnDifferentHashCode()
    {
        var id1 = StubIntEntityId.Create(42);
        var id2 = StubIntEntityId.Create(43);

        Assert.NotEqual(id1.GetHashCode(), id2.GetHashCode());
    }

    [Fact]
    public void ToString_ShouldReturnStringRepresentationOfValue()
    {
        const int intValue = 42;
        var id = StubIntEntityId.Create(intValue);

        Assert.Equal(intValue.ToString(), id.ToString());
    }

    [Fact]
    public void Deserialize_WithValidId_ShouldCreateIdWithParsedValue()
    {
        var entity = new StubIntEntity
        {
            Id = StubIntEntityId.Create(42)
        };

        var serialized = new SystemTextJsonSerializer().SerializeToString(entity);
        var deserialized = new SystemTextJsonSerializer().Deserialize<StubIntEntity>(serialized);

        deserialized.Id.Value.ShouldBe(42);
        deserialized.Id.IsEmpty.ShouldBeFalse();
    }

    [TypedEntityId<int>]
    public partial class StubIntEntity : Entity<StubIntEntityId>
    {
    }
}