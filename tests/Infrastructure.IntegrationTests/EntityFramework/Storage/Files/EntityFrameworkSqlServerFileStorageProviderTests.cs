// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class EntityFrameworkSqlServerFileStorageProviderTests : EntityFrameworkFileStorageProviderTestsBase, IDisposable
{
    private readonly TestEnvironmentFixture fixture;
    private readonly EntityFrameworkRelationalFileStorageTestSupport support;

    public EntityFrameworkSqlServerFileStorageProviderTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        this.fixture = fixture.WithOutput(output);
        _ = this.fixture.EnsureSqlServerDbContext(output);
        this.support = new EntityFrameworkRelationalFileStorageTestSupport(
            output,
            options => options.UseSqlServer(this.fixture.SqlConnectionString));
    }

    protected override EntityFrameworkFileStorageOptions DefaultOptions => this.support.DefaultOptions;

    protected override ServiceProvider ServiceProvider => this.support.ServiceProvider;

    protected override IFileStorageProvider CreateInMemoryProvider(string locationName)
        => this.support.CreateInMemoryProvider(locationName);

    protected override EntityFrameworkFileStorageProvider<StubDbContext> CreateProvider(
        string locationName,
        EntityFrameworkFileStorageOptions options = null)
        => this.support.CreateProvider(locationName, options);

    public void Dispose()
    {
        this.support.Dispose();
    }
}
