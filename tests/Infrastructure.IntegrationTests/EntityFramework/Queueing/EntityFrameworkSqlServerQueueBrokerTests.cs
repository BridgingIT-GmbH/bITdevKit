// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Microsoft.EntityFrameworkCore;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))]
public class EntityFrameworkSqlServerQueueBrokerTests : EntityFrameworkQueueBrokerTestsBase, IDisposable
{
    private readonly EntityFrameworkQueueBrokerTestSupport support;

    public EntityFrameworkSqlServerQueueBrokerTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        fixture.WithOutput(output);
        this.support = new EntityFrameworkQueueBrokerTestSupport(
            output,
            options => options.UseSqlServer(fixture.SqlConnectionString));
    }

    protected override EntityFrameworkQueueBrokerTestSupport Support => this.support;

    public void Dispose()
    {
        this.support.Dispose();
    }
}
