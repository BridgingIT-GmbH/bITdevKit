// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Jobs;

using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

[IntegrationTest("Infrastructure")]
[Collection(nameof(IsolatedSqliteTestEnvironmentCollection))]
public class EntityFrameworkSqliteJobStoreProviderTests : EntityFrameworkJobStoreProviderTestsBase, IDisposable
{
    private readonly string databasePath;
    private readonly EntityFrameworkJobSchedulerTestSupport support;

    public EntityFrameworkSqliteJobStoreProviderTests(ITestOutputHelper output)
    {
        this.databasePath = Path.Combine(AppContext.BaseDirectory, $"entity-framework-jobs-{Guid.NewGuid():N}.db");
        this.support = new EntityFrameworkJobSchedulerTestSupport(
            output,
            options => options.UseSqlite($"Data Source={this.databasePath}"));
    }

    protected override EntityFrameworkJobSchedulerTestSupport Support => this.support;

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
