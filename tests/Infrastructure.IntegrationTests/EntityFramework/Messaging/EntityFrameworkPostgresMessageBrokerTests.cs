// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Microsoft.EntityFrameworkCore;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))]
public class EntityFrameworkPostgresMessageBrokerTests : EntityFrameworkMessageBrokerTestsBase, IDisposable
{
    private readonly EntityFrameworkMessageBrokerTestSupport support;

    public EntityFrameworkPostgresMessageBrokerTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        fixture.WithOutput(output);
        this.support = new EntityFrameworkMessageBrokerTestSupport(
            output,
            options => options.UseNpgsql(fixture.PostgresConnectionString));
    }

    protected override EntityFrameworkMessageBrokerTestSupport Support => this.support;

    public void Dispose()
    {
        this.support.Dispose();
    }
}
