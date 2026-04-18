// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Microsoft.EntityFrameworkCore;

[IntegrationTest("Infrastructure")]
public class EntityFrameworkSqliteMessageBrokerTests : EntityFrameworkMessageBrokerTestsBase, IDisposable
{
    private readonly string databasePath;
    private readonly EntityFrameworkMessageBrokerTestSupport support;

    public EntityFrameworkSqliteMessageBrokerTests(ITestOutputHelper output)
    {
        this.databasePath = Path.Combine(AppContext.BaseDirectory, $"entity-framework-message-broker-{Guid.NewGuid():N}.db");
        this.support = new EntityFrameworkMessageBrokerTestSupport(
            output,
            options => options.UseSqlite($"Data Source={this.databasePath}"));
    }

    protected override EntityFrameworkMessageBrokerTestSupport Support => this.support;

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
