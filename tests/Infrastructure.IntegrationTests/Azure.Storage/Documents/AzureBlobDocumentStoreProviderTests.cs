// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using Application.Storage;
using global::Azure.Storage.Blobs;
using Infrastructure.Azure;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class AzureBlobDocumentStoreProviderTests
{
    private readonly TestEnvironmentFixture fixture;
    private readonly AzureBlobDocumentStoreProvider sut;

    public AzureBlobDocumentStoreProviderTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        this.fixture = fixture.WithOutput(output);
        this.sut = new AzureBlobDocumentStoreProvider(XunitLoggerFactory.Create(this.fixture.Output),
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
    public async Task FindPageResultAsync_WithDocumentKeyAndFilterRowKeySuffix_ReturnsClientSideFilteringFailureByDefault()
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
        var result = await this.sut.FindPageResultAsync<PersonStub>(DocumentQueries.Query().ForKey("partition", "row" + ticks).WithRowKeySuffix().Take(10).Build());

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.GetType().Name == "DocumentStoreQueryNotSupportedError" ||
            e.GetType().Name == "DocumentStoreClientSideFilteringRejectedError");
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
        var partitionKey = "paging-blob-" + ticks;
        await this.SeedPagingPeopleAsync(this.sut, partitionKey, "row-", 3);

        // Act
        var firstPage = await this.sut.ListPageResultAsync<AzureBlobPagingPersonStub>(
            DocumentQueries.Query()
                .ForKey(partitionKey, "row-")
                .WithRowKeyPrefix()
                .Take(2)
                .Build());
        var secondPage = await this.sut.ListPageResultAsync<AzureBlobPagingPersonStub>(
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
    public async Task FindPageResultAsync_WithContinuation_DownloadsOnlyReturnedPage()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var partitionKey = "paging-blob-find-" + ticks;
        await this.SeedPagingPeopleAsync(this.sut, partitionKey, "row-", 3);

        // Act
        var firstPage = await this.sut.FindPageResultAsync<AzureBlobPagingPersonStub>(
            DocumentQueries.Query()
                .ForKey(partitionKey, "row-")
                .WithRowKeyPrefix()
                .Take(2)
                .Build());
        var secondPage = await this.sut.FindPageResultAsync<AzureBlobPagingPersonStub>(
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
    public async Task CountAndListPageResultAsync_WithSerializerThatThrowsOnDeserialize_DoNotDeserializePayloads()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var partitionKey = "paging-blob-count-" + ticks;
        var provider = new AzureBlobDocumentStoreProvider(
            XunitLoggerFactory.Create(this.fixture.Output),
            this.fixture.AzuriteConnectionString,
            serializer: new ThrowingDeserializeSerializer());
        await this.SeedPagingPeopleAsync(provider, partitionKey, "row-", 3);

        // Act
        var listResult = await provider.ListPageResultAsync<AzureBlobPagingPersonStub>(
            DocumentQueries.Query()
                .ForKey(partitionKey, "row-")
                .WithRowKeyPrefix()
                .Take(2)
                .Build());
        var countResult = await provider.CountResultAsync<AzureBlobPagingPersonStub>(
            DocumentQueries.Count()
                .ForKey(partitionKey, "row-")
                .WithRowKeyPrefix()
                .Build());

        // Assert
        listResult.IsSuccess.ShouldBeTrue();
        listResult.Value.Items.Count.ShouldBe(2);
        countResult.IsSuccess.ShouldBeTrue();
        countResult.Value.ShouldBe(3);
    }

    [Fact]
    public async Task ListPageResultAsync_WithRowKeySuffix_ReturnsClientSideFilteringFailureByDefault()
    {
        // Act
        var result = await this.sut.ListPageResultAsync<AzureBlobPagingPersonStub>(
            DocumentQueries.Query()
                .ForKey("partition", "suffix")
                .WithRowKeySuffix()
                .Take(2)
                .Build());

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.GetType().Name == "DocumentStoreQueryNotSupportedError" ||
            e.GetType().Name == "DocumentStoreClientSideFilteringRejectedError");
    }

    [Fact]
    public async Task UpsertResultAsync_WithReservedSeparatorInKey_ReturnsInvalidQueryFailure()
    {
        // Act
        var result = await this.sut.UpsertResultAsync(
            new DocumentKey("partition__bad", "row-1"),
            new AzureBlobPagingPersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "Invalid",
                LastName = "Separator",
                Age = 33
            });

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.GetType().Name == "DocumentStoreInvalidQueryError");
    }

    [Fact]
    public async Task ListPageResultAsync_WithExistingBlobContainingMultipleSeparators_ParsesFirstSeparatorOnly()
    {
        // Arrange
        var partitionKey = "existing-blob-" + DateTime.UtcNow.Ticks;
        var rowKey = "row__with__separator";
        var containerClient = new BlobServiceClient(this.fixture.AzuriteConnectionString)
            .GetBlobContainerClient(nameof(AzureBlobPagingPersonStub).ToLowerInvariant());
        await containerClient.CreateIfNotExistsAsync();
        await containerClient.UploadBlobAsync(partitionKey + "__" + rowKey, BinaryData.FromString("{}"));
        var provider = new AzureBlobDocumentStoreProvider(
            XunitLoggerFactory.Create(this.fixture.Output),
            this.fixture.AzuriteConnectionString,
            options: new DocumentStoreOptions { AllowFullScans = true });

        // Act
        var result = await provider.ListPageResultAsync<AzureBlobPagingPersonStub>(
            DocumentQueries.Query()
                .AllowFullScan()
                .Take(100)
                .Build());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldContain(new DocumentKey(partitionKey, rowKey));
    }

    private async Task SeedPagingPeopleAsync(AzureBlobDocumentStoreProvider provider, string partitionKey, string rowPrefix, int count)
    {
        for (var i = 1; i <= count; i++)
        {
            await provider.UpsertResultAsync(
                new DocumentKey(partitionKey, rowPrefix + i),
                new AzureBlobPagingPersonStub
                {
                    Id = Guid.NewGuid(),
                    Nationality = "USA",
                    FirstName = "Person " + i,
                    LastName = "Paging",
                    Age = 18 + i
                });
        }
    }

    private sealed class AzureBlobPagingPersonStub
    {
        public Guid Id { get; set; }

        public string Nationality { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public int Age { get; set; }
    }

    private sealed class ThrowingDeserializeSerializer : ISerializer
    {
        private readonly SystemTextJsonSerializer inner = new();

        public void Serialize(object value, Stream output)
        {
            this.inner.Serialize(value, output);
        }

        public object Deserialize(Stream input, Type type)
        {
            throw new InvalidOperationException("Deserialize should not be called.");
        }

        public T Deserialize<T>(Stream input)
        {
            throw new InvalidOperationException("Deserialize should not be called.");
        }
    }
}
