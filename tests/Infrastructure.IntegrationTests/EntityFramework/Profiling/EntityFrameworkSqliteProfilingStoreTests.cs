// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Profiling;

using Microsoft.EntityFrameworkCore;

[IntegrationTest("Infrastructure")]
[Collection(nameof(IsolatedSqliteTestEnvironmentCollection))]
public sealed class EntityFrameworkSqliteProfilingStoreTests
    : EntityFrameworkProfilingStoreTestsBase
{
    private readonly string connectionString =
        $"Data Source={Path.Combine(Path.GetTempPath(), $"profiling-{Guid.NewGuid():N}.db")}";

    protected override void ConfigureDatabase(DbContextOptionsBuilder options) =>
        options.UseSqlite(this.connectionString);
}
