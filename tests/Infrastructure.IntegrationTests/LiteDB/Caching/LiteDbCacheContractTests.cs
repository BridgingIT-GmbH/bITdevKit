// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using BridgingIT.DevKit.Tests.Caching;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Runs the shared Application Caching provider contract against a disposable native file.</summary>
public sealed class LiteDbCacheContractTests : CacheProviderContractTestsBase
{
    /// <inheritdoc />
    protected override Task<CacheProviderContractFixture> CreateAsync(Action<ApplicationCachingBuilderContext> configure = null) => Task.FromResult(Create(configure: configure));

    /// <summary>Creates identical cache contracts with native persistence and a controlled clock.</summary>
    /// <param name="file">Optional durable file preserved between hosts.</param>
    /// <param name="clock">Optional clock shared between reopened hosts.</param>
    /// <param name="configure">Optional common policy overrides.</param>
    /// <returns>A configured portable fixture that owns its host and temporary directory.</returns>
    public static CacheProviderContractFixture Create(string file = null, CacheContractClock clock = null, Action<ApplicationCachingBuilderContext> configure = null)
    {
        global::LiteDB.LiteDBPragmas.I_AM_AWARE_MY_DATABASE_BREAKS_WHEN_I_USE_THIS();
        var directory = Directory.CreateTempSubdirectory("bdk-litedb-appcache-");
        file ??= Path.Combine(directory.FullName, "cache.db");
        clock ??= new();
        LiteDbFeatureOptions native = null;
        var services = CacheProviderContractFixture.CreateServices(clock, "LiteDB", LiteDbCacheStoreProvider.DefaultCapabilities,
            (sp, name, options) => new LiteDbCacheStoreProvider(sp.GetRequiredService<IServiceScopeFactory>(), native, name, options,
                sp.GetRequiredService<TimeProvider>(), sp.GetService<CacheRuntimeResources>()), configure);
        services.AddLiteDbDatabase(options => options.ConnectionString = file);
        native = services.RegisterLiteDbFeature("appcache", ["state", "catalog", "entries", "payloads", "chunks", "keys", "fences", "operations", "receipts"]);
        return new(services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true }), clock, () => { directory.Delete(true); return ValueTask.CompletedTask; });
    }
}
