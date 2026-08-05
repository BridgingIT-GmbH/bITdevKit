// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Broadcasting;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

[IntegrationTest("Infrastructure")]
[Collection(nameof(IsolatedSqliteTestEnvironmentCollection))]
public sealed class EntityFrameworkSqliteBroadcastRegistryStoreTests
    : EntityFrameworkBroadcastRegistryStoreTestsBase,
        IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    protected override void ConfigureDatabase(DbContextOptionsBuilder options) =>
        options.UseSqlite(this.connection);

    public Task InitializeAsync() => this.connection.OpenAsync();

    public async Task DisposeAsync() => await this.connection.DisposeAsync();
}
