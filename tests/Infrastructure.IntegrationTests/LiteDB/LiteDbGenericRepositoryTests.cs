// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

[IntegrationTest("Infrastructure")]
public class LiteDbGenericRepositoryTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("bdk-litedb-tests-").FullName;

    public LiteDbGenericRepositoryTests()
    {
        // The upstream development build requires explicit acknowledgement. These tests use disposable databases.
        global::LiteDB.LiteDBPragmas.I_AM_AWARE_MY_DATABASE_BREAKS_WHEN_I_USE_THIS();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("fictional-test-password")]
    public async Task InsertAsync_ReopenedDatabase_PreservesEntity(string password)
    {
        // Arrange
        var connectionString = $"Filename={Path.Combine(this.directory, "customers.db")}";
        if (password is not null)
        {
            connectionString += $";Password={password}";
        }

        var entity = new Customer { Id = Guid.NewGuid(), Name = "Alice", Age = 29 };
        using (var context = new LiteDbContext(connectionString))
        {
            var sut = CreateRepository(context);
            await sut.InsertAsync(entity);
        }

        // Act
        using var reopened = new LiteDbContext(connectionString);
        var result = await CreateRepository(reopened).FindOneAsync(entity.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(entity.Id);
        result.Name.ShouldBe(entity.Name);
        result.Age.ShouldBe(entity.Age);
    }

    [Fact]
    public async Task UpsertAsync_NewAndExistingEntity_PersistsOneUpdatedEntity()
    {
        // Arrange
        using var context = this.CreateContext();
        var sut = CreateRepository(context);
        var entity = new Customer { Id = Guid.NewGuid(), Name = "Alice", Age = 29 };

        // Act
        var inserted = await sut.UpsertAsync(entity);
        entity.Name = "Alice Updated";
        var updated = await sut.UpsertAsync(entity);
        var stored = await sut.FindOneAsync(entity.Id);

        // Assert
        inserted.action.ShouldBe(RepositoryActionResult.Inserted);
        updated.action.ShouldBe(RepositoryActionResult.Updated);
        stored.Name.ShouldBe("Alice Updated");
        (await sut.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task UpdateAsync_ExistingEntity_PersistsChanges()
    {
        // Arrange
        using var context = this.CreateContext();
        var sut = CreateRepository(context);
        var entity = await sut.InsertAsync(new Customer { Id = Guid.NewGuid(), Name = "Alice", Age = 29 });
        entity.Age = 30;

        // Act
        await sut.UpdateAsync(entity);
        var result = await sut.FindOneAsync(entity.Id);

        // Assert
        result.Age.ShouldBe(30);
        (await sut.CountAsync()).ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FindOneAsync_GuidOrStringId_ReturnsEntity(bool useStringId)
    {
        // Arrange
        using var context = this.CreateContext();
        var sut = CreateRepository(context);
        var entity = await sut.InsertAsync(new Customer { Id = Guid.NewGuid(), Name = "Alice" });
        object id = useStringId ? entity.Id.ToString() : entity.Id;

        // Act
        var result = await sut.FindOneAsync(id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(entity.Id);
        (await sut.ExistsAsync(id)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteAsync_GuidOrStringId_DeletesEntity(bool useStringId)
    {
        // Arrange
        using var context = this.CreateContext();
        var sut = CreateRepository(context);
        var entity = await sut.InsertAsync(new Customer { Id = Guid.NewGuid(), Name = "Alice" });
        object id = useStringId ? entity.Id.ToString() : entity.Id;

        // Act
        var result = await sut.DeleteAsync(id);

        // Assert
        result.ShouldBe(RepositoryActionResult.Deleted);
        (await sut.ExistsAsync(entity.Id)).ShouldBeFalse();
        (await sut.CountAsync()).ShouldBe(0);
    }

    [Theory]
    [InlineData(OrderDirection.Ascending)]
    [InlineData(OrderDirection.Descending)]
    public async Task FindAllAsync_SpecificationsWithOrderingAndPaging_ReturnsExpectedEntities(OrderDirection direction)
    {
        // Arrange
        using var context = this.CreateContext();
        var sut = CreateRepository(context);
        await sut.InsertSetAsync(
        [
            new Customer { Id = Guid.NewGuid(), Name = "Minor", Age = 17 },
            new Customer { Id = Guid.NewGuid(), Name = "Alice", Age = 21 },
            new Customer { Id = Guid.NewGuid(), Name = "Bob", Age = 30 },
            new Customer { Id = Guid.NewGuid(), Name = "Clara", Age = 40 },
            new Customer { Id = Guid.NewGuid(), Name = "Excluded", Age = 50 }
        ]);
        ISpecification<Customer>[] specifications =
        [
            new Specification<Customer>(customer => customer.Age >= 18),
            new Specification<Customer>(customer => customer.Name != "Excluded")
        ];
        var options = new FindOptions<Customer>(skip: 1, take: 2,
            order: new OrderOption<Customer>(customer => customer.Age, direction));

        // Act
        var results = await sut.FindAllAsync(specifications, options);
        var count = await sut.CountAsync(specifications);

        // Assert
        results.Select(customer => customer.Age).ShouldBe(
            direction == OrderDirection.Ascending ? new[] { 30, 40 } : new[] { 30, 21 });
        count.ShouldBe(3);
    }

    [Fact]
    public async Task ProjectAllAsync_SpecificationWithPaging_ReturnsProjectedValues()
    {
        // Arrange
        using var context = this.CreateContext();
        var sut = CreateRepository(context);
        await sut.InsertSetAsync(
        [
            new Customer { Id = Guid.NewGuid(), Name = "Alice", Age = 21 },
            new Customer { Id = Guid.NewGuid(), Name = "Bob", Age = 30 },
            new Customer { Id = Guid.NewGuid(), Name = "Clara", Age = 40 }
        ]);

        // Act
        var results = await sut.ProjectAllAsync(new Specification<Customer>(customer => customer.Age >= 30),
            customer => customer.Name,
            new FindOptions<Customer>(take: 1, orderExpression: customer => customer.Age));

        // Assert
        results.ShouldBe(["Bob"]);
    }

    [Fact]
    public async Task FindOneAsync_MissingEntity_ReturnsNull()
    {
        // Arrange
        using var context = this.CreateContext();
        var sut = CreateRepository(context);
        var id = Guid.NewGuid();

        // Act
        var result = await sut.FindOneAsync(id);
        var deleted = await sut.DeleteAsync(id);

        // Assert
        result.ShouldBeNull();
        deleted.ShouldBe(RepositoryActionResult.None);
        (await sut.ExistsAsync(id)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Constructor_NullOptions_ThrowsArgumentNullException(bool readOnly)
    {
        // Arrange
        Action action = readOnly
            ? () => new LiteDbReadOnlyGenericRepository<Customer>((ILiteDbRepositoryOptions)null)
            : () => new LiteDbGenericRepository<Customer>((ILiteDbRepositoryOptions)null);

        // Act / Assert
        action.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("options");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Constructor_MissingContext_ThrowsArgumentNullException(bool readOnly)
    {
        // Arrange
        var options = new LiteDbRepositoryOptions();
        Action action = readOnly
            ? () => new LiteDbReadOnlyGenericRepository<Customer>(options)
            : () => new LiteDbGenericRepository<Customer>(options);

        // Act / Assert
        action.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("DbContext");
    }

    [Fact]
    public async Task InsertAsync_DefaultGuid_AssignsAndPersistsIdentifier()
    {
        // Arrange
        using var context = this.CreateContext();
        var sut = CreateRepository(context);
        var entity = new Customer { Name = "Alice", Age = 29 };

        // Act
        var result = await sut.InsertAsync(entity);

        // Assert
        result.ShouldBeSameAs(entity);
        result.Id.ShouldNotBe(Guid.Empty);
        (await sut.FindOneAsync(result.Id)).Name.ShouldBe("Alice");
        (await sut.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task InsertAsync_NullEntity_ReturnsNullAndPreservesStoredEntities()
    {
        // Arrange
        using var context = this.CreateContext();
        SeedCustomers(context);
        var sut = CreateRepository(context);

        // Act
        var result = await sut.InsertAsync(null);

        // Assert
        result.ShouldBeNull();
        (await sut.CountAsync()).ShouldBe(3);
    }

    [Fact]
    public async Task UpdateAsync_NullEntity_ReturnsNullAndPreservesStoredEntities()
    {
        // Arrange
        using var context = this.CreateContext();
        SeedCustomers(context);
        var sut = CreateRepository(context);

        // Act
        var result = await sut.UpdateAsync(null);

        // Assert
        result.ShouldBeNull();
        (await sut.CountAsync()).ShouldBe(3);
    }

    [Fact]
    public async Task UpsertAsync_NullEntity_ReturnsNoActionAndPreservesStoredEntities()
    {
        // Arrange
        using var context = this.CreateContext();
        SeedCustomers(context);
        var sut = CreateRepository(context);

        // Act
        var result = await sut.UpsertAsync(null);

        // Assert
        result.entity.ShouldBeNull();
        result.action.ShouldBe(RepositoryActionResult.None);
        (await sut.CountAsync()).ShouldBe(3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InsertSetAsync_NullOrEmptySequence_ReturnsEmptyAndPreservesStoredEntities(bool useNull)
    {
        // Arrange
        using var context = this.CreateContext();
        SeedCustomers(context);
        var sut = CreateRepository(context);
        IEnumerable<Customer> entities = useNull ? null : [];

        // Act
        var result = await sut.InsertSetAsync(entities);

        // Assert
        result.ShouldBeEmpty();
        (await sut.CountAsync()).ShouldBe(3);
    }

    [Fact]
    public async Task InsertSetAsync_SequenceContainingNulls_PersistsOnlyNonNullEntities()
    {
        // Arrange
        using var context = this.CreateContext();
        var sut = CreateRepository(context);
        var alice = new Customer { Id = Guid.NewGuid(), Name = "Alice" };
        var bob = new Customer { Id = Guid.NewGuid(), Name = "Bob" };

        // Act
        var result = await sut.InsertSetAsync([null, alice, null, bob, null]);

        // Assert
        result.Select(customer => customer.Id).ShouldBe([alice.Id, bob.Id]);
        (await sut.FindAllAsync()).Select(customer => customer.Name).ShouldBe(["Alice", "Bob"], ignoreOrder: true);
    }

    [Fact]
    public async Task DeleteAsync_ByEntity_DeletesOnlyTargetAndPersistsDeletion()
    {
        // Arrange
        Customer[] customers;
        using (var context = this.CreateContext())
        {
            customers = SeedCustomers(context);
            var sut = CreateRepository(context);

            // Act
            var result = await sut.DeleteAsync(customers[1]);

            // Assert
            result.ShouldBe(RepositoryActionResult.Deleted);
        }

        using var reopened = this.CreateContext();
        var remaining = await CreateRepository(reopened).FindAllAsync();
        remaining.Select(customer => customer.Id).ShouldBe([customers[0].Id, customers[2].Id], ignoreOrder: true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteAsync_NullEntityOrIdentifier_PreservesStoredEntities(bool useNullEntity)
    {
        // Arrange
        using var context = this.CreateContext();
        var sut = new LiteDbGenericRepository<StringCustomer>(options => options.DbContext(context));
        await sut.InsertAsync(new StringCustomer { Id = "customer-1", Name = "Alice" });
        var entity = useNullEntity ? null : new StringCustomer { Name = "Missing ID" };

        // Act
        var result = await sut.DeleteAsync(entity);

        // Assert
        result.ShouldBe(RepositoryActionResult.None);
        (await sut.CountAsync()).ShouldBe(1);
        (await sut.FindOneAsync("customer-1")).Name.ShouldBe("Alice");
    }

    [Fact]
    public async Task DeleteAsync_MissingEntity_PreservesOtherEntities()
    {
        // Arrange
        using var context = this.CreateContext();
        SeedCustomers(context);
        var sut = CreateRepository(context);

        // Act
        var result = await sut.DeleteAsync(new Customer { Id = Guid.NewGuid(), Name = "Missing" });

        // Assert
        result.ShouldBe(RepositoryActionResult.None);
        (await sut.CountAsync()).ShouldBe(3);
    }

    [Fact]
    public async Task UpsertAsync_StringIdentifier_UpdatesExistingEntityWithoutDuplicates()
    {
        // Arrange
        using var context = this.CreateContext();
        var sut = new LiteDbGenericRepository<StringCustomer>(new LiteDbRepositoryOptions { DbContext = context });
        var entity = new StringCustomer { Id = "customer-1", Name = "Alice" };
        await sut.InsertAsync(entity);

        // Act
        var result = await sut.UpsertAsync(new StringCustomer { Id = entity.Id, Name = "Alice Updated" });

        // Assert
        result.action.ShouldBe(RepositoryActionResult.Updated);
        (await sut.FindOneAsync(entity.Id)).Name.ShouldBe("Alice Updated");
        (await sut.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task FindOneAsync_NullId_DoesNotMatchExistingEntities()
    {
        // Arrange
        using var context = this.CreateContext();
        SeedCustomers(context);
        var sut = CreateRepository(context);

        // Act
        var result = await sut.FindOneAsync((object)null);
        var exists = await sut.ExistsAsync(null);
        var deleted = await sut.DeleteAsync((object)null);

        // Assert
        result.ShouldBeNull();
        exists.ShouldBeFalse();
        deleted.ShouldBe(RepositoryActionResult.None);
        (await sut.CountAsync()).ShouldBe(3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task FindOneAsync_InvalidGuidString_ThrowsFormatException(string id)
    {
        // Arrange
        using var context = this.CreateContext();
        SeedCustomers(context);
        var sut = CreateRepository(context);

        // Act / Assert
        await Should.ThrowAsync<FormatException>(() => sut.FindOneAsync(id));
        (await sut.CountAsync()).ShouldBe(3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadOnlyRepository_ExistingEntities_ReadsStoredData(bool useBuilder)
    {
        // Arrange
        using var context = this.CreateContext();
        var customers = SeedCustomers(context);
        var sut = useBuilder
            ? new LiteDbReadOnlyGenericRepository<Customer>(options => options.DbContext(context))
            : new LiteDbReadOnlyGenericRepository<Customer>(new LiteDbRepositoryOptions { DbContext = context });

        // Act
        var result = await sut.FindOneAsync(customers[1].Id);
        var count = await sut.CountAsync();

        // Assert
        result.Name.ShouldBe("Bob");
        count.ShouldBe(3);
    }

    [Fact]
    public async Task ReadOperations_EmptyDatabase_ReturnNoResults()
    {
        // Arrange
        using var context = this.CreateContext();
        var sut = CreateRepository(context);

        // Act
        var entities = await sut.FindAllAsync();
        var names = await sut.ProjectAllAsync(customer => customer.Name);
        var entity = await sut.FindOneAsync(new Specification<Customer>(customer => customer.Age >= 18));
        var count = await sut.CountAsync();

        // Assert
        entities.ShouldBeEmpty();
        names.ShouldBeEmpty();
        entity.ShouldBeNull();
        count.ShouldBe(0);
    }

    [Fact]
    public async Task FindAllAsync_WithoutOptions_ReturnsAllEntities()
    {
        // Arrange
        using var context = this.CreateContext();
        var customers = SeedCustomers(context);
        var sut = CreateRepository(context);

        // Act
        var result = await sut.FindAllAsync();

        // Assert
        result.Select(customer => customer.Id).ShouldBe(customers.Select(customer => customer.Id), ignoreOrder: true);
    }

    [Theory]
    [InlineData(18, 2)]
    [InlineData(30, 1)]
    [InlineData(50, 0)]
    public async Task FindAllAsync_SingleSpecification_ReturnsOnlyMatches(int minimumAge, int expectedCount)
    {
        // Arrange
        using var context = this.CreateContext();
        SeedCustomers(context);
        var sut = CreateRepository(context);

        // Act
        var result = (await sut.FindAllAsync(new Specification<Customer>(customer => customer.Age >= minimumAge))).ToList();

        // Assert
        result.Count.ShouldBe(expectedCount);
        result.ShouldAllBe(customer => customer.Age >= minimumAge);
    }

    [Theory]
    [InlineData(0, 2, 2)]
    [InlineData(1, 2, 2)]
    [InlineData(2, 5, 1)]
    [InlineData(5, 2, 0)]
    public async Task FindAllAsync_PagingWithoutOrdering_RespectsBounds(int skip, int take, int expectedCount)
    {
        // Arrange
        using var context = this.CreateContext();
        var customers = SeedCustomers(context);
        var sut = CreateRepository(context);

        // Act
        var result = (await sut.FindAllAsync(new FindOptions<Customer>(skip: skip, take: take))).ToList();

        // Assert
        result.Count.ShouldBe(expectedCount);
        result.ShouldAllBe(customer => customers.Any(expected => expected.Id == customer.Id));
        result.Select(customer => customer.Id).Distinct().Count().ShouldBe(expectedCount);
    }

    [Fact]
    public async Task FindAllAsync_StringFieldOrdering_ReturnsSortedEntities()
    {
        // Arrange
        using var context = this.CreateContext();
        SeedCustomers(context);
        var sut = CreateRepository(context);

        // Act
        var result = await sut.FindAllAsync(new FindOptions<Customer>(order: new OrderOption<Customer>("Age")));

        // Assert
        result.Select(customer => customer.Age).ShouldBe([17, 21, 30]);
    }

    [Theory]
    [InlineData("Clara", true)]
    [InlineData("Alice", false)]
    public async Task FindOneAsync_MultipleSpecifications_RequiresEveryPredicate(string name, bool expectedMatch)
    {
        // Arrange
        using var context = this.CreateContext();
        var customers = SeedCustomers(context);
        var sut = CreateRepository(context);
        var specifications = new List<ISpecification<Customer>>
        {
            new Specification<Customer>(customer => customer.Age >= 18),
            new Specification<Customer>(customer => customer.Name == name)
        };

        // Act
        var result = await sut.FindOneAsync(specifications);

        // Assert
        if (expectedMatch)
        {
            result.ShouldNotBeNull();
            result.Id.ShouldBe(customers[2].Id);
        }
        else
        {
            result.ShouldBeNull();
        }
    }

    [Fact]
    public async Task ProjectAllAsync_WithoutOptions_ReturnsAllProjectedValues()
    {
        // Arrange
        using var context = this.CreateContext();
        SeedCustomers(context);
        var sut = CreateRepository(context);

        // Act
        var result = await sut.ProjectAllAsync(customer => customer.Name);

        // Assert
        result.ShouldBe(["Alice", "Bob", "Clara"], ignoreOrder: true);
    }

    [Fact]
    public async Task ProjectAllAsync_FilterWithoutOrdering_ReturnsOnlyProjectedMatches()
    {
        // Arrange
        using var context = this.CreateContext();
        SeedCustomers(context);
        var sut = CreateRepository(context);

        // Act
        var result = await sut.ProjectAllAsync(new Specification<Customer>(customer => customer.Age >= 18),
            customer => customer.Name);

        // Assert
        result.ShouldBe(["Bob", "Clara"], ignoreOrder: true);
    }

    [Theory]
    [InlineData(OrderDirection.Ascending, "Dave")]
    [InlineData(OrderDirection.Descending, "Clara")]
    public async Task ProjectAllAsync_MultipleSpecificationsWithOrderingAndPaging_ReturnsExpectedValue(
        OrderDirection direction, string expectedName)
    {
        // Arrange
        using var context = this.CreateContext();
        SeedCustomers(context);
        var sut = CreateRepository(context);
        await sut.InsertAsync(new Customer { Id = Guid.NewGuid(), Name = "Dave", Age = 40 });
        var specifications = new List<ISpecification<Customer>>
        {
            new Specification<Customer>(customer => customer.Age >= 18),
            new Specification<Customer>(customer => customer.Name != "Bob")
        };
        var options = new FindOptions<Customer>(skip: 1, take: 1,
            order: new OrderOption<Customer>(customer => customer.Age, direction));

        // Act
        var result = await sut.ProjectAllAsync(specifications, customer => customer.Name, options);

        // Assert
        result.ShouldBe([expectedName]);
    }

    [Theory]
    [InlineData(18, 2)]
    [InlineData(30, 1)]
    [InlineData(50, 0)]
    public async Task CountAsync_SingleSpecification_CountsOnlyMatches(int minimumAge, long expectedCount)
    {
        // Arrange
        using var context = this.CreateContext();
        SeedCustomers(context);
        var sut = CreateRepository(context);

        // Act
        var result = await sut.CountAsync(new Specification<Customer>(customer => customer.Age >= minimumAge));

        // Assert
        result.ShouldBe(expectedCount);
    }

    [Fact]
    public async Task CountAsync_EnumerableSpecifications_RequiresEveryPredicate()
    {
        // Arrange
        using var context = this.CreateContext();
        SeedCustomers(context);
        var sut = CreateRepository(context);
        var specifications = new List<ISpecification<Customer>>
        {
            new Specification<Customer>(customer => customer.Age >= 18),
            new Specification<Customer>(customer => customer.Name != "Bob")
        };

        // Act
        var result = await sut.CountAsync(specifications);

        // Assert
        result.ShouldBe(1);
    }

    public void Dispose()
    {
        Directory.Delete(this.directory, recursive: true);
    }

    private static LiteDbGenericRepository<Customer> CreateRepository(ILiteDbContext context)
    {
        return new LiteDbGenericRepository<Customer>(options => options.DbContext(context));
    }

    private LiteDbContext CreateContext()
    {
        return new LiteDbContext(Path.Combine(this.directory, "customers.db"));
    }

    private static Customer[] SeedCustomers(ILiteDbContext context)
    {
        Customer[] customers =
        [
            new Customer { Id = Guid.NewGuid(), Name = "Alice", Age = 17 },
            new Customer { Id = Guid.NewGuid(), Name = "Bob", Age = 21 },
            new Customer { Id = Guid.NewGuid(), Name = "Clara", Age = 30 }
        ];
        context.Database.GetCollection<Customer>().InsertBulk(customers);

        return customers;
    }

    public sealed class Customer : Entity<Guid>
    {
        public string Name { get; set; }

        public int Age { get; set; }
    }

    public sealed class StringCustomer : Entity<string>
    {
        public string Name { get; set; }
    }
}