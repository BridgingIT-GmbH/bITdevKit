// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using Microsoft.EntityFrameworkCore.Infrastructure;

/// <summary>Separates EF models by cache schema/prefix and native engine, including design-time models.</summary>
/// <example><code>options.ReplaceService&lt;IModelCacheKeyFactory, CacheModelCacheKeyFactory&gt;();</code></example>
public sealed class CacheModelCacheKeyFactory : IModelCacheKeyFactory
{
    /// <inheritdoc />
    public object Create(DbContext context, bool designTime) => context is CacheDbContext cache ?
        (context.GetType(), cache.SchemaOptions.Schema, cache.SchemaOptions.TablePrefix, context.Database.ProviderName, designTime) : (context.GetType(), designTime);
}