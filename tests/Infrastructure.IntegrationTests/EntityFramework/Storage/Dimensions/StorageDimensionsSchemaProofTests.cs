namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

/// <summary>Verifies the relational Dimension schema and the Cosmos document model.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~StorageDimensionsSchemaProofTests</code></example>
public class StorageDimensionsSchemaProofTests
{
    [Theory]
    [InlineData("sqlite")]
    [InlineData("sqlserver")]
    [InlineData("postgres")]
    public void AnnotationsOnlyModels_CombinedFeatures_DiscoverKeysAndIndexes(string engine)
    {
        var options = new DbContextOptionsBuilder<CombinedContext>();
        switch (engine)
        {
            case "sqlite": options.UseSqlite("Data Source=:memory:"); break;
            case "sqlserver": options.UseSqlServer("Server=localhost;Database=dimensions_model;Integrated Security=true;TrustServerCertificate=true"); break;
            case "postgres": options.UseNpgsql("Host=localhost;Database=dimensions_model;Username=model"); break;
        }

        using var sut = new CombinedContext(options.Options);
        var blob = sut.Model.FindEntityType(typeof(StorageBlobDimension));
        var document = sut.Model.FindEntityType(typeof(StorageDocumentDimension));
        blob.ShouldNotBeNull();
        document.ShouldNotBeNull();
        foreach (var entity in new[] { blob, document })
        {
            entity.FindPrimaryKey().Properties.Count.ShouldBe(2);
            entity.GetIndexes().Count().ShouldBe(3);
            entity.GetForeignKeys().Count().ShouldBe(2);
            entity.FindProperty("StringValue").GetMaxLength().ShouldBe(512);
            entity.FindProperty("StringValue").IsUnicode().ShouldBe(false);
            entity.FindProperty("DecimalValue").GetPrecision().ShouldBe(18);
            entity.FindProperty("DecimalValue").GetScale().ShouldBe(6);
        }

        sut.Database.GenerateCreateScript().ShouldContain("__Storage_BlobDimensionNames");
        sut.Database.GenerateCreateScript().ShouldContain("__Storage_DocumentDimensions");
        var owner = sut.Model.FindEntityType(typeof(StorageDocument));
        var visibility = owner.GetIndexes().Single(index => index.Properties.Select(property => property.Name).SequenceEqual(
            [nameof(StorageDocument.TypeHash), nameof(StorageDocument.Type), nameof(StorageDocument.Id), nameof(StorageDocument.ExpiresAtUnixMilliseconds)]));
        visibility.GetFilter().ShouldBeNull();
        sut.Database.GenerateCreateScript().ShouldContain(visibility.GetDatabaseName());
    }

    /// <summary>Checks that the Cosmos model keeps its conventional document key and excludes Dimension entities.</summary>
    /// <example><code>dotnet test --filter FullyQualifiedName~CosmosModel_WithExplicitExclusions_PreservesExistingStorageShape</code></example>
    [Fact]
    public void CosmosModel_WithExplicitExclusions_PreservesExistingStorageShape()
    {
        // Arrange
        const string endpoint = "https://localhost:8081";
        const string database = "dimensions_model";
        var accountKey = Convert.ToBase64String(new byte[64]);
        var options = new DbContextOptionsBuilder<ExcludedDocumentContext>()
            .UseCosmos(endpoint, accountKey, database).Options;
        var baselineOptions = new DbContextOptionsBuilder<ConventionalCosmosContext>()
            .UseCosmos(endpoint, accountKey, database).Options;
        using var context = new ExcludedDocumentContext(options);
        using var baseline = new ConventionalCosmosContext(baselineOptions);
        var expected = baseline.Model.FindEntityType(typeof(ConventionalCosmosDocument));
        var expectedKey = expected.FindProperty(nameof(ConventionalCosmosDocument.Id));

        // Act
        var sut = context.Model.FindEntityType(typeof(StorageDocument));
        var key = sut.FindProperty(nameof(StorageDocument.Id));

        // Assert
        sut.FindProperty(nameof(StorageDocument.DimensionsJson)).ShouldBeNull();
        context.Model.FindEntityType(typeof(StorageDocumentDimension)).ShouldBeNull();
        context.Model.FindEntityType(typeof(StorageDocumentDimensionName)).ShouldBeNull();
        key.ClrType.ShouldBe(expectedKey.ClrType);
        key.GetValueConverter().ShouldBeNull();
        CosmosPropertyExtensions.GetJsonPropertyName(key).ShouldBe(CosmosPropertyExtensions.GetJsonPropertyName(expectedKey));
        var converterType = key.GetTypeMapping().Converter?.GetType();
        converterType.ShouldBe(expectedKey.GetTypeMapping().Converter?.GetType());
        var jsonId = sut.GetProperties().Single(property => CosmosPropertyExtensions.GetJsonPropertyName(property) == "id");
        var expectedJsonId = expected.GetProperties().Single(property => CosmosPropertyExtensions.GetJsonPropertyName(property) == "id");
        jsonId.ClrType.ShouldBe(expectedJsonId.ClrType);
        jsonId.IsShadowProperty().ShouldBe(expectedJsonId.IsShadowProperty());
    }

