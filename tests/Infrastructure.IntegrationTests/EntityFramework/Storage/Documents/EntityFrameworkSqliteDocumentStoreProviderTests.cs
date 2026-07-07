// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework.Storage;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class EntityFrameworkSqliteDocumentStoreProviderTests(ITestOutputHelper output, TestEnvironmentFixture fixture) : EntityFrameworkDocumentStoreProviderTestsBase
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);
    private readonly ITestOutputHelper output = output;

    [Fact]
    public override async Task CountResultAsync_ReturnsDocumentCount()
    {
        await base.CountResultAsync_ReturnsDocumentCount();
    }

    [Fact]
    public override async Task DeleteResultAsync_DeletesEntity()
    {
        await base.DeleteResultAsync_DeletesEntity();
    }

    [Fact]
    public override async Task FindPageResultAsync_WithDocumentKeyAndFilterFullMatch_ReturnsFilteredEntities()
    {
        await base.FindPageResultAsync_WithDocumentKeyAndFilterFullMatch_ReturnsFilteredEntities();
    }

    [Fact]
    public override async Task FindPageResultAsync_WithDocumentKeyAndFilterRowKeyPrefix_ReturnsFilteredEntities()
    {
        await base.FindPageResultAsync_WithDocumentKeyAndFilterRowKeyPrefix_ReturnsFilteredEntities();
    }

    [Fact]
    public override async Task FindPageResultAsync_WithDocumentKeyAndFilterRowKeySuffix_ReturnsFilteredEntities()
    {
        await base.FindPageResultAsync_WithDocumentKeyAndFilterRowKeySuffix_ReturnsFilteredEntities();
    }

    [Fact]
    public override async Task FindPageResultAsync_WithoutFilter_ReturnsEntities()
    {
        await base.FindPageResultAsync_WithoutFilter_ReturnsEntities();
    }

    [Fact]
    public override async Task ExistsResultAsync_WithExactKey_ReturnsExpectedValue()
    {
        await base.ExistsResultAsync_WithExactKey_ReturnsExpectedValue();
    }

    [Fact]
    public override async Task ListPageResultAsync_WithDocumentKeyAndFilter_ReturnsFilteredDocumentKeys()
    {
        await base.ListPageResultAsync_WithDocumentKeyAndFilter_ReturnsFilteredDocumentKeys();
    }

    [Fact]
    public override async Task ListPageResultAsync_WithoutFilter_ReturnsDocumentKeys()
    {
        await base.ListPageResultAsync_WithoutFilter_ReturnsDocumentKeys();
    }

    [Fact]
    public override async Task UpsertResultAsync_CreatesOrUpdatesSingleLogicalRow()
    {
        await base.UpsertResultAsync_CreatesOrUpdatesSingleLogicalRow();
    }

    [Fact]
    public override async Task UpsertResultAsync_PopulatesLookupHashesAndClearsLease()
    {
        await base.UpsertResultAsync_PopulatesLookupHashesAndClearsLease();
    }

    [Fact]
    public override async Task UpsertResultAsync_WithConcurrentWriters_PreservesSingleLogicalDocument()
    {
        await base.UpsertResultAsync_WithConcurrentWriters_PreservesSingleLogicalDocument();
    }

    [Fact]
    public override async Task UpsertResultAsync_WithPartitionKeyLongerThan256_ReturnsFailure()
    {
        await base.UpsertResultAsync_WithPartitionKeyLongerThan256_ReturnsFailure();
    }

    [Fact]
    public override async Task UpsertResultAsync_WithRowKeyLongerThan256_ReturnsFailure()
    {
        await base.UpsertResultAsync_WithRowKeyLongerThan256_ReturnsFailure();
    }

    protected override async Task ExecuteDbContextAsync(Func<StubDbContext, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using var dbContext = this.fixture.EnsureSqliteDbContext(this.output, true);
        await action(dbContext);
    }

    protected override async Task<TResult> ExecuteDbContextAsync<TResult>(Func<StubDbContext, Task<TResult>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using var dbContext = this.fixture.EnsureSqliteDbContext(this.output, true);
        return await action(dbContext);
    }

    protected override EntityFrameworkDocumentStoreProvider<StubDbContext> CreateProvider(
        EntityFrameworkDocumentStoreProviderOptions options = null,
        bool forceNew = false,
        DocumentStoreOptions documentStoreOptions = null)
    {
        return new EntityFrameworkDocumentStoreProvider<StubDbContext>(
            this.fixture.EnsureSqliteDbContext(this.output, forceNew),
            options: options,
            documentStoreOptions: documentStoreOptions);
    }
}
