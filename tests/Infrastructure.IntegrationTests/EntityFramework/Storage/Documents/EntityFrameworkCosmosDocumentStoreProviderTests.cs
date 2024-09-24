// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using DotNet.Testcontainers.Containers;
using Infrastructure.EntityFramework.Storage;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class EntityFrameworkCosmosDocumentStoreProviderTests(ITestOutputHelper output, TestEnvironmentFixture fixture) : EntityFrameworkDocumentStoreProviderTestsBase
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);
    private readonly ITestOutputHelper output = output;

    [SkippableFact]
    public override async Task DeleteAsync_DeletesEntity()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.DeleteAsync_DeletesEntity();
    }

    [SkippableFact]
    public override async Task FindAsync_WithDocumentKeyAndFilterFullMatch_ReturnsFilteredEntities()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.FindAsync_WithDocumentKeyAndFilterFullMatch_ReturnsFilteredEntities();
    }

    [SkippableFact]
    public override async Task FindAsync_WithDocumentKeyAndFilterRowKeyPrefix_ReturnsFilteredEntities()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.FindAsync_WithDocumentKeyAndFilterRowKeyPrefix_ReturnsFilteredEntities();
    }

    [SkippableFact]
    public override async Task FindAsync_WithDocumentKeyAndFilterRowKeySuffix_ReturnsFilteredEntities()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.FindAsync_WithDocumentKeyAndFilterRowKeySuffix_ReturnsFilteredEntities();
    }

    [SkippableFact]
    public override async Task FindAsync_WithoutFilter_ReturnsEntities()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.FindAsync_WithoutFilter_ReturnsEntities();
    }

    [SkippableFact]
    public override async Task ListAsync_WithDocumentKeyAndFilter_ReturnsFilteredDocumentKeys()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.ListAsync_WithDocumentKeyAndFilter_ReturnsFilteredDocumentKeys();
    }

    [SkippableFact]
    public override async Task ListAsync_WithoutFilter_ReturnsDocumentKeys()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.ListAsync_WithoutFilter_ReturnsDocumentKeys();
    }

    [SkippableFact]
    public override async Task UpsertAsync_CreatesOrUpdateEntity()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.UpsertAsync_CreatesOrUpdateEntity();
    }

    protected override EntityFrameworkDocumentStoreProvider<StubDbContext> GetProvider()
    {
        return new EntityFrameworkDocumentStoreProvider<StubDbContext>(this.fixture.EnsureCosmosDbContext(this.output));
    }
}