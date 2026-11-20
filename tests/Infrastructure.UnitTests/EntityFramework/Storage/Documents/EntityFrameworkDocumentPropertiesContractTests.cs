namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Storage;

using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class EntityFrameworkDocumentPropertiesContractTests : Application.UnitTests.Storage.DocumentPropertiesContractTests, IDisposable
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly ServiceProvider services;

    public EntityFrameworkDocumentPropertiesContractTests()
    {
        this.connection.Open();
        var collection = new ServiceCollection();
        collection.AddDbContext<PropertyDbContext>(options => options.UseSqlite(this.connection));
        this.services = collection.BuildServiceProvider();
        using var scope = this.services.CreateScope();
        scope.ServiceProvider.GetRequiredService<PropertyDbContext>().Database.EnsureCreated();
    }

    protected override IDocumentStoreProvider CreateProvider() => new EntityFrameworkDocumentStoreProvider<PropertyDbContext>(this.services.GetRequiredService<IServiceScopeFactory>());

    public void Dispose()
    {
        this.services.Dispose();
        this.connection.Dispose();
    }

    private sealed class PropertyDbContext(DbContextOptions<PropertyDbContext> options) : DbContext(options), IDocumentStoreContext
    {
        public DbSet<StorageDocument> StorageDocuments { get; set; }
    }
}
