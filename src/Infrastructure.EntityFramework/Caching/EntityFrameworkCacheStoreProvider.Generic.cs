// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Runs the advanced relational cache through a host context exposing the cache persistence capability.</summary>
/// <typeparam name="TContext">The independently created standalone or module context.</typeparam>
/// <param name="name">The normalized provider registration name.</param>
/// <param name="options">The selected engine, resource limits and timing policies.</param>
/// <param name="contexts">Creates a fresh context owned and disposed by each bounded cache operation.</param>
/// <param name="timeProvider">The clock used for leases, deadlines and expiration.</param>
/// <param name="resources">Optional shared advanced-cache resource budgets.</param>
/// <param name="ready">Optional host database-readiness coordination.</param>
/// <param name="logger">Optional structured provider logger.</param>
/// <param name="diagnostics">The host diagnostics policy.</param>
/// <remarks>Deploy the host context's migrations before readiness. Every cache token keeps binary comparison semantics.</remarks>
/// <example><code>var provider = new EntityFrameworkCacheStoreProvider&lt;CatalogDbContext&gt;("database", options, () =&gt; new CatalogDbContext(contextOptions));</code></example>
public sealed class EntityFrameworkCacheStoreProvider<TContext>(string name, CacheProviderOptions options, Func<TContext> contexts,
    TimeProvider timeProvider = null, CacheRuntimeResources resources = null, IDatabaseReadyService ready = null,
    ILogger<EntityFrameworkCacheStoreProvider> logger = null, CacheDiagnosticsOptions diagnostics = null)
    : EntityFrameworkCacheStoreProvider(name, options, contexts, timeProvider, resources, ready, logger, diagnostics)
    where TContext : DbContext, IApplicationCachingContext;