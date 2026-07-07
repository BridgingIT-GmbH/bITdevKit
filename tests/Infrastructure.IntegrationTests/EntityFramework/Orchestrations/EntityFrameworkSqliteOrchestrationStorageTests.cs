// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Orchestrations;

using Microsoft.EntityFrameworkCore;

[IntegrationTest("Infrastructure")]
[Collection(nameof(IsolatedSqliteTestEnvironmentCollection))]
public class EntityFrameworkSqliteOrchestrationStorageTests : EntityFrameworkOrchestrationStorageTestsBase, IDisposable
{
    private readonly string databasePath;
    private readonly EntityFrameworkOrchestrationTestSupport support;

    public EntityFrameworkSqliteOrchestrationStorageTests(ITestOutputHelper output)
    {
        this.databasePath = Path.Combine(AppContext.BaseDirectory, $"entity-framework-orchestration-{Guid.NewGuid():N}.db");
        this.support = new EntityFrameworkOrchestrationTestSupport(
            output,
            options => options.UseSqlite($"Data Source={this.databasePath}"));
    }

    protected override EntityFrameworkOrchestrationTestSupport Support => this.support;

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
