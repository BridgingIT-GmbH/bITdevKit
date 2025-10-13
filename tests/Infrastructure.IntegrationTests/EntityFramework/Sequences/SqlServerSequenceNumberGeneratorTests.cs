// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Sequences;

using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Xunit;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class SqlServerSequenceNumberGeneratorTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    : SequenceNumberGeneratorTestsBase
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);
    private readonly ITestOutputHelper output = output;

    protected override ISequenceNumberGenerator CreateGenerator()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(new XunitLoggerProvider(this.output)));

        var db = this.fixture.EnsureSqlServerDbContext(this.output);
        services.AddDbContext<StubDbContext>(options =>
        {
            options.UseSqlServer(this.fixture.SqlConnectionString);
        });

        services.AddScoped<ISequenceNumberGenerator, SqlServerSequenceNumberGenerator<StubDbContext>>(
            sp => new SqlServerSequenceNumberGenerator<StubDbContext>(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IServiceProvider>(),
                new SequenceNumberGeneratorOptions()));

        var serviceProvider = services.BuildServiceProvider();
        this.ServiceProvider = serviceProvider;  // Set for base class if needed

        // Ensure the database and sequences are created
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<StubDbContext>();

        return serviceProvider.GetRequiredService<ISequenceNumberGenerator>();
    }

    [Fact]
    public override async Task GetNextAsync_SequenceExists_ReturnsNextValue()
    {
        await base.GetNextAsync_SequenceExists_ReturnsNextValue();
    }

    [Fact]
    public override async Task GetNextAsync_SequenceDoesNotExist_ReturnsFailureWithNotFoundError()
    {
        await base.GetNextAsync_SequenceDoesNotExist_ReturnsFailureWithNotFoundError();
    }

    [Fact]
    public override async Task GetSequenceInfoAsync_SequenceExists_ReturnsInfo()
    {
        await base.GetSequenceInfoAsync_SequenceExists_ReturnsInfo();
    }

    [Fact]
    public override async Task ResetSequenceAsync_SequenceExists_ResetsSuccessfully()
    {
        await base.ResetSequenceAsync_SequenceExists_ResetsSuccessfully();
    }

    [Fact]
    public override async Task GetNextMultipleAsync_MultipleSequences_ReturnsValues()
    {
        await base.GetNextMultipleAsync_MultipleSequences_ReturnsValues();
    }
}