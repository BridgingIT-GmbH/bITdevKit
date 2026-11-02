// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.ActiveEntity;

using BridgingIT.DevKit.Domain.Model;
using Microsoft.Extensions.DependencyInjection;

public class ActiveEntityRegistryTests
{
    [Fact]
    public void GetOrAdd_CalledTwice_ReturnsSameInstanceAndRegistersOnce()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var first = ActiveEntityRegistry.GetOrAdd(services);
        var second = ActiveEntityRegistry.GetOrAdd(services);

        // Assert
        second.ShouldBeSameAs(first);
        services.Count(d => d.ServiceType == typeof(ActiveEntityRegistry)).ShouldBe(1);
    }

    [Fact]
    public void Register_SameEntityTwice_KeepsLastDescriptor()
    {
        // Arrange
        var sut = new ActiveEntityRegistry();

        // Act
        sut.Register(new ActiveEntityDescriptor(typeof(StubEntity), typeof(Guid), false));
        sut.Register(new ActiveEntityDescriptor(typeof(StubEntity), typeof(Guid), true));

        // Assert
        sut.Descriptors.Count.ShouldBe(1);
        sut.Descriptors.Single().IsSoftDeletable.ShouldBeTrue();
        sut.Contains(typeof(StubEntity)).ShouldBeTrue();
        sut.Contains(typeof(OtherStubEntity)).ShouldBeFalse();
    }

    [Fact]
    public void Descriptors_ReturnsSnapshot()
    {
        // Arrange
        var sut = new ActiveEntityRegistry();
        sut.Register(new ActiveEntityDescriptor(typeof(StubEntity), typeof(Guid), false));
        var snapshot = sut.Descriptors;

        // Act
        sut.Register(new ActiveEntityDescriptor(typeof(OtherStubEntity), typeof(int), false));

        // Assert
        snapshot.Count.ShouldBe(1);
        sut.Descriptors.Count.ShouldBe(2);
    }

    [Fact]
    public void ConfiguratorConstructor_RecordsEntityInRegistry()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        _ = new ActiveEntityConfigurator<StubEntity, Guid>(services);
        _ = new ActiveEntityConfigurator<SoftDeletableStubEntity, int>(services);

        // Assert
        var registry = ActiveEntityRegistry.GetOrAdd(services);
        registry.Descriptors.Count.ShouldBe(2);
        registry.Descriptors.Single(d => d.EntityType == typeof(StubEntity)).IdType.ShouldBe(typeof(Guid));
        registry.Descriptors.Single(d => d.EntityType == typeof(StubEntity)).IsSoftDeletable.ShouldBeFalse();
        registry.Descriptors.Single(d => d.EntityType == typeof(SoftDeletableStubEntity)).IsSoftDeletable.ShouldBeTrue();
    }

    [Fact]
    public void AddActiveEntityBuilder_RecordsEntityInRegistry()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddActiveEntity<StubEntity, Guid>();

        // Assert
        ActiveEntityRegistry.GetOrAdd(services).Contains(typeof(StubEntity)).ShouldBeTrue();
    }

    private class StubEntity : Entity<Guid>;

    private class OtherStubEntity : Entity<int>;

    private class SoftDeletableStubEntity : Entity<int>, ISoftDeletable
    {
        public bool? Deleted { get; private set; }

        public void SetDeleted(bool value = true) => this.Deleted = value;
    }
}
