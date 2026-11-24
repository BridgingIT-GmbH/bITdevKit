// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

using BridgingIT.DevKit.Tests.Caching;

/// <summary>Runs the shared complete-provider contract scenarios against the built-in in-memory implementation.</summary>
public sealed class InMemoryCacheContractTests : CacheProviderContractTestsBase
{
    /// <inheritdoc />
    protected override Task<CacheProviderContractFixture> CreateAsync(Action<ApplicationCachingBuilderContext> configure = null)
    {
        var clock = new CacheContractClock();
        var services = CacheProviderContractFixture.CreateServices(clock, configure: configure);
        return Task.FromResult(new CacheProviderContractFixture(services.BuildServiceProvider(), clock));
    }
}