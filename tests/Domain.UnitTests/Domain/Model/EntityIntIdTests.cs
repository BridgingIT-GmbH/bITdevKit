// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using DevKit.Domain.Model;

[UnitTest("Domain")]
public class EntityIntIdTests
{
    [Fact]
    public void Equals_SameReference_ShouldReturnTrue()
    {
        var entity = new StubEntity { Id = 1 };
        Assert.True(entity.Equals(entity));
    }

    [Fact]
    public void Equals_SameIdDifferentReference_ShouldReturnTrue()
    {
        const int id = 1;
        var entity1 = new StubEntity { Id = id };
        var entity2 = new StubEntity { Id = id };
        Assert.True(entity1.Equals(entity2));
    }

    [Fact]
    public void Equals_DifferentId_ShouldReturnFalse()
    {
        var entity1 = new StubEntity { Id = 1 };
        var entity2 = new StubEntity { Id = 2 };
        Assert.False(entity1.Equals(entity2));
    }

    [Fact]
    public void Equals_DifferentType_ShouldReturnFalse()
    {
        var entity = new StubEntity { Id = 1 };
        var otherObject = new object();
        Assert.False(entity.Equals(otherObject));
    }

    [Fact]
    public void Equals_TransientEntities_ShouldReturnFalse()
    {
        var entity1 = new StubEntity();
        var entity2 = new StubEntity();
        Assert.False(entity1.Equals(entity2));
    }

    [Fact]
    public void Equals_OneTransientOneNot_ShouldReturnFalse()
    {
        var entity1 = new StubEntity();
        var entity2 = new StubEntity { Id = 1 };
        Assert.False(entity1.Equals(entity2));
    }

    [Fact]
    public void EqualityOperator_SameEntities_ShouldReturnTrue()
    {
        var entity1 = new StubEntity { Id = 1 };
        var entity2 = entity1;
        Assert.True(entity1 == entity2);
    }

    [Fact]
    public void EqualityOperator_DifferentEntities_ShouldReturnFalse()
    {
        var entity1 = new StubEntity { Id = 1 };
        var entity2 = new StubEntity { Id = 2 };
        Assert.False(entity1 == entity2);
    }

    [Fact]
    public void InequalityOperator_SameEntities_ShouldReturnFalse()
    {
        var entity1 = new StubEntity { Id = 1 };
        var entity2 = entity1;
        Assert.False(entity1 != entity2);
    }

    [Fact]
    public void InequalityOperator_DifferentEntities_ShouldReturnTrue()
    {
        var entity1 = new StubEntity { Id = 1 };
        var entity2 = new StubEntity { Id = 2 };
        Assert.True(entity1 != entity2);
    }

    [Fact]
    public void GetHashCode_SameId_ShouldReturnSameHashCode()
    {
        const int id = 1;
        var entity1 = new StubEntity { Id = id };
        var entity2 = new StubEntity { Id = id };
        Assert.Equal(entity1.GetHashCode(), entity2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentId_ShouldReturnDifferentHashCode()
    {
        var entity1 = new StubEntity { Id = 1 };
        var entity2 = new StubEntity { Id = 2 };
        Assert.NotEqual(entity1.GetHashCode(), entity2.GetHashCode());
    }

    [Fact]
    public void IsTransient_NullId_ShouldReturnFalse()
    {
        var entity = new StubEntity();
        Assert.False(entity.Equals(new StubEntity())); // because id is default (entity is transient)
    }

    [Fact]
    public void IsTransient_DefaultId_ShouldReturnFalse()
    {
        var entity = new StubEntity { Id = 0 };
        Assert.False(entity.Equals(new StubEntity { Id = 0 })); // because id is default (entity is transient)
    }

    [Fact]
    public void IsTransient_NonDefaultId_ShouldReturnFalse()
    {
        var entity = new StubEntity { Id = 1 };
        Assert.False(entity.Equals(new StubEntity { Id = 2 }));
    }

    [Fact]
    public void Equals_ProxyTypes_ShouldCompareBaseTypes()
    {
        var entity1 = new StubEntity { Id = 1 };
        var entity2 = new StubEntityProxy { Id = entity1.Id };
        Assert.False(entity1.Equals(entity2)); // because types are different
    }

    private class StubEntity : Entity<int>;

    private class StubEntityProxy : StubEntity;
}