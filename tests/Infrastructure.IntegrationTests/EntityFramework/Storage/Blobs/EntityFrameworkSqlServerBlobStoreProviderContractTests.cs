// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))]
public sealed class EntityFrameworkSqlServerBlobStoreProviderContractTests(
    ITestOutputHelper output,
    TestEnvironmentFixture fixture)
    : EntityFrameworkBlobStoreProviderContractTestsBase(output, fixture)
{
    protected override StubDbContext CreateDbContext(bool forceNew = false) =>
        this.Fixture.EnsureSqlServerDbContext(this.Output, forceNew);
}

