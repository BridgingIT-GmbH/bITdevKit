// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Orchestrations;

using Microsoft.EntityFrameworkCore;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))]
public class EntityFrameworkPostgresOrchestrationStorageTests : EntityFrameworkOrchestrationStorageTestsBase, IDisposable
{
    private readonly EntityFrameworkOrchestrationTestSupport support;

    public EntityFrameworkPostgresOrchestrationStorageTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        fixture.WithOutput(output);
        this.support = new EntityFrameworkOrchestrationTestSupport(
            output,
            options => options.UseNpgsql(fixture.PostgresConnectionString));
    }

    protected override EntityFrameworkOrchestrationTestSupport Support => this.support;

    public void Dispose()
    {
        this.support.Dispose();
    }
}