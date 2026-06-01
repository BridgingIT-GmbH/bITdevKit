// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Jobs;

using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

[IntegrationTest("Infrastructure")]
[Collection(nameof(JobsTestEnvironmentCollection))]
public class EntityFrameworkPostgresJobStoreProviderTests(ITestOutputHelper output, JobsTestEnvironmentFixture fixture) : EntityFrameworkJobStoreProviderTestsBase, IDisposable
{
    private readonly EntityFrameworkJobSchedulerTestSupport support = new(
        fixture.WithOutput(output).Output,
        options => options.UseNpgsql(fixture.PostgresConnectionString));

    protected override EntityFrameworkJobSchedulerTestSupport Support => this.support;

    public void Dispose()
    {
        this.support.Dispose();
    }
}