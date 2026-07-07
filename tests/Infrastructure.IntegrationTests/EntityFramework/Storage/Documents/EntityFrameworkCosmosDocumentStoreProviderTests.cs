// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using DotNet.Testcontainers.Containers;
using Application.Storage;
using Infrastructure.EntityFramework.Storage;

[IntegrationTest("Infrastructure")]
[Collection(nameof(EntityFrameworkCosmosDocumentStoreTestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class EntityFrameworkCosmosDocumentStoreProviderTests(ITestOutputHelper output, TestEnvironmentFixture fixture) : EntityFrameworkDocumentStoreProviderTestsBase
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);
    private readonly ITestOutputHelper output = output;

    [SkippableFact]
    public override async Task CountResultAsync_ReturnsDocumentCount()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.CountResultAsync_ReturnsDocumentCount();
    }

    [SkippableFact]
    public override async Task DeleteResultAsync_DeletesEntity()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.DeleteResultAsync_DeletesEntity();
    }

    [SkippableFact]
    public override async Task FindPageResultAsync_WithDocumentKeyAndFilterFullMatch_ReturnsFilteredEntities()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.FindPageResultAsync_WithDocumentKeyAndFilterFullMatch_ReturnsFilteredEntities();
    }

    [SkippableFact]
    public override async Task FindPageResultAsync_WithDocumentKeyAndFilterRowKeyPrefix_ReturnsFilteredEntities()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.FindPageResultAsync_WithDocumentKeyAndFilterRowKeyPrefix_ReturnsFilteredEntities();
    }

    [SkippableFact]
    public override async Task FindPageResultAsync_WithDocumentKeyAndFilterRowKeySuffix_ReturnsFilteredEntities()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.FindPageResultAsync_WithDocumentKeyAndFilterRowKeySuffix_ReturnsFilteredEntities();
    }

    [SkippableFact]
    public override async Task FindPageResultAsync_WithoutFilter_ReturnsEntities()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.FindPageResultAsync_WithoutFilter_ReturnsEntities();
    }

    [SkippableFact]
    public override async Task ExistsResultAsync_WithExactKey_ReturnsExpectedValue()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.ExistsResultAsync_WithExactKey_ReturnsExpectedValue();
    }

    [SkippableFact]
    public override async Task ListPageResultAsync_WithDocumentKeyAndFilter_ReturnsFilteredDocumentKeys()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.ListPageResultAsync_WithDocumentKeyAndFilter_ReturnsFilteredDocumentKeys();
    }

    [SkippableFact]
    public override async Task ListPageResultAsync_WithoutFilter_ReturnsDocumentKeys()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.ListPageResultAsync_WithoutFilter_ReturnsDocumentKeys();
    }

    [SkippableFact]
    public override async Task UpsertResultAsync_CreatesOrUpdatesSingleLogicalRow()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.UpsertResultAsync_CreatesOrUpdatesSingleLogicalRow();
    }

    [SkippableFact]
    public override async Task UpsertResultAsync_PopulatesLookupHashesAndClearsLease()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.UpsertResultAsync_PopulatesLookupHashesAndClearsLease();
    }

    [SkippableFact]
    public override async Task UpsertResultAsync_WithPartitionKeyLongerThan256_ReturnsFailure()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.UpsertResultAsync_WithPartitionKeyLongerThan256_ReturnsFailure();
    }

    [SkippableFact]
    public override async Task UpsertResultAsync_WithRowKeyLongerThan256_ReturnsFailure()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.UpsertResultAsync_WithRowKeyLongerThan256_ReturnsFailure();
    }

    protected override async Task ExecuteDbContextAsync(Func<StubDbContext, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using var dbContext = this.fixture.EnsureCosmosDbContext(this.output, forceNew: true);
        await action(dbContext);
    }

    protected override async Task<TResult> ExecuteDbContextAsync<TResult>(Func<StubDbContext, Task<TResult>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using var dbContext = this.fixture.EnsureCosmosDbContext(this.output, forceNew: true);
        return await action(dbContext);
    }

    protected override EntityFrameworkDocumentStoreProvider<StubDbContext> CreateProvider(
        EntityFrameworkDocumentStoreProviderOptions options = null,
        bool forceNew = false,
        DocumentStoreOptions documentStoreOptions = null)
    {
        return new EntityFrameworkDocumentStoreProvider<StubDbContext>(
            this.fixture.EnsureCosmosDbContext(this.output, forceNew: forceNew),
            options: options,
            documentStoreOptions: documentStoreOptions);
    }
}
