// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage
{
    using BridgingIT.DevKit.Domain.Model;

    // lives in a deny-listed namespace on purpose
    public class StoredThing : Entity<Guid>
    {
        public string Label { get; set; }
    }
}

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Entities
{
    using BridgingIT.DevKit.Domain;
    using BridgingIT.DevKit.Domain.Repositories;
    using BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;
    using Microsoft.Extensions.DependencyInjection;

    [UnitTest("Presentation")]
    public class EntitiesDashboardDiscoveryTests
    {
        [Fact]
        public void Discover_RepositoryRegistrations_AreListedOrderedByName()
        {
            // Arrange
            using var provider = EntitiesDashboardTestData.CreateProvider(services: s => s
                .AddRepository<Region>()
                .AddRepository<Customer>()
                .AddRepository<Order>());

            // Act
            var entities = EntitiesDashboardTestData.Catalog(provider).Entities;

            // Assert
            entities.Select(e => e.Name).ShouldBe(["Customer", "Order", "Region"]);
            entities.ShouldAllBe(e => e.Sources.Count == 1 && e.Sources[0] is RepositoryEntitiesDashboardSource);
        }

        [Fact]
        public void Discover_NoRegistrations_HasNoSources()
        {
            // Arrange
            using var provider = EntitiesDashboardTestData.CreateProvider();

            // Act
            var catalog = EntitiesDashboardTestData.Catalog(provider);

            // Assert
            catalog.HasSources.ShouldBeFalse();
            catalog.Entities.ShouldBeEmpty();
        }

        [Fact]
        public void Discover_ActiveEntityRegistryOnly_UsesTheActiveEntitySource()
        {
            // Arrange
            using var provider = EntitiesDashboardTestData.CreateProvider(services: s =>
                ActiveEntityRegistry.GetOrAdd(s).Register(new ActiveEntityDescriptor(typeof(Ticket), typeof(Guid), false)));

            // Act
            var entity = EntitiesDashboardTestData.Catalog(provider).Entities.Single();

            // Assert
            entity.Name.ShouldBe("Ticket");
            entity.Sources.ShouldHaveSingleItem().ShouldBeOfType<ActiveEntityEntitiesDashboardSource>();
            entity.IdType.ShouldBe(typeof(Guid));
        }

        [Fact]
        public void Discover_EntityInBothSources_ListsItOnceWithRepositoryAsDefault()
        {
            // Arrange
            using var provider = EntitiesDashboardTestData.CreateProvider(services: s =>
            {
                s.AddRepository<Ticket>();
                ActiveEntityRegistry.GetOrAdd(s).Register(new ActiveEntityDescriptor(typeof(Ticket), typeof(Guid), false));
            });

            // Act
            var entity = EntitiesDashboardTestData.Catalog(provider).Entities.Single();

            // Assert
            entity.Sources.Count.ShouldBe(2);
            entity.Sources[0].ShouldBeOfType<RepositoryEntitiesDashboardSource>();
            entity.Sources[1].ShouldBeOfType<ActiveEntityEntitiesDashboardSource>();
            entity.DefaultSource.ShouldBeOfType<RepositoryEntitiesDashboardSource>();
        }

        [Fact]
        public void Discover_ExcludedViaOptions_IsSkipped()
        {
            // Arrange
            using var provider = EntitiesDashboardTestData.CreateProvider(
                o => o.Exclude<Region>(),
                s => s.AddRepository<Region>().AddRepository<Customer>());

            // Act
            var entities = EntitiesDashboardTestData.Catalog(provider).Entities;

            // Assert
            entities.Select(e => e.Name).ShouldBe(["Customer"]);
        }

        [Fact]
        public void Discover_ExcludedViaEntityConfiguration_IsSkipped()
        {
            // Arrange
            using var provider = EntitiesDashboardTestData.CreateProvider(
                o => o.Entity<Region>(e => e.Exclude()),
                s => s.AddRepository<Region>().AddRepository<Customer>());

            // Act
            var entities = EntitiesDashboardTestData.Catalog(provider).Entities;

            // Assert
            entities.Select(e => e.Name).ShouldBe(["Customer"]);
        }

        [Fact]
        public void Discover_DenyListedType_IsSkippedUnlessExplicitlyIncluded()
        {
            // Arrange
            using var denied = EntitiesDashboardTestData.CreateProvider(services: s => s.AddRepository<BridgingIT.DevKit.Application.Storage.StoredThing>());
            using var included = EntitiesDashboardTestData.CreateProvider(
                o => o.Include<BridgingIT.DevKit.Application.Storage.StoredThing>(),
                s => s.AddRepository<BridgingIT.DevKit.Application.Storage.StoredThing>());

            // Act & Assert
            EntitiesDashboardTestData.Catalog(denied).HasSources.ShouldBeFalse();
            EntitiesDashboardTestData.Catalog(included).Entities.Single().Name.ShouldBe("StoredThing");
        }

        [Theory]
        [InlineData(typeof(BridgingIT.DevKit.Application.Storage.StoredThing), true)]
        [InlineData(typeof(Customer), false)]
        public void DenyList_IsDenied_MatchesNamespacePrefixes(Type type, bool expected)
        {
            // Arrange & Act & Assert
            EntitiesDashboardDenyList.IsDenied(type).ShouldBe(expected);
        }

        [Fact]
        public void Discover_ActiveEntityWithoutIdType_IsSkipped()
        {
            // Arrange
            using var provider = EntitiesDashboardTestData.CreateProvider(services: s =>
                ActiveEntityRegistry.GetOrAdd(s).Register(new ActiveEntityDescriptor(typeof(Ticket), null, false)));

            // Act
            var catalog = EntitiesDashboardTestData.Catalog(provider);

            // Assert
            catalog.HasSources.ShouldBeFalse();
        }

        [Fact]
        public void Find_UnknownOrNullKey_ReturnsNull()
        {
            // Arrange
            using var provider = EntitiesDashboardTestData.CreateProvider(services: s => s.AddRepository<Region>());
            var sut = EntitiesDashboardTestData.Catalog(provider);

            // Act & Assert
            sut.Find(typeof(Region).FullName).ShouldNotBeNull();
            sut.Find("Missing").ShouldBeNull();
            sut.Find(null).ShouldBeNull();
        }

        [Fact]
        public void Discover_ExplicitConfiguration_AppliesMaskingAndHidingToTheDescriptor()
        {
            // Arrange
            using var provider = EntitiesDashboardTestData.CreateProvider(
                o => o.Entity<Customer>(e => e.Mask(c => c.ApiToken).Hide(c => c.InternalNote)),
                s => s.AddRepository<Customer>());

            // Act
            var customer = EntitiesDashboardTestData.Catalog(provider).Entities.Single();

            // Assert
            customer.FindProperty("ApiToken").IsMasked.ShouldBeTrue();
            customer.FindProperty("InternalNote").IsHidden.ShouldBeTrue();
        }

        [Fact]
        public void ServiceCatalog_DecoratorRegistrations_AreNotTreatedAsEntitySources()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGenericRepository<Region>>(new SnapshotRepository<Region>());
            services.AddSingleton<IGenericReadOnlyRepository<Region>>(sp => sp.GetRequiredService<IGenericRepository<Region>>());
            services.AddEntitiesDashboard();
            using var provider = services.BuildServiceProvider();

            // Act
            var entities = EntitiesDashboardTestData.Catalog(provider).Entities;

            // Assert
            entities.Count.ShouldBe(1);
        }
    }

    [UnitTest("Presentation")]
    public class EntitiesDashboardReferenceResolverTests
    {
        private static IReadOnlyList<EntitiesDashboardEntity> Discover(Action<EntitiesDashboardOptions> configure = null)
        {
            using var provider = EntitiesDashboardTestData.CreateProvider(configure, s => s
                .AddRepository<Customer>()
                .AddRepository<Region>()
                .AddRepository<Order>());

            return EntitiesDashboardTestData.Catalog(provider).Entities;
        }

        [Fact]
        public void Resolve_TypedIdProperty_LinksToTheEntityOwningThatIdType()
        {
            // Arrange & Act
            var order = Discover().Single(e => e.Name == "Order");

            // Assert
            order.FindProperty("CustomerId").Reference.ShouldNotBeNull().TargetKey.ShouldBe(typeof(Customer).FullName);
        }

        [Fact]
        public void Resolve_NameConventionWithMatchingIdType_LinksToTheEntityByShortName()
        {
            // Arrange & Act
            var order = Discover().Single(e => e.Name == "Order");

            // Assert
            order.FindProperty("RegionId").Reference.ShouldNotBeNull().TargetKey.ShouldBe(typeof(Region).FullName);
        }

        [Fact]
        public void Resolve_PlainProperties_AreNotLinked()
        {
            // Arrange & Act
            var order = Discover().Single(e => e.Name == "Order");

            // Assert
            order.FindProperty("OrderNumber").Reference.ShouldBeNull();
            order.IdProperty.Reference.ShouldBeNull();
        }

        [Fact]
        public void Resolve_ReferenceNone_SuppressesDetection()
        {
            // Arrange & Act
            var order = Discover(o => o.Entity<Order>(e => e.Reference(x => x.RegionId).None())).Single(e => e.Name == "Order");

            // Assert
            order.FindProperty("RegionId").Reference.ShouldBeNull();
            order.FindProperty("CustomerId").Reference.ShouldNotBeNull();
        }

        [Fact]
        public void Resolve_ConfiguredReference_OverridesConvention()
        {
            // Arrange & Act
            var order = Discover(o => o.Entity<Order>(e => e.Reference(x => x.RegionId).To<Customer>())).Single(e => e.Name == "Order");

            // Assert
            order.FindProperty("RegionId").Reference.TargetKey.ShouldBe(typeof(Customer).FullName);
        }

        [Fact]
        public void Resolve_ConfiguredTargetNotDiscovered_DoesNotLink()
        {
            // Arrange & Act
            var order = Discover(o =>
            {
                o.Exclude<Region>();
                o.Entity<Order>(e => e.Reference(x => x.RegionId).To<Region>());
            }).Single(e => e.Name == "Order");

            // Assert
            order.FindProperty("RegionId").Reference.ShouldBeNull();
        }

        [Fact]
        public void Resolve_NameConventionWithMismatchingIdType_DoesNotLink()
        {
            // Arrange
            using var provider = EntitiesDashboardTestData.CreateProvider(services: s => s.AddRepository<Order>().AddRepository<Customer>());

            // Act
            var order = EntitiesDashboardTestData.Catalog(provider).Entities.Single(e => e.Name == "Order");

            // Assert
            order.FindProperty("RegionId").Reference.ShouldBeNull();
        }
    }
}
