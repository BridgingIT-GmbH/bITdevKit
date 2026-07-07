// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using Application.Storage;
using global::Azure.Data.Tables;
using Infrastructure.Azure;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class AzureTableDocumentStoreProviderTests
{
    private readonly TestEnvironmentFixture fixture;
    private readonly AzureTableDocumentStoreProvider sut;

    public AzureTableDocumentStoreProviderTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        this.fixture = fixture.WithOutput(output);
        this.sut = new AzureTableDocumentStoreProvider(XunitLoggerFactory.Create(this.fixture.Output),
            this.fixture.AzuriteConnectionString,
            options: new DocumentStoreOptions { AllowFullScans = true });
    }

    [Fact]
    public async Task FindPageResultAsync_WithoutFilter_ReturnsEntities()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "Mary" + ticks,
                LastName = "Jane",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "a"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "b"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "c"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "d"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });

        // Act
        var result = await this.sut.FindPageResultAsync<PersonStub>(DocumentQueries.Query().AllowFullScan().Take(100).Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBeGreaterThanOrEqualTo(5); // due to other tests
        result.Value.Items.Any(e => e.FirstName.Equals("Mary" + ticks))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task FindPageResultAsync_WithDocumentKeyAndFilterFullMatch_ReturnsFilteredEntities()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "Mary" + ticks,
                LastName = "Jane",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "a"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "b"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "c"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "d"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });

        // Act
        var result = await this.sut.FindPageResultAsync<PersonStub>(DocumentQueries.Query().ForKey("partition", "row" + ticks).WithFullMatch().Take(10).Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(1);
        result.Value.Items.First()
            .FirstName.ShouldBe("Mary" + ticks);
    }

    [Fact]
    public async Task FindPageResultAsync_WithDocumentKeyAndFilterRowKeyPrefix_ReturnsFilteredEntities()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "Mary" + ticks,
                LastName = "Jane",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "a"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "b"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "c"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "d"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });

        // Act
        var result = await this.sut.FindPageResultAsync<PersonStub>(DocumentQueries.Query().ForKey("partition", "row" + ticks).WithRowKeyPrefix().Take(10).Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(5);
        result.Value.Items.First()
            .FirstName.ShouldBe("Mary" + ticks);
    }

    [Fact]
    public async Task ListPageResultAsync_WithoutFilter_ReturnsDocumentKeys()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "Mary" + ticks,
                LastName = "Jane",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "a"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "b"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "c"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "d"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });

        // Act
        var result = await this.sut.ListPageResultAsync<PersonStub>(DocumentQueries.Query().AllowFullScan().Take(100).Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBeGreaterThanOrEqualTo(5); // due to other tests
        result.Value.Items.Where(d => d.RowKey.StartsWith("row" + ticks))
            .ShouldAllBe(d => d.PartitionKey == "partition");
        result.Value.Items.Count(d => d.RowKey.StartsWith("row" + ticks))
            .ShouldBe(5);
    }

    [Fact]
    public async Task ListPageResultAsync_WithDocumentKeyAndFilter_ReturnsFilteredDocumentKeys()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "Mary" + ticks,
                LastName = "Jane",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "a"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "b"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "c"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "d"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });

        // Act
        var result = await this.sut.ListPageResultAsync<PersonStub>(DocumentQueries.Query().ForKey("partition", "row" + ticks).WithFullMatch().Take(10).Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(1);
        result.Value.Items.All(d => d.PartitionKey.Equals("partition"))
            .ShouldBeTrue();
        result.Value.Items.All(d => d.RowKey.StartsWith("row" + ticks))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task UpsertResultAsync_CreatesOrUpdateEntity()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
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
        await this.sut.UpsertResultAsync(documentKey, entity);

        // Assert
        var result = await this.sut.FindPageResultAsync<PersonStub>(DocumentQueries.Query().ForKey(documentKey.PartitionKey, documentKey.RowKey).WithFullMatch().Take(10).Build());
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(1);
        result.Value.Items.First()
            .ShouldBe(entity);
    }

    [Fact]
    public async Task DeleteResultAsync_DeletesEntity()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
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
        await this.sut.UpsertResultAsync(documentKey, entity);
        await this.sut.DeleteResultAsync<PersonStub>(documentKey);

        // Assert
        var result = await this.sut.FindPageResultAsync<PersonStub>(DocumentQueries.Query().ForKey(documentKey.PartitionKey, documentKey.RowKey).WithFullMatch().Take(10).Build());
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(0);
    }

    [Fact]
    public async Task ListPageResultAsync_WithContinuation_ReturnsNextPageUsingSdkContinuationToken()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var partitionKey = "paging-table-" + ticks;
        await this.SeedPagingPeopleAsync(partitionKey, "row-", 3);

        // Act
        var firstPage = await this.sut.ListPageResultAsync<AzureTablePagingPersonStub>(
            DocumentQueries.Query()
                .ForKey(partitionKey, "row-")
                .WithRowKeyPrefix()
                .Take(2)
                .Build());
        var secondPage = await this.sut.ListPageResultAsync<AzureTablePagingPersonStub>(
            DocumentQueries.Query()
                .ForKey(partitionKey, "row-")
                .WithRowKeyPrefix()
                .Take(2)
                .ContinueWith(firstPage.Value.ContinuationToken)
                .Build());

        // Assert
        firstPage.IsSuccess.ShouldBeTrue();
        firstPage.Value.Items.Count.ShouldBe(2);
        firstPage.Value.ContinuationToken.ShouldNotBeNullOrWhiteSpace();
        secondPage.IsSuccess.ShouldBeTrue();
        secondPage.Value.Items.Count.ShouldBe(1);
        secondPage.Value.ContinuationToken.ShouldBeNull();
        firstPage.Value.Items.Concat(secondPage.Value.Items)
            .Select(e => e.RowKey)
            .OrderBy(e => e)
            .ShouldBe(["row-1", "row-2", "row-3"]);
    }

    [Fact]
    public async Task FindPageResultAsync_WithContinuation_ReturnsOnlyRequestedPage()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var partitionKey = "paging-table-find-" + ticks;
        await this.SeedPagingPeopleAsync(partitionKey, "row-", 3);

        // Act
        var firstPage = await this.sut.FindPageResultAsync<AzureTablePagingPersonStub>(
            DocumentQueries.Query()
                .ForKey(partitionKey, "row-")
                .WithRowKeyPrefix()
                .Take(2)
                .Build());
        var secondPage = await this.sut.FindPageResultAsync<AzureTablePagingPersonStub>(
            DocumentQueries.Query()
                .ForKey(partitionKey, "row-")
                .WithRowKeyPrefix()
                .Take(2)
                .ContinueWith(firstPage.Value.ContinuationToken)
                .Build());

        // Assert
        firstPage.IsSuccess.ShouldBeTrue();
        firstPage.Value.Items.Count.ShouldBe(2);
        firstPage.Value.ContinuationToken.ShouldNotBeNullOrWhiteSpace();
        secondPage.IsSuccess.ShouldBeTrue();
        secondPage.Value.Items.Count.ShouldBe(1);
        secondPage.Value.ContinuationToken.ShouldBeNull();
    }

    [Fact]
    public async Task ListPageResultAsync_WithRowKeySuffix_ReturnsUnsupportedQueryFailure()
    {
        // Act
        var result = await this.sut.ListPageResultAsync<AzureTablePagingPersonStub>(
            DocumentQueries.Query()
                .ForKey("partition", "suffix")
                .WithRowKeySuffix()
                .Take(2)
                .Build());

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.GetType().Name == "DocumentStoreQueryNotSupportedError");
    }

    [Fact]
    public async Task ListPageResultAsync_WithIllegalKeyCharacters_NormalizesKeysForStorageQueries()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var originalPartitionKey = "pa/rt'i#tion?" + ticks;
        var originalRowKey = "ro\\w?'#-1";
        var normalizedPartitionKey = "partition" + ticks;
        var normalizedRowKey = "row-1";
        var upsert = await this.sut.UpsertResultAsync(
            new DocumentKey(originalPartitionKey, originalRowKey),
            new AzureTablePagingPersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "Normalized",
                LastName = "Key",
                Age = 31
            });

        // Act
        var getResult = await this.sut.GetResultAsync<AzureTablePagingPersonStub>(
            new DocumentKey(originalPartitionKey, originalRowKey));
        var listResult = await this.sut.ListPageResultAsync<AzureTablePagingPersonStub>(
            DocumentQueries.Query()
                .ForKey(originalPartitionKey, "ro\\")
                .WithRowKeyPrefix()
                .Take(10)
                .Build());

        // Assert
        upsert.IsSuccess.ShouldBeTrue();
        getResult.IsSuccess.ShouldBeTrue();
        listResult.IsSuccess.ShouldBeTrue();
        listResult.Value.Items.Single().ShouldBe(new DocumentKey(normalizedPartitionKey, normalizedRowKey));
    }

    [Fact]
    public async Task AddAzureTableDocumentStoreClient_WithDocumentStoreOptions_PassesOptionsToProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new TableServiceClient(this.fixture.AzuriteConnectionString));
        services.AddAzureTableDocumentStoreClient<AzureTablePagingPersonStub>(
            documentStoreOptions: new DocumentStoreOptions { AllowFullScans = true });
        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<IDocumentStoreClient<AzureTablePagingPersonStub>>();
        await client.UpsertResultAsync(
            new DocumentKey("di-options-" + DateTime.UtcNow.Ticks, "row-1"),
            new AzureTablePagingPersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "Options",
                LastName = "Wired",
                Age = 41
            });

        // Act
        var result = await client.ListPageResultAsync(
            DocumentQueries.Query()
                .AllowFullScan()
                .Take(1)
                .Build());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(1);
    }

    private async Task SeedPagingPeopleAsync(string partitionKey, string rowPrefix, int count)
    {
        for (var i = 1; i <= count; i++)
        {
            await this.sut.UpsertResultAsync(
                new DocumentKey(partitionKey, rowPrefix + i),
                new AzureTablePagingPersonStub
                {
                    Id = Guid.NewGuid(),
                    Nationality = "USA",
                    FirstName = "Person " + i,
                    LastName = "Paging",
                    Age = 18 + i
                });
        }
    }

    private sealed class AzureTablePagingPersonStub
    {
        public Guid Id { get; set; }

        public string Nationality { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public int Age { get; set; }
    }
}
