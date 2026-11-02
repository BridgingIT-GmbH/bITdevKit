// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Entities;

using System.Reflection;
using System.Text.Json;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

public sealed class CustomerId : EntityId<Guid>
{
    private CustomerId()
    {
    }

    private CustomerId(Guid value)
    {
        this.Value = value;
    }

    public override Guid Value { get; protected set; }

    public static CustomerId Create(Guid value) => new(value);

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Value;
    }
}

public enum CustomerStatus
{
    Active,
    Suspended
}

public sealed class CustomerTier : EnumerationValueObject<CustomerTier, int>
{
    public static readonly CustomerTier Standard = new(1, "Standard");

    public static readonly CustomerTier Premium = new(2, "Premium");

    private CustomerTier(int key, string name)
        : base(key, name)
    {
    }
}

public class Address : ValueObject
{
    public string Street { get; set; }

    public string City { get; set; }

    public string TaxNumber { get; set; }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Street;
        yield return this.City;
        yield return this.TaxNumber;
    }
}

public class Customer : AuditableEntity<CustomerId>, IConcurrency
{
    public string Name { get; set; }

    public string Email { get; set; }

    public int Age { get; set; }

    public bool IsVip { get; set; }

    public decimal CreditLimit { get; set; }

    public DateTime? BirthDate { get; set; }

    public CustomerStatus Status { get; set; }

    public CustomerTier Tier { get; set; }

    public Address Address { get; set; }

    public string ApiToken { get; set; }

    public string InternalNote { get; set; }

    public Guid ConcurrencyVersion { get; set; }
}

public class Region : Entity<int>
{
    public string Name { get; set; }

    public string Code { get; set; }
}

public class Order : Entity<Guid>
{
    public string OrderNumber { get; set; }

    public decimal Total { get; set; }

    public CustomerId CustomerId { get; set; }

    public int RegionId { get; set; }
}

public class LockedProduct : Entity<Guid>
{
    public LockedProduct(string sku, string name)
    {
        this.Sku = sku;
        this.Name = name;
    }

    public string Sku { get; private set; }

    public string Name { get; private set; }
}

public class ArchivedNote : Entity<Guid>, ISoftDeletable
{
    private bool? deleted;

    public string Title { get; set; }

    public string Body { get; set; }

    public bool? Deleted => this.deleted;

    public void SetDeleted(bool value = true)
    {
        this.deleted = value;
    }
}

public class Ticket : Entity<Guid>
{
    public string Subject { get; set; }
}

/// <summary>An in-memory repository that hands out shallow copies, so unsaved edits never leak into the store.</summary>
public class SnapshotRepository<TEntity> : InMemoryRepository<TEntity>
    where TEntity : class, IEntity
{
    private static readonly MethodInfo CloneMethod =
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

    public SnapshotRepository(params TEntity[] seed)
        : base(o => o.Context(new InMemoryContext<TEntity>(seed)).PublishEvents(false))
    {
    }

    public override async Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var found = await base.FindOneAsync(id, options, cancellationToken);

        return found == null ? null : (TEntity)CloneMethod.Invoke(found, null);
    }
}

internal static class EntitiesDashboardTestData
{
    public static readonly Guid CustomerGuid1 = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static readonly Guid CustomerGuid2 = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public static Customer NewCustomer(Guid id, string name, int age = 30, string email = null) =>
        new()
        {
            Id = CustomerId.Create(id),
            Name = name,
            Email = email ?? $"{name.ToLowerInvariant()}@example.test",
            Age = age,
            CreditLimit = 100.5m,
            Status = CustomerStatus.Active,
            Tier = CustomerTier.Standard,
            ApiToken = "secret-token",
            InternalNote = "internal",
            Address = new Address { Street = "Main Street 1", City = "Springfield", TaxNumber = "TAX-1" },
            ConcurrencyVersion = Guid.NewGuid()
        };

    public static EntitiesDashboardEntity Describe<TEntity>(
        Action<EntitiesDashboardOptions> configure = null,
        Type idType = null,
        params IEntitiesDashboardSource[] sources)
    {
        var options = new EntitiesDashboardOptions();
        configure?.Invoke(options);
        options.EntityConfigurations.TryGetValue(typeof(TEntity), out var configuration);
        idType ??= typeof(TEntity).GetProperty("Id").PropertyType;
        if (sources.Length == 0)
        {
            sources = [new RepositoryEntitiesDashboardSource(typeof(TEntity), idType)];
        }

        return EntityPropertyDescriptorFactory.Create(typeof(TEntity), idType, configuration, sources);
    }

    public static EntitiesDashboardEntity DescribeCustomer(Action<EntitiesDashboardOptions> configure = null) =>
        Describe<Customer>(configure);

    public static ServiceProvider CreateProvider(
        Action<EntitiesDashboardOptions> configure = null,
        Action<IServiceCollection> services = null)
    {
        var collection = new ServiceCollection();
        collection.AddLogging();
        services?.Invoke(collection);
        collection.AddEntitiesDashboard(configure);

        return collection.BuildServiceProvider();
    }

    public static IServiceCollection AddRepository<TEntity>(this IServiceCollection services, params TEntity[] seed)
        where TEntity : class, IEntity
    {
        services.AddSingleton<IGenericRepository<TEntity>>(new SnapshotRepository<TEntity>(seed));

        return services;
    }

    public static DefaultHttpContext CreateHttpContext(IServiceProvider provider) =>
        new() { RequestServices = provider };

    public static JsonElement Json(object value) =>
        JsonSerializer.SerializeToElement(value);

    public static EntitiesDashboardCatalog Catalog(IServiceProvider provider) =>
        provider.GetRequiredService<EntitiesDashboardCatalog>();

    public static EntitiesDashboardQuery Query(
        EntitiesDashboardEntity entity,
        IReadOnlyList<string> filters = null,
        string match = null,
        string quickSearch = null,
        string sort = null,
        string direction = null,
        int batch = 1,
        string source = null,
        IReadOnlyList<string> columns = null) =>
        new(entity.Key, source, filters ?? [], match, quickSearch, sort, direction, batch, null, columns ?? []);
}
