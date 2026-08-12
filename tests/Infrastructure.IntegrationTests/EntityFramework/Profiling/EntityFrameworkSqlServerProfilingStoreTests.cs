// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Profiling;

using BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Jobs;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

[IntegrationTest("Infrastructure")]
[Collection(nameof(JobsTestEnvironmentCollection))]
public sealed class EntityFrameworkSqlServerProfilingStoreTests(JobsTestEnvironmentFixture fixture)
    : EntityFrameworkProfilingStoreTestsBase
{
    private readonly string connectionString = new SqlConnectionStringBuilder(
        fixture.SqlConnectionString
    )
    {
        InitialCatalog = $"Profiling_{Guid.NewGuid():N}",
    }.ConnectionString;

    protected override void ConfigureDatabase(DbContextOptionsBuilder options) =>
        options.UseSqlServer(this.connectionString);
}
