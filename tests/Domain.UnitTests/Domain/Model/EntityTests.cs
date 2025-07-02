// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model.AAA;

using DevKit.Domain.Model;

public class EntityTests
{
    [Fact]
    public void Equals_SameGuidId_ReturnsTrue()
    {
        var id = Guid.NewGuid();
        var entity1 = new GuidEntity { Id = id };
        var entity2 = new GuidEntity { Id = id };

        (entity1 == entity2).ShouldBeTrue();
        entity1.Equals(entity2)
            .ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentGuidId_ReturnsFalse()
    {
        var entity1 = new GuidEntity { Id = Guid.NewGuid() };
        var entity2 = new GuidEntity { Id = Guid.NewGuid() };

        (entity1 == entity2).ShouldBeFalse();
        entity1.Equals(entity2)
            .ShouldBeFalse();
    }

    [Fact]
    public void Equals_SameIntId_ReturnsTrue()
    {
        var entity1 = new IntEntity { Id = 1 };
        var entity2 = new IntEntity { Id = 1 };

        (entity1 == entity2).ShouldBeTrue();
        entity1.Equals(entity2)
            .ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentIntId_ReturnsFalse()
    {
        var entity1 = new IntEntity { Id = 1 };
        var entity2 = new IntEntity { Id = 2 };

        (entity1 == entity2).ShouldBeFalse();
        entity1.Equals(entity2)
            .ShouldBeFalse();
    }

    [Fact]
    public void Equals_DifferentTypes_ReturnsFalse()
    {
        var guidEntity = new GuidEntity { Id = Guid.NewGuid() };
        var intEntity = new IntEntity { Id = 1 };

        guidEntity.Equals(intEntity)
            .ShouldBeFalse();
    }

    [Fact]
    public void Equals_TransientEntities_ReturnsFalse()
    {
        var entity1 = new GuidEntity();
        var entity2 = new GuidEntity();

        (entity1 == entity2).ShouldBeFalse();
        entity1.Equals(entity2)
            .ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_SameGuidId_ReturnsSameHashCode()
    {
        var id = Guid.NewGuid();
        var entity1 = new GuidEntity { Id = id };
        var entity2 = new GuidEntity { Id = id };

        entity1.GetHashCode()
            .ShouldBe(entity2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_SameIntId_ReturnsSameHashCode()
    {
        var entity1 = new IntEntity { Id = 1 };
        var entity2 = new IntEntity { Id = 1 };

        entity1.GetHashCode()
            .ShouldBe(entity2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentIds_ReturnsDifferentHashCodes()
    {
        var entity1 = new IntEntity { Id = 1 };
        var entity2 = new IntEntity { Id = 2 };

        entity1.GetHashCode()
            .ShouldNotBe(entity2.GetHashCode());
    }

    [Fact]
    public void IEntity_IdProperty_WorksCorrectly()
    {
        IEntity guidEntity = new GuidEntity { Id = Guid.NewGuid() };
        IEntity intEntity = new IntEntity { Id = 1 };

        guidEntity.Id.ShouldBeOfType<Guid>();
        intEntity.Id.ShouldBeOfType<int>();

        var newGuidId = Guid.NewGuid();
        guidEntity.Id = newGuidId;
        guidEntity.Id.ShouldBe(newGuidId);

        const int newIntId = 2;
        intEntity.Id = newIntId;
        intEntity.Id.ShouldBe(newIntId);
    }

    private class GuidEntity : Entity<Guid>;

    private class IntEntity : Entity<int>;
}