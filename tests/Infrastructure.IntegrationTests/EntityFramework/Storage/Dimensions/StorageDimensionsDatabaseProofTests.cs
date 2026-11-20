namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Security.Cryptography;
using System.Text;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

public sealed partial class StorageDimensionsDatabaseProofTests
{
    [Fact]
    public async Task AnnotatedSchema_RealEngines_StoresSearchesAndDeletesIndependently()
    {
        await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").WithDatabase("dimensions_proof").Build();
        await sql.StartAsync();
        await postgres.StartAsync();
        foreach (var engine in new[] { "sqlserver", "postgres", "sqlite" })
        {
            var options = new DbContextOptionsBuilder<ProofContext>();
            if (engine == "sqlserver")
            {
                var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sql.GetConnectionString()) { InitialCatalog = "dimensions_proof" };
                options.UseSqlServer(connection.ConnectionString);
            }
            else if (engine == "postgres") options.UseNpgsql(postgres.GetConnectionString());
            else options.UseSqlite("Data Source=:memory:");

            await using var sut = new ProofContext(options.Options);
            if (engine == "sqlite") await sut.Database.OpenConnectionAsync();
            await sut.Database.EnsureCreatedAsync();
            var script = sut.Database.GenerateCreateScript();
            script.ShouldContain("__Storage_BlobDimensions");
            script.ShouldContain("__Storage_DocumentDimensionNames");
            var repository = new DirectoryInfo(AppContext.BaseDirectory);
            while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "bITdevKit.slnx"))) repository = repository.Parent;
            if (repository is null) throw new InvalidOperationException("Repository root was not found.");
            var directory = Path.Combine(repository.FullName, "artifacts", "storage-dimensions", engine);
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "create.sql"), script);
            var model = sut.GetService<IDesignTimeModel>().Model;
            var operations = sut.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel());
            var migrationCommands = sut.GetService<IMigrationsSqlGenerator>().Generate(operations, model);
            await File.WriteAllTextAsync(Path.Combine(directory, "initial-migration.sql"), string.Join(Environment.NewLine, migrationCommands.Select(command => command.CommandText)));
            foreach (var valueType in new[] { typeof(StorageBlobDimension), typeof(StorageDocumentDimension) })
                sut.Model.FindEntityType(valueType).GetIndexes().Count().ShouldBe(3);

            var blobName = new StorageBlobDimensionName { Scope = "archive", ScopeHash = SHA256.HashData("archive"u8), Name = "amount", NameHash = SHA256.HashData("AMOUNT"u8) };
            var docName = new StorageDocumentDimensionName { Scope = "invoice", ScopeHash = SHA256.HashData("invoice"u8), Name = "amount", NameHash = SHA256.HashData("AMOUNT"u8) };
            var blob = new StorageBlob { Container = "archive", ContainerHash = "container", Name = "invoice.pdf", NameHash = "name" };
            var doc = new StorageDocument { Type = "invoice", TypeHash = "type", PartitionKey = "p", PartitionKeyHash = "partition", RowKey = "r", RowKeyHash = "row" };
            var blobValue = new StorageBlobDimension { Owner = blob, Name = blobName, ValueType = BlobDimensionValueType.Decimal, DecimalValue = 1.000000m };
            var docValue = new StorageDocumentDimension { Owner = doc, Name = docName, ValueType = DocumentDimensionValueType.Decimal, DecimalValue = 1.000000m };
            sut.AddRange(blobValue, docValue);
            await sut.SaveChangesAsync();
            blobValue.NameId.ShouldBe(blobName.Id);
            docValue.NameId.ShouldBe(docName.Id);
            blobName.Id.ShouldBeGreaterThan(0);
            docName.Id.ShouldBeGreaterThan(0);
            var operand = 1.0m;
            (await sut.Set<StorageBlobDimension>().CountAsync(row => row.NameId == blobName.Id && row.ValueType == BlobDimensionValueType.Decimal && row.DecimalValue == operand)).ShouldBe(1);
            (await sut.Set<StorageDocumentDimension>().CountAsync(row => row.NameId == docName.Id && row.ValueType == DocumentDimensionValueType.Decimal && row.DecimalValue == operand)).ShouldBe(1);
            await sut.Set<StorageBlob>().Where(row => row.Id == blob.Id).ExecuteDeleteAsync();
            await sut.Set<StorageDocument>().Where(row => row.Id == doc.Id).ExecuteDeleteAsync();
            sut.ChangeTracker.Clear();
            (await sut.Set<StorageBlobDimension>().CountAsync()).ShouldBe(0);
            (await sut.Set<StorageDocumentDimension>().CountAsync()).ShouldBe(0);
            (await sut.Set<StorageBlobDimensionName>().CountAsync()).ShouldBe(1);
            (await sut.Set<StorageDocumentDimensionName>().CountAsync()).ShouldBe(1);
            await VerifyPopulatedIndexesAsync(sut, engine, directory, blobName.Id, docName.Id);
        }
    }

    private sealed class ProofContext(DbContextOptions<ProofContext> options) : DbContext(options)
    {
        public DbSet<StorageBlob> Blobs { get; set; }

        public DbSet<StorageBlobChunk> Chunks { get; set; }

        public DbSet<StorageDocument> Documents { get; set; }
    }
}
