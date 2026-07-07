// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using Application.Storage;

[IntegrationTest("Application")]
public class InMemoryDocumentStoreProviderTests(ITestOutputHelper output) : TestsBase(output)
{
    private readonly InMemoryDocumentStoreProvider sut = new(XunitLoggerFactory.Create(output));

    [Fact]
    public async Task FindPageResultAsync_WithContinuation_ReturnsAllMatchingDocumentsInOrder()
    {
        // Arrange
        var partitionKey = "people-" + Guid.NewGuid().ToString("N");
        await this.UpsertPeopleAsync(partitionKey, ["001", "002", "003"]);
        var firstQuery = DocumentQueries.Query()
            .ForKey(partitionKey, "00")
            .WithRowKeyPrefix()
            .Take(2)
            .Build();

        // Act
        var firstPage = await this.sut.FindPageResultAsync<PersonStub>(firstQuery);
        var secondPage = await this.sut.FindPageResultAsync<PersonStub>(
            DocumentQueries.Query()
                .ForKey(partitionKey, "00")
                .WithRowKeyPrefix()
                .Take(2)
                .ContinueWith(firstPage.Value.ContinuationToken)
                .Build());

        // Assert
        firstPage.IsSuccess.ShouldBeTrue();
        firstPage.Value.Items.Select(item => item.FirstName).ShouldBe(["First001", "First002"]);
        firstPage.Value.HasMore.ShouldBeTrue();
        secondPage.IsSuccess.ShouldBeTrue();
        secondPage.Value.Items.Select(item => item.FirstName).ShouldBe(["First003"]);
        secondPage.Value.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task ListPageResultAsync_WithContinuation_ReturnsKeysWithoutPayloadDependence()
    {
        // Arrange
        var partitionKey = "keys-" + Guid.NewGuid().ToString("N");
        await this.UpsertPeopleAsync(partitionKey, ["101", "102", "103"]);
        var firstPage = await this.sut.ListPageResultAsync<PersonStub>(
            DocumentQueries.Query()
                .ForKey(partitionKey, "10")
                .WithRowKeyPrefix()
                .Take(2)
                .Build());

        // Act
        var secondPage = await this.sut.ListPageResultAsync<PersonStub>(
            DocumentQueries.Query()
                .ForKey(partitionKey, "10")
                .WithRowKeyPrefix()
                .Take(2)
                .ContinueWith(firstPage.Value.ContinuationToken)
                .Build());

        // Assert
        firstPage.IsSuccess.ShouldBeTrue();
        firstPage.Value.Items.Select(key => key.RowKey).ShouldBe(["101", "102"]);
        firstPage.Value.HasMore.ShouldBeTrue();
        secondPage.IsSuccess.ShouldBeTrue();
        secondPage.Value.Items.Select(key => key.RowKey).ShouldBe(["103"]);
        secondPage.Value.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task CountExistsGetAndDeleteResultAsync_RoundTripThroughProvider()
    {
        // Arrange
        var partitionKey = "roundtrip-" + Guid.NewGuid().ToString("N");
        var documentKey = new DocumentKey(partitionKey, "201");
        await this.sut.UpsertResultAsync(
            documentKey,
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "Round",
                LastName = "Trip",
                Age = 32
            });

        // Act
        var count = await this.sut.CountResultAsync<PersonStub>(
            DocumentQueries.Count()
                .ForKey(partitionKey, "20")
                .WithRowKeyPrefix()
                .Build());
        var existsBeforeDelete = await this.sut.ExistsResultAsync<PersonStub>(documentKey);
        var get = await this.sut.GetResultAsync<PersonStub>(documentKey);
        var delete = await this.sut.DeleteResultAsync<PersonStub>(documentKey);
        var existsAfterDelete = await this.sut.ExistsResultAsync<PersonStub>(documentKey);

        // Assert
        count.IsSuccess.ShouldBeTrue();
        count.Value.ShouldBe(1);
        existsBeforeDelete.Value.ShouldBeTrue();
        get.IsSuccess.ShouldBeTrue();
        get.Value.FirstName.ShouldBe("Round");
        delete.IsSuccess.ShouldBeTrue();
        existsAfterDelete.Value.ShouldBeFalse();
    }

    private async Task UpsertPeopleAsync(string partitionKey, IEnumerable<string> rowKeys)
    {
        foreach (var rowKey in rowKeys)
        {
            await this.sut.UpsertResultAsync(
                new DocumentKey(partitionKey, rowKey),
                new PersonStub
                {
                    Id = Guid.NewGuid(),
                    Country = "USA",
                    FirstName = "First" + rowKey,
                    LastName = "Last" + rowKey,
                    Age = 20
                });
        }
    }
}
