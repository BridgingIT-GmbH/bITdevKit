// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Profiling;

using BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class EntityFrameworkProfilingStoreContractTests
    : ProfilingStoreContractTests,
        IDisposable
{
    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        $"bitdevkit-profiling-{Guid.NewGuid():N}.db"
    );
    private readonly ServiceProvider provider;

    public EntityFrameworkProfilingStoreContractTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ProfilingTestDbContext>(options =>
            options.UseSqlite($"Data Source={this.databasePath};Pooling=False")
        );
        services
            .AddProfiling(options => options.Enabled())
            .WithEntityFrameworkStore<ProfilingTestDbContext>();
        this.provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true }
        );
        using var scope = this.provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ProfilingTestDbContext>().Database.EnsureCreated();
    }

    protected override bool ExpectedSupportsMultiNode => true;

    protected override IProfilingStore CreateStore() =>
        this.provider.GetRequiredService<IProfilingStore>();

    public void Dispose()
    {
        this.provider.Dispose();
        if (File.Exists(this.databasePath))
        {
            File.Delete(this.databasePath);
        }
    }
}
