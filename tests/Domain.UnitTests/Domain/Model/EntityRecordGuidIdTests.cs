﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model.AAA;

using DevKit.Domain.Model;

[UnitTest("Domain")]
public class EntityRecordGuidIdTests
{
    //[Fact]
    //public void Equals_SameReference_ShouldReturnTrue()
    //{
    //    var entity = new StubEntityRecord("John Doe") { Id = Guid.NewGuid() };
    //    Assert.True(entity.Equals(entity));
    //}

    //[Fact]
    //public void Equals_SameIdDifferentReference_ShouldReturnTrue()
    //{
    //    var id = Guid.NewGuid();
    //    var entity1 = new StubEntityRecord("John Doe") { Id = id };
    //    var entity2 = new StubEntityRecord("Mary Jane") { Id = id };
    //    Assert.True(entity1 == entity2);
    //    Assert.True(entity1.Equals(entity2));
    //}

    //[Fact]
    //public void Equals_DifferentId_ShouldReturnFalse()
    //{
    //    var entity1 = new StubEntityRecord { Id = Guid.NewGuid() };
    //    var entity2 = new StubEntityRecord { Id = Guid.NewGuid() };
    //    Assert.False(entity1.Equals(entity2));
    //}

    //[Fact]
    //public void Equals_DifferentType_ShouldReturnFalse()
    //{
    //    var entity = new StubEntityRecord { Id = Guid.NewGuid() };
    //    var otherObject = new object();
    //    Assert.False(entity.Equals(otherObject));
    //}

    //[Fact]
    //public void Equals_TransientEntities_ShouldReturnFalse()
    //{
    //    var entity1 = new StubEntityRecord();
    //    var entity2 = new StubEntityRecord();
    //    Assert.False(entity1.Equals(entity2));
    //}

    //[Fact]
    //public void Equals_OneTransientOneNot_ShouldReturnFalse()
    //{
    //    var entity1 = new StubEntityRecord();
    //    var entity2 = new StubEntityRecord { Id = Guid.NewGuid() };
    //    Assert.False(entity1.Equals(entity2));
    //}

    //[Fact]
    //public void EqualityOperator_SameEntities_ShouldReturnTrue()
    //{
    //    var entity1 = new StubEntityRecord { Id = Guid.NewGuid() };
    //    var entity2 = entity1;
    //    Assert.True(entity1 == entity2);
    //}

    //[Fact]
    //public void EqualityOperator_DifferentEntities_ShouldReturnFalse()
    //{
    //    var entity1 = new StubEntityRecord { Id = Guid.NewGuid() };
    //    var entity2 = new StubEntityRecord { Id = Guid.NewGuid() };
    //    Assert.False(entity1 == entity2);
    //}

    //[Fact]
    //public void InequalityOperator_SameEntities_ShouldReturnFalse()
    //{
    //    var entity1 = new StubEntityRecord { Id = Guid.NewGuid() };
    //    var entity2 = entity1;
    //    Assert.False(entity1 != entity2);
    //}

    //[Fact]
    //public void InequalityOperator_DifferentEntities_ShouldReturnTrue()
    //{
    //    var entity1 = new StubEntityRecord { Id = Guid.NewGuid() };
    //    var entity2 = new StubEntityRecord { Id = Guid.NewGuid() };
    //    Assert.True(entity1 != entity2);
    //}

    //[Fact]
    //public void GetHashCode_SameId_ShouldReturnSameHashCode()
    //{
    //    var id = Guid.NewGuid();
    //    var entity1 = new StubEntityRecord { Id = id };
    //    var entity2 = new StubEntityRecord { Id = id };
    //    Assert.Equal(entity1.GetHashCode(), entity2.GetHashCode());
    //}

    //[Fact]
    //public void GetHashCode_DifferentId_ShouldReturnDifferentHashCode()
    //{
    //    var entity1 = new StubEntityRecord { Id = Guid.NewGuid() };
    //    var entity2 = new StubEntityRecord { Id = Guid.NewGuid() };
    //    Assert.NotEqual(entity1.GetHashCode(), entity2.GetHashCode());
    //}

    //[Fact]
    //public void IsTransient_NullId_ShouldReturnFalse()
    //{
    //    var entity = new StubEntityRecord();
    //    Assert.False(entity.Equals(new StubEntityRecord())); // because id is default (entity is transient)
    //}

    //[Fact]
    //public void IsTransient_DefaultId_ShouldReturnFalse()
    //{
    //    var entity = new StubEntityRecord { Id = Guid.Empty };
    //    Assert.False(entity.Equals(new StubEntityRecord { Id = Guid.Empty })); // because id is default (entity is transient)
    //}

    //[Fact]
    //public void IsTransient_NonDefaultId_ShouldReturnFalse()
    //{
    //    var entity = new StubEntityRecord { Id = Guid.NewGuid() };
    //    Assert.False(entity.Equals(new StubEntityRecord { Id = Guid.NewGuid() }));
    //}

    //[Fact]
    //public void Equals_ProxyTypes_ShouldCompareBaseTypes()
    //{
    //    var entity1 = new StubEntityRecord { Id = Guid.NewGuid() };
    //    var entity2 = new StubEntityRecordProxy { Id = entity1.Id };
    //    Assert.False(entity1.Equals(entity2)); // because types are different
    //}

#pragma warning disable SA1313 // Parameter names should begin with lower-case letter
}

public record StubEntityRecord(string Name) : EntityRecord<Guid>
{
    public string Name { get; set; } = Name;

    public override int GetHashCode()
    {
        return HashCode.Combine(GetUnproxiedType(this), this.Id);
    }
}

public record StubEntityRecordProxy : StubEntityRecord
{
    protected StubEntityRecordProxy(StubEntityRecord original)
        : base(original) { }
}
#pragma warning restore SA1313 // Parameter names should begin with lower-case letter