// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using System;
using BridgingIT.DevKit.Domain.Model;
using static BridgingIT.DevKit.Domain.UnitTests.Domain.Model.TypedEntityGuidIdTests;
using static BridgingIT.DevKit.Domain.UnitTests.Domain.Model.TypedEntityIntIdTests;
using static BridgingIT.DevKit.Domain.UnitTests.Domain.Model.TypedEntityLongIdTests;

[UnitTest("Domain")]
public class TypedEntityLongIdTests
{
    [Fact]
    public void Create_WithoutParameters_ShouldThrowNotImplementedException()
    {
        Assert.Throws<NotImplementedException>(() => StubLongEntityId.Create());
    }

    [Fact]
    public void Create_WithLong_ShouldCreateIdWithSpecifiedValue()
    {
        const long longValue = 9223372036854775807; // Max long value
        var id = StubLongEntityId.Create(longValue);

        Assert.Equal(longValue, id.Value);
    }

    [Fact]
    public void Create_WithValidString_ShouldCreateIdWithParsedValue()
    {
        const string longString = "9223372036854775807"; // Max long value
        var id = StubLongEntityId.Create(longString);

        Assert.Equal(long.Parse(longString), id.Value);
    }

    [Fact]
    public void Create_WithInvalidString_ShouldThrowFormatException()
    {
        Assert.Throws<FormatException>(() => StubLongEntityId.Create("invalid-long"));
    }

    [Fact]
    public void Create_WithNullOrWhiteSpace_ShouldThrowException()
    {
        Assert.Throws<ArgumentException>(() => StubLongEntityId.Create(null));
        Assert.Throws<ArgumentException>(() => StubLongEntityId.Create(string.Empty));
        Assert.Throws<FormatException>(() => StubLongEntityId.Create("   "));
    }

    [Fact]
    public void ImplicitCast_ToLong_ShouldReturnValue()
    {
        const long longValue = 9223372036854775807; // Max long value
        long castedLong = StubLongEntityId.Create(longValue);

        Assert.Equal(longValue, castedLong);
    }

    [Fact]
    public void ImplicitCast_ToString_ShouldReturnStringRepresentation()
    {
        const long longValue = 9223372036854775807; // Max long value
        string castedString = StubLongEntityId.Create(longValue);

        Assert.Equal(longValue.ToString(), castedString);
    }

    [Fact]
    public void ImplicitCast_FromLong_ShouldCreateStubLongEntityId()
    {
        const long longValue = 9223372036854775807; // Max long value
        StubLongEntityId id = longValue;

        Assert.Equal(longValue, id.Value);
    }

    [Fact]
    public void IsEmpty_WithZero_ShouldReturnTrue()
    {
        var id = StubLongEntityId.Create(0);

        Assert.True(id.IsEmpty);
    }

    [Fact]
    public void IsEmpty_WithNonZero_ShouldReturnFalse()
    {
        var id = StubLongEntityId.Create(9223372036854775807); // Max long value

        Assert.False(id.IsEmpty);
    }

    [Fact]
    public void Equals_SameValue_ShouldReturnTrue()
    {
        const long longValue = 9223372036854775807; // Max long value
        var id1 = StubLongEntityId.Create(longValue);
        var id2 = StubLongEntityId.Create(longValue);

        Assert.True(id1.Equals(id2));
        Assert.True(id1 == id2);
        Assert.False(id1 != id2);
    }

    [Fact]
    public void Equals_DifferentValue_ShouldReturnFalse()
    {
        var id1 = StubLongEntityId.Create(9223372036854775807); // Max long value
        var id2 = StubLongEntityId.Create(9223372036854775806); // Max long value - 1

        Assert.False(id1.Equals(id2));
        Assert.False(id1 == id2);
        Assert.True(id1 != id2);
    }

    [Fact]
    public void Equals_Null_ShouldReturnFalse()
    {
        var id = StubLongEntityId.Create(9223372036854775807); // Max long value

        Assert.False(id.Equals(null));
        Assert.False(id == null);
        Assert.True(id != null);
    }

    [Fact]
    public void GetHashCode_SameValue_ShouldReturnSameHashCode()
    {
        const long longValue = 9223372036854775807; // Max long value
        var id1 = StubLongEntityId.Create(longValue);
        var id2 = StubLongEntityId.Create(longValue);

        Assert.Equal(id1.GetHashCode(), id2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValue_ShouldReturnDifferentHashCode()
    {
        var id1 = StubLongEntityId.Create(9223372036854775807); // Max long value
        var id2 = StubLongEntityId.Create(9223372036854775806); // Max long value - 1

        Assert.NotEqual(id1.GetHashCode(), id2.GetHashCode());
    }

    [Fact]
    public void ToString_ShouldReturnStringRepresentationOfValue()
    {
        const long longValue = 9223372036854775807; // Max long value
        var id = StubLongEntityId.Create(longValue);

        Assert.Equal(longValue.ToString(), id.ToString());
    }

    [Fact]
    public void Deserialize_WithValidId_ShouldCreateIdWithParsedValue()
    {
        var entity = new StubLongEntity
        {
            Id = StubLongEntityId.Create(9223372036854775807)
        };

        var serialized = new SystemTextJsonSerializer().SerializeToString(entity);
        var deserialized = new SystemTextJsonSerializer().Deserialize<StubLongEntity>(serialized);

        deserialized.Id.Value.ShouldBe(9223372036854775807);
        deserialized.Id.IsEmpty.ShouldBeFalse();
    }

    [TypedEntityId<long>]
    public partial class StubLongEntity : Entity<StubLongEntityId>
    {
    }
}