namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;

[Collection(nameof(TestEnvironmentCollection))]
public sealed class EntityFrameworkSqlServerDocumentStoreConcurrencyContractTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    : Shared.Storage.DocumentStoreConcurrencyContractTests
{
    protected override IDocumentStoreProvider CreateProvider() => new EntityFrameworkDocumentStoreProvider<StubDbContext>(
        DocumentStoreTestScopeFactory.Create(() => fixture.EnsureSqlServerDbContext(output, true)));
}

[Collection(nameof(TestEnvironmentCollection))]
public sealed class EntityFrameworkPostgresDocumentStoreConcurrencyContractTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    : Shared.Storage.DocumentStoreConcurrencyContractTests
{
    protected override IDocumentStoreProvider CreateProvider() => new EntityFrameworkDocumentStoreProvider<StubDbContext>(
        DocumentStoreTestScopeFactory.Create(() => fixture.EnsurePostgresDbContext(output, true)));
}

[Collection(nameof(TestEnvironmentCollection))]
public sealed class EntityFrameworkSqliteDocumentStoreConcurrencyContractTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    : Shared.Storage.DocumentStoreConcurrencyContractTests
{
    protected override IDocumentStoreProvider CreateProvider() => new EntityFrameworkDocumentStoreProvider<StubDbContext>(
        DocumentStoreTestScopeFactory.Create(() => fixture.EnsureSqliteDbContext(output, true)));
}
