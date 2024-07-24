// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using System;
using BridgingIT.DevKit.Domain.Model;

[UnitTest("Domain")]
public class TypedEntityGuidIdTests
{
    [Fact]
    public void Create_WithoutParameters_ShouldCreateUniqueId()
    {
        var id1 = StubGuidEntityId.Create();
        var id2 = StubGuidEntityId.Create();

        Assert.NotEqual(id1, id2);
        Assert.NotEqual(Guid.Empty, id1.Value);
        Assert.NotEqual(Guid.Empty, id2.Value);
    }

    [Fact]
    public void Create_WithGuid_ShouldCreateIdWithSpecifiedValue()
    {
        var guid = Guid.NewGuid();
        var id = StubGuidEntityId.Create(guid);

        Assert.Equal(guid, id.Value);
    }

    [Fact]
    public void Create_WithValidString_ShouldCreateIdWithParsedValue()
    {
        const string guidString = "12345678-1234-1234-1234-123456789012";
        var id = StubGuidEntityId.Create(guidString);

        Assert.Equal(Guid.Parse(guidString), id.Value);
    }

    [Fact]
    public void Create_WithInvalidString_ShouldThrowArgumentException()
    {
        Assert.Throws<FormatException>(() => StubGuidEntityId.Create("invalid-guid"));
    }

    [Fact]
    public void Create_WithNullOrWhiteSpace_ShouldThrowException()
    {
        Assert.Throws<ArgumentException>(() => StubGuidEntityId.Create(null));
        Assert.Throws<ArgumentException>(() => StubGuidEntityId.Create(string.Empty));
        Assert.Throws<FormatException>(() => StubGuidEntityId.Create("   "));
    }

    [Fact]
    public void ImplicitCast_ToGuid_ShouldReturnValue()
    {
        var guid = Guid.NewGuid();
        Guid castedGuid = StubGuidEntityId.Create(guid);

        Assert.Equal(guid, castedGuid);
    }

    [Fact]
    public void ImplicitCast_ToString_ShouldReturnStringRepresentation()
    {
        var guid = Guid.NewGuid();
        string castedString = StubGuidEntityId.Create(guid);

        Assert.Equal(guid.ToString(), castedString);
    }

    [Fact]
    public void ImplicitCast_FromGuid_ShouldCreateStubGuidEntityId()
    {
        var guid = Guid.NewGuid();
        StubGuidEntityId id = guid;

        Assert.Equal(guid, id.Value);
    }

    [Fact]
    public void IsEmpty_WithEmptyGuid_ShouldReturnTrue()
    {
        var id = StubGuidEntityId.Create(Guid.Empty);

        Assert.True(id.IsEmpty);
    }

    [Fact]
    public void IsEmpty_WithNonEmptyGuid_ShouldReturnFalse()
    {
        var id = StubGuidEntityId.Create();

        Assert.False(id.IsEmpty);
    }

    [Fact]
    public void Equals_SameValue_ShouldReturnTrue()
    {
        var guid = Guid.NewGuid();
        var id1 = StubGuidEntityId.Create(guid);
        var id2 = StubGuidEntityId.Create(guid);

        Assert.True(id1.Equals(id2));
        Assert.True(id1 == id2);
        Assert.False(id1 != id2);
    }

    [Fact]
    public void Equals_DifferentValue_ShouldReturnFalse()
    {
        var id1 = StubGuidEntityId.Create();
        var id2 = StubGuidEntityId.Create();

        Assert.False(id1.Equals(id2));
        Assert.False(id1 == id2);
        Assert.True(id1 != id2);
    }

    [Fact]
    public void Equals_Null_ShouldReturnFalse()
    {
        var id = StubGuidEntityId.Create();

        Assert.False(id.Equals(null));
        Assert.False(id == null);
        Assert.True(id != null);
    }

    [Fact]
    public void GetHashCode_SameValue_ShouldReturnSameHashCode()
    {
        var guid = Guid.NewGuid();
        var id1 = StubGuidEntityId.Create(guid);
        var id2 = StubGuidEntityId.Create(guid);

        Assert.Equal(id1.GetHashCode(), id2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValue_ShouldReturnDifferentHashCode()
    {
        var id1 = StubGuidEntityId.Create();
        var id2 = StubGuidEntityId.Create();

        Assert.NotEqual(id1.GetHashCode(), id2.GetHashCode());
    }

    [Fact]
    public void ToString_ShouldReturnStringRepresentationOfValue()
    {
        var guid = Guid.NewGuid();
        var id = StubGuidEntityId.Create(guid);

        Assert.Equal(guid.ToString(), id.ToString());
    }

    [TypedEntityId<Guid>]
    public partial class StubGuidEntity : Entity<StubGuidEntityId>
    {
    }
}
