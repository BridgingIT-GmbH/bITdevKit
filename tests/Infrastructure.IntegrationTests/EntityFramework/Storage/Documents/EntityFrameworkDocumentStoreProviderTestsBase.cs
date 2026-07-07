// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

public abstract class EntityFrameworkDocumentStoreProviderTestsBase
{
    public virtual async Task CountResultAsync_ReturnsDocumentCount()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var sut = this.CreateProvider(documentStoreOptions: new DocumentStoreOptions { AllowFullScans = true });
        await this.SeedPeopleAsync(sut, ticks);

        // Act
        var result = await sut.CountResultAsync<PersonStub>(
            DocumentQueries.Count()
                .AllowFullScan()
                .Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.ShouldBe(5);
    }

    public virtual async Task DeleteResultAsync_DeletesEntity()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var sut = this.CreateProvider();
        var documentKey = new DocumentKey("partition", "row" + ticks);
        var entity = new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        };

        // Act
        var upsertResult = await sut.UpsertResultAsync(documentKey, entity);
        var deleteResult = await sut.DeleteResultAsync<PersonStub>(documentKey);

        // Assert
        var result = await sut.ExistsResultAsync<PersonStub>(documentKey);
        upsertResult.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, upsertResult.Errors.Select(e => e.Message)));
        deleteResult.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, deleteResult.Errors.Select(e => e.Message)));
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.ShouldBeFalse();
    }

    public virtual async Task FindPageResultAsync_WithDocumentKeyAndFilterFullMatch_ReturnsFilteredEntities()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var sut = this.CreateProvider();
        await this.SeedPeopleAsync(sut, ticks);

        // Act
        var result = await sut.FindPageResultAsync<PersonStub>(
            DocumentQueries.Query()
                .ForKey("partition", "row" + ticks)
                .WithFullMatch()
                .Take(10)
                .Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(1);
        result.Value.Items.First()
            .FirstName.ShouldBe("Mary" + ticks);
    }

    public virtual async Task FindPageResultAsync_WithDocumentKeyAndFilterRowKeyPrefix_ReturnsFilteredEntities()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var sut = this.CreateProvider();
        await this.SeedPeopleAsync(sut, ticks);

        // Act
        var result = await sut.FindPageResultAsync<PersonStub>(
            DocumentQueries.Query()
                .ForKey("partition", "row" + ticks)
                .WithRowKeyPrefix()
                .Take(10)
                .Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(5);
        result.Value.Items.First()
            .FirstName.ShouldBe("Mary" + ticks);
    }

    public virtual async Task FindPageResultAsync_WithDocumentKeyAndFilterRowKeySuffix_ReturnsFilteredEntities()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var sut = this.CreateProvider();
        await this.SeedPeopleAsync(sut, ticks);

        // Act
        var result = await sut.FindPageResultAsync<PersonStub>(
            DocumentQueries.Query()
                .ForKey("partition", "row" + ticks)
                .WithRowKeySuffix()
                .Take(10)
                .Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(1);
        result.Value.Items.First()
            .FirstName.ShouldBe("Mary" + ticks);
    }

    public virtual async Task FindPageResultAsync_WithoutFilter_ReturnsEntities()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var sut = this.CreateProvider(documentStoreOptions: new DocumentStoreOptions { AllowFullScans = true });
        await this.SeedPeopleAsync(sut, ticks);

        // Act
        var result = await sut.FindPageResultAsync<PersonStub>(
            DocumentQueries.Query()
                .AllowFullScan()
                .Take(10)
                .Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(5);
        result.Value.Items.Any(e => e.FirstName.Equals("Mary" + ticks))
            .ShouldBeTrue();
    }

    public virtual async Task ExistsResultAsync_WithExactKey_ReturnsExpectedValue()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var sut = this.CreateProvider();
        var documentKey = new DocumentKey("partition", "row" + ticks);
        var upsertResult = await sut.UpsertResultAsync(documentKey, this.CreatePerson("Mary" + ticks));

        // Act
        var existing = await sut.ExistsResultAsync<PersonStub>(documentKey);
        var missing = await sut.ExistsResultAsync<PersonStub>(new DocumentKey("partition", "missing" + ticks));

        // Assert
        upsertResult.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, upsertResult.Errors.Select(e => e.Message)));
        existing.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, existing.Errors.Select(e => e.Message)));
        missing.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, missing.Errors.Select(e => e.Message)));
        existing.Value.ShouldBeTrue();
        missing.Value.ShouldBeFalse();
    }

    public virtual async Task ListPageResultAsync_WithDocumentKeyAndFilter_ReturnsFilteredDocumentKeys()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var sut = this.CreateProvider();
        await this.SeedPeopleAsync(sut, ticks);

        // Act
        var result = await sut.ListPageResultAsync<PersonStub>(
            DocumentQueries.Query()
                .ForKey("partition", "row" + ticks)
                .WithFullMatch()
                .Take(10)
                .Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(1);
        result.Value.Items.All(d => d.PartitionKey.Equals("partition"))
            .ShouldBeTrue();
        result.Value.Items.All(d => d.RowKey.StartsWith("row" + ticks))
            .ShouldBeTrue();
    }

    public virtual async Task ListPageResultAsync_WithoutFilter_ReturnsDocumentKeys()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var sut = this.CreateProvider(documentStoreOptions: new DocumentStoreOptions { AllowFullScans = true });
        await this.SeedPeopleAsync(sut, ticks);

        // Act
        var result = await sut.ListPageResultAsync<PersonStub>(
            DocumentQueries.Query()
                .AllowFullScan()
                .Take(10)
                .Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(5);
        result.Value.Items.All(d => d.PartitionKey.Equals("partition"))
            .ShouldBeTrue();
        result.Value.Items.Any(d => d.RowKey.StartsWith("row" + ticks))
            .ShouldBeTrue();
    }

    public virtual async Task UpsertResultAsync_CreatesOrUpdatesSingleLogicalRow()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var documentKey = new DocumentKey("partition", "row" + ticks);
        var sut = this.CreateProvider();

        // Act
        var firstUpsert = await sut.UpsertResultAsync(documentKey, this.CreatePerson("John"));
        var secondUpsert = await sut.UpsertResultAsync(documentKey, this.CreatePerson("Jane"));

        // Assert
        var result = await sut.GetResultAsync<PersonStub>(documentKey);
        firstUpsert.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, firstUpsert.Errors.Select(e => e.Message)));
        secondUpsert.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, secondUpsert.Errors.Select(e => e.Message)));
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.FirstName.ShouldBe("Jane");
    }

    public virtual async Task UpsertResultAsync_PopulatesLookupHashesAndClearsLease()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var documentKey = new DocumentKey("partition", "row" + ticks);
        var sut = this.CreateProvider();

        // Act
        var upsertResult = await sut.UpsertResultAsync(documentKey, this.CreatePerson("Mary" + ticks));

        // Assert
        upsertResult.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, upsertResult.Errors.Select(e => e.Message)));
        var row = await this.ExecuteDbContextAsync(async dbContext =>
            await dbContext.StorageDocuments.SingleAsync(e => e.PartitionKey == documentKey.PartitionKey && e.RowKey == documentKey.RowKey));

        row.TypeHash.ShouldNotBeNullOrWhiteSpace();
        row.PartitionKeyHash.ShouldBe(HashHelper.ComputeSha256(documentKey.PartitionKey));
        row.RowKeyHash.ShouldBe(HashHelper.ComputeSha256(documentKey.RowKey));
        row.ContentHash.ShouldNotBeNullOrWhiteSpace();
        row.LockedBy.ShouldBeNull();
        row.LockedUntil.ShouldBeNull();
    }

    public virtual async Task UpsertResultAsync_WithConcurrentWriters_PreservesSingleLogicalDocument()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var documentKey = new DocumentKey("partition", "row" + ticks);

        // Act
        await Task.WhenAll(Enumerable.Range(0, 8).Select(async index =>
        {
            var sut = this.CreateProvider(this.CreateConcurrentWriterOptions(), forceNew: true);
            var upsertResult = await sut.UpsertResultAsync(documentKey, this.CreatePerson("Writer" + index));
            upsertResult.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, upsertResult.Errors.Select(e => e.Message)));
        }));

        // Assert
        var rows = await this.ExecuteDbContextAsync(async dbContext =>
            await dbContext.StorageDocuments
                .Where(e => e.PartitionKey == documentKey.PartitionKey && e.RowKey == documentKey.RowKey)
                .ToListAsync());

        rows.Count.ShouldBe(1);
        rows[0].LockedBy.ShouldBeNull();
        rows[0].LockedUntil.ShouldBeNull();
        rows[0].ContentHash.ShouldNotBeNullOrWhiteSpace();
    }

    public virtual async Task UpsertResultAsync_WithPartitionKeyLongerThan256_ReturnsFailure()
    {
        // Arrange
        await this.ResetStoreAsync();
        var sut = this.CreateProvider();
        var documentKey = new DocumentKey(new string('p', StorageDocument.MaximumKeyLength + 1), "row");

        // Act
        var result = await sut.UpsertResultAsync(documentKey, this.CreatePerson("TooLong"));

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    public virtual async Task UpsertResultAsync_WithRowKeyLongerThan256_ReturnsFailure()
    {
        // Arrange
        await this.ResetStoreAsync();
        var sut = this.CreateProvider();
        var documentKey = new DocumentKey("partition", new string('r', StorageDocument.MaximumKeyLength + 1));

        // Act
        var result = await sut.UpsertResultAsync(documentKey, this.CreatePerson("TooLong"));

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    protected virtual async Task ResetStoreAsync()
    {
        await this.ExecuteDbContextAsync(async dbContext =>
        {
            EnsureDocumentStoreTableCreated(dbContext);

            var rows = await dbContext.StorageDocuments.ToListAsync();
            if (rows.Count == 0)
            {
                return;
            }

            dbContext.StorageDocuments.RemoveRange(rows);
            await dbContext.SaveChangesAsync();
        });
    }

    protected abstract Task ExecuteDbContextAsync(Func<StubDbContext, Task> action);

    protected abstract Task<TResult> ExecuteDbContextAsync<TResult>(Func<StubDbContext, Task<TResult>> action);

    protected virtual EntityFrameworkDocumentStoreProviderOptions CreateConcurrentWriterOptions() => null;

    protected virtual EntityFrameworkDocumentStoreProvider<StubDbContext> CreateProvider(
        EntityFrameworkDocumentStoreProviderOptions options = null,
        bool forceNew = false,
        DocumentStoreOptions documentStoreOptions = null)
    {
        return null;
    }

    private static void EnsureDocumentStoreTableCreated(StubDbContext dbContext)
    {
        dbContext.Database.EnsureCreated();

        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        var tableName = dbContext.Model.FindEntityType(typeof(StorageDocument))?.GetTableName();
        if (TableExists(dbContext, tableName))
        {
            if (!HasColumn(dbContext, tableName, "ConcurrencyVersion") ||
                !HasColumn(dbContext, tableName, "TypeHash") ||
                !HasColumn(dbContext, tableName, "PartitionKeyHash") ||
                !HasColumn(dbContext, tableName, "RowKeyHash"))
            {
                dbContext.Database.EnsureDeleted();
                dbContext.Database.EnsureCreated();
            }

            return;
        }

        dbContext.GetService<IRelationalDatabaseCreator>().CreateTables();
    }

    private static bool TableExists(DbContext dbContext, string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return true;
        }

        var providerName = dbContext.Database.ProviderName;
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        try
        {
            if (shouldClose)
            {
                connection.Open();
            }

            using var command = connection.CreateCommand();
            command.CommandText = providerName switch
            {
                string name when name.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) =>
                    "SELECT 1 FROM sys.tables WHERE name = @name",
                string name when name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) =>
                    "SELECT 1 FROM information_schema.tables WHERE table_name = @name",
                string name when name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) =>
                    "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @name",
                _ => null
            };

            if (command.CommandText is null)
            {
                return true;
            }

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@name";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            return command.ExecuteScalar() is not null;
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }

    private static bool HasColumn(DbContext dbContext, string tableName, string columnName)
    {
        if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(columnName))
        {
            return false;
        }

        var providerName = dbContext.Database.ProviderName;
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        try
        {
            if (shouldClose)
            {
                connection.Open();
            }

            using var command = connection.CreateCommand();
            command.CommandText = providerName switch
            {
                string name when name.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) =>
                    "SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(@table) AND name = @column",
                string name when name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) =>
                    "SELECT 1 FROM information_schema.columns WHERE table_name = @table AND column_name = @column",
                string name when name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) =>
                    $"PRAGMA table_info(\"{tableName}\")",
                _ => null
            };

            if (command.CommandText is null)
            {
                return true;
            }

            if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (string.Equals(reader["name"]?.ToString(), columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            var tableParameter = command.CreateParameter();
            tableParameter.ParameterName = "@table";
            tableParameter.Value = providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase)
                ? $"dbo.{tableName}"
                : tableName;
            command.Parameters.Add(tableParameter);

            var columnParameter = command.CreateParameter();
            columnParameter.ParameterName = "@column";
            columnParameter.Value = columnName;
            command.Parameters.Add(columnParameter);

            return command.ExecuteScalar() is not null;
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }

    private PersonStub CreatePerson(string firstName) => new()
    {
        Id = Guid.NewGuid(),
        Nationality = "USA",
        FirstName = firstName,
        LastName = "Doe",
        Age = 18
    };

    private async Task SeedPeopleAsync(EntityFrameworkDocumentStoreProvider<StubDbContext> sut, long ticks)
    {
        (await sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks), this.CreatePerson("Mary" + ticks))).IsSuccess.ShouldBeTrue();
        (await sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "a"), this.CreatePerson("John"))).IsSuccess.ShouldBeTrue();
        (await sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "b"), this.CreatePerson("John"))).IsSuccess.ShouldBeTrue();
        (await sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "c"), this.CreatePerson("John"))).IsSuccess.ShouldBeTrue();
        (await sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "d"), this.CreatePerson("John"))).IsSuccess.ShouldBeTrue();
    }
}
