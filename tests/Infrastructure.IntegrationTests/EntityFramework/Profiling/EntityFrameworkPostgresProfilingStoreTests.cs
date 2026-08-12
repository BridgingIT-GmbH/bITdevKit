// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Profiling;

using BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Jobs;
using Microsoft.EntityFrameworkCore;
using Npgsql;

[IntegrationTest("Infrastructure")]
[Collection(nameof(JobsTestEnvironmentCollection))]
public sealed class EntityFrameworkPostgresProfilingStoreTests(JobsTestEnvironmentFixture fixture)
    : EntityFrameworkProfilingStoreTestsBase
{
    private readonly string connectionString = new NpgsqlConnectionStringBuilder(
        fixture.PostgresConnectionString
    )
    {
        Database = $"profiling_{Guid.NewGuid():N}",
    }.ConnectionString;

    protected override void ConfigureDatabase(DbContextOptionsBuilder options) =>
        options.UseNpgsql(this.connectionString);
}