    [Fact]
    public async Task SqliteDecimalEquality_AlternateScales_UsesSearchIndex()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<DocumentContext>().UseSqlite(connection).Options;
        await using var sut = new DocumentContext(options);
        await sut.Database.EnsureCreatedAsync();
        var name = new StorageDocumentDimensionName { Scope = "invoice", ScopeHash = new byte[32], Name = "amount", NameHash = new byte[32] };
        var owner = new StorageDocument { Type = "invoice", TypeHash = "type", PartitionKey = "p", PartitionKeyHash = "p", RowKey = "r", RowKeyHash = "r" };
        var value = new StorageDocumentDimension { Owner = owner, Name = name, ValueType = DocumentDimensionValueType.Decimal, DecimalValue = 1.000000m };
        sut.Add(value);
        await sut.SaveChangesAsync();
        name.Id.ShouldBeGreaterThan(0);
        value.NameId.ShouldBe(name.Id);
        var operand = 1.0m;
        var query = sut.Set<StorageDocumentDimension>().Where(row => row.NameId == name.Id && row.ValueType == DocumentDimensionValueType.Decimal && row.DecimalValue == operand);
        (await query.CountAsync()).ShouldBe(1);
        query.ToQueryString().ShouldNotContain("ef_compare");
        var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN SELECT DocumentId FROM __Storage_DocumentDimensions WHERE NameId = $id AND ValueType = $type AND DecimalValue = $value";
        command.Parameters.AddWithValue("$id", name.Id);
        command.Parameters.AddWithValue("$type", (byte)DocumentDimensionValueType.Decimal);
        command.Parameters.AddWithValue("$value", operand);
        await using var reader = await command.ExecuteReaderAsync();
        var descriptions = new List<string>();
        while (await reader.ReadAsync()) descriptions.Add(reader.GetString(3));
        descriptions.ShouldContain(text => text.Contains("SEARCH") && text.Contains("DecimalValue"));
    }

    private static void ConfigureExistingCosmosModel(ModelBuilder modelBuilder)
    {
        // Neutralize pre-existing relational-only facets equally in both discovery probes.
        var owner = modelBuilder.Entity<StorageDocument>();
        owner.Property(row => row.ConcurrencyVersion).IsConcurrencyToken(false);
        foreach (var index in owner.Metadata.GetIndexes().ToArray()) owner.Metadata.RemoveIndex(index.Properties);
    }

    private sealed class CombinedContext(DbContextOptions<CombinedContext> options) : DbContext(options)
    {
        public DbSet<StorageBlob> StorageBlobs { get; set; }

        public DbSet<StorageBlobChunk> StorageBlobChunks { get; set; }

        public DbSet<StorageDocument> StorageDocuments { get; set; }
    }

    private sealed class DocumentContext(DbContextOptions<DocumentContext> options) : DbContext(options)
    {
        public DbSet<StorageDocument> StorageDocuments { get; set; }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            if (this.Database.IsCosmos())
                configurationBuilder.Conventions.Remove(typeof(Microsoft.EntityFrameworkCore.Metadata.Conventions.IndexAttributeConvention));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Isolate Dimensions discovery from the existing relational-only Guid concurrency annotation.
            if (this.Database.IsCosmos()) ConfigureExistingCosmosModel(modelBuilder);
        }
    }
    private sealed class ExcludedDocumentContext(DbContextOptions<ExcludedDocumentContext> options) : DbContext(options)
    {
        public DbSet<StorageDocument> StorageDocuments { get; set; }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Conventions.Remove(typeof(Microsoft.EntityFrameworkCore.Metadata.Conventions.IndexAttributeConvention));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Apply the same baseline-only Cosmos concurrency adjustment as the control context.
            ConfigureExistingCosmosModel(modelBuilder);
            modelBuilder.ExcludeDocumentDimensions();
        }
    }

    private sealed class ConventionalCosmosContext(DbContextOptions<ConventionalCosmosContext> options) : DbContext(options)
    {
        /// <summary>Registers a reference entity that uses the Cosmos GUID convention.</summary>
        /// <example><code>var query = context.Documents.AsNoTracking();</code></example>
        public DbSet<ConventionalCosmosDocument> Documents => this.Set<ConventionalCosmosDocument>();
    }

    private sealed class ConventionalCosmosDocument
    {
        /// <summary>Gets or sets the conventional Cosmos primary key.</summary>
        /// <example><code>var id = new ConventionalCosmosDocument { Id = Guid.NewGuid() }.Id;</code></example>
        public Guid Id { get; set; }
    }
}
