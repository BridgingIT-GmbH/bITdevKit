// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Microsoft.EntityFrameworkCore;

[IntegrationTest("Infrastructure")]
public class EntityFrameworkSqliteQueueBrokerTests : EntityFrameworkQueueBrokerTestsBase, IDisposable
{
    private readonly string databasePath;
    private readonly EntityFrameworkQueueBrokerTestSupport support;

    public EntityFrameworkSqliteQueueBrokerTests(ITestOutputHelper output)
    {
        this.databasePath = Path.Combine(AppContext.BaseDirectory, $"entity-framework-queue-broker-{Guid.NewGuid():N}.db");
        this.support = new EntityFrameworkQueueBrokerTestSupport(
            output,
            options => options.UseSqlite($"Data Source={this.databasePath}"));
    }

    protected override EntityFrameworkQueueBrokerTestSupport Support => this.support;

    public void Dispose()
    {
        this.support.Dispose();

        try
        {
            if (File.Exists(this.databasePath))
            {
                File.Delete(this.databasePath);
            }
        }
        catch (IOException)
        {
        }
    }
}
