// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.FileSystem.Caching;
using BridgingIT.DevKit.Tests.Caching;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Runs the portable contract against a real local durable native filesystem root.</summary>
/// <remarks>This fixture does not qualify SMB, NFS, another operating system, or power-loss durability.</remarks>
public sealed class FileSystemCacheContractTests : CacheProviderContractTestsBase
{
    /// <inheritdoc />
    protected override Task<CacheProviderContractFixture> CreateAsync(Action<ApplicationCachingBuilderContext> configure = null)
    {
        var path = Path.Combine(Path.GetTempPath(), "bdk-cache-contracts", Guid.NewGuid().ToString("N"));
        var clock = new CacheContractClock();
        var capabilities = FileSystemCacheStoreProvider.CapabilitiesFor(new() { RootPath = path });
        var services = CacheProviderContractFixture.CreateServices(clock, "FileSystem", capabilities,
            (sp, name, options) => new FileSystemCacheStoreProvider(name, options, clock, sp.GetRequiredService<CacheRuntimeResources>()), configure,
            configureProvider: p => p.With(x => x.RootPath, path));
        return Task.FromResult(new CacheProviderContractFixture(services.BuildServiceProvider(), clock, () =>
        {
            if (Directory.Exists(path)) { Directory.Delete(path, recursive: true); }

            return ValueTask.CompletedTask;
        }));
    }
}