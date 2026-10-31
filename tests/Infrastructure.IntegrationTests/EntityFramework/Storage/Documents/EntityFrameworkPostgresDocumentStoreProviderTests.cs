// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework.Storage;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class EntityFrameworkPostgresDocumentStoreProviderTests(ITestOutputHelper output, TestEnvironmentFixture fixture) : EntityFrameworkDocumentStoreProviderTestsBase
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);

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

    [Fact]
    public override async Task GetManyAsync_WithKeysAcrossPartitionsAndChunks_ReturnsResultsInInputOrder()
    {
        await base.GetManyAsync_WithKeysAcrossPartitionsAndChunks_ReturnsResultsInInputOrder();
    }

    [Fact]
    public override async Task GetManyAsync_WithExpiredDocument_ReturnsNotFound()
    {
        await base.GetManyAsync_WithExpiredDocument_ReturnsNotFound();
    }

    [Fact]
    public override async Task UpsertResultAsync_GeneratesDistinctRowIdsInInsertOrder()
    {
        await base.UpsertResultAsync_GeneratesDistinctRowIdsInInsertOrder();
    }

    [Fact]
    public override async Task UpsertManyAsync_WithInsertsOverwritesAndDuplicates_AppliesInOrder()
    {
        await base.UpsertManyAsync_WithInsertsOverwritesAndDuplicates_AppliesInOrder();
    }

    [Fact]
    public override async Task UpsertManyAsync_WithCreateOnlyConflict_CommitsPrefixAndReportsFailedKey()
    {
        await base.UpsertManyAsync_WithCreateOnlyConflict_CommitsPrefixAndReportsFailedKey();
    }

    [Fact]
    public override async Task UpsertManyAsync_WithStaleETag_CommitsPrefixAndReportsFailedKey()
    {
        await base.UpsertManyAsync_WithStaleETag_CommitsPrefixAndReportsFailedKey();
    }

    [Fact]
    public override async Task UpsertManyAsync_WithRowKeyLongerThan256_CommitsPrefixAndReportsFailedKey()
    {
        await base.UpsertManyAsync_WithRowKeyLongerThan256_CommitsPrefixAndReportsFailedKey();
    }

    [Fact]
    public override async Task DeleteManyAsync_WithMissingKeyAndETagMismatch_DeletesPrefixAndReportsFailedKey()
    {
        await base.DeleteManyAsync_WithMissingKeyAndETagMismatch_DeletesPrefixAndReportsFailedKey();
    }

    [Fact]
    public override async Task ListPageResultAsync_WalkingFullTypeScan_ReturnsEveryKeyOnceInOrder()
    {
        await base.ListPageResultAsync_WalkingFullTypeScan_ReturnsEveryKeyOnceInOrder();
    }

    [Fact]
    public override async Task SweepExpiredAsync_RemovesOnlyExpiredDocuments()
    {
        await base.SweepExpiredAsync_RemovesOnlyExpiredDocuments();
    }

    // Postgres uuid sorts byte-wise.
    protected override void AssertRowIdsInInsertOrder(IReadOnlyList<Guid> ids)
    {
        ids.Select(x => Convert.ToHexString(x.ToByteArray(bigEndian: true))).ShouldBeInOrder(SortDirection.Ascending);
    }

    protected override async Task ExecuteDbContextAsync(Func<StubDbContext, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using var dbContext = this.fixture.EnsurePostgresDbContext(output, true);
        await action(dbContext);
    }

    protected override async Task<TResult> ExecuteDbContextAsync<TResult>(Func<StubDbContext, Task<TResult>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using var dbContext = this.fixture.EnsurePostgresDbContext(output, true);
        return await action(dbContext);
    }

    protected override EntityFrameworkDocumentStoreProviderOptions CreateConcurrentWriterOptions() =>
        new()
        {
            LeaseDuration = TimeSpan.FromSeconds(15),
            RetryCount = 10,
            RetryDelay = TimeSpan.FromMilliseconds(25)
        };

    protected override EntityFrameworkDocumentStoreProvider<StubDbContext> CreateProvider(
        EntityFrameworkDocumentStoreProviderOptions options = null,
        bool forceNew = false,
        DocumentStoreOptions documentStoreOptions = null)
    {
        return new EntityFrameworkDocumentStoreProvider<StubDbContext>(
            DocumentStoreTestScopeFactory.Create(() => this.fixture.EnsurePostgresDbContext(output, true)),
            documentStoreOptions);
    }
}
