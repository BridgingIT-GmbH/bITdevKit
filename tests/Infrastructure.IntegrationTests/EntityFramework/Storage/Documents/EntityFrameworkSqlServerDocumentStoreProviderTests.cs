// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class EntityFrameworkSqlServerDocumentStoreProviderTests(ITestOutputHelper output, TestEnvironmentFixture fixture) : EntityFrameworkDocumentStoreProviderTestsBase
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);
    private readonly ITestOutputHelper output = output;

    [Fact]
    public override async Task DeleteAsync_DeletesEntity()
    {
        await base.DeleteAsync_DeletesEntity();
    }

    [Fact]
    public override async Task FindAsync_WithDocumentKeyAndFilterFullMatch_ReturnsFilteredEntities()
    {
        await base.FindAsync_WithDocumentKeyAndFilterFullMatch_ReturnsFilteredEntities();
    }

    [Fact]
    public override async Task FindAsync_WithDocumentKeyAndFilterRowKeyPrefix_ReturnsFilteredEntities()
    {
        await base.FindAsync_WithDocumentKeyAndFilterRowKeyPrefix_ReturnsFilteredEntities();
    }

    [Fact]
    public override async Task FindAsync_WithDocumentKeyAndFilterRowKeySuffix_ReturnsFilteredEntities()
    {
        await base.FindAsync_WithDocumentKeyAndFilterRowKeySuffix_ReturnsFilteredEntities();
    }

    [Fact]
    public override async Task FindAsync_WithoutFilter_ReturnsEntities()
    {
        await base.FindAsync_WithoutFilter_ReturnsEntities();
    }

    [Fact]
    public override async Task ListAsync_WithDocumentKeyAndFilter_ReturnsFilteredDocumentKeys()
    {
        await base.ListAsync_WithDocumentKeyAndFilter_ReturnsFilteredDocumentKeys();
    }

    [Fact]
    public override async Task ListAsync_WithoutFilter_ReturnsDocumentKeys()
    {
        await base.ListAsync_WithoutFilter_ReturnsDocumentKeys();
    }

    [Fact]
    public override async Task UpsertAsync_CreatesOrUpdateEntity()
    {
        await base.UpsertAsync_CreatesOrUpdateEntity();
    }

    protected override EntityFrameworkDocumentStoreProvider<StubDbContext> GetProvider()
    {
        return new EntityFrameworkDocumentStoreProvider<StubDbContext>(
            this.fixture.EnsureSqlServerDbContext(this.output));
    }
}