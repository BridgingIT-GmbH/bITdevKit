// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Selects trusted built-in observers. Configuration can replace or clear these optional stage lists.</summary>
/// <example><code>caching.WithLogging().WithMetrics().WithCollection&lt;ProductCard&gt;("products");</code></example>
public static class CacheObserverBuilderExtensions
{
    /// <summary>Selects structured operation and owned-source logging without keys or payloads.</summary>
    public static CacheCollectionBuilder WithLogging(this CacheCollectionBuilder builder) => Select(builder, "logging", true);

    /// <summary>Selects bounded operation and owned-source instruments. Actual source attempts are measured separately.</summary>
    public static CacheCollectionBuilder WithMetrics(this CacheCollectionBuilder builder) => Select(builder, "metrics", true);

    /// <summary>Selects optional Common operation profiling, with independent owned-source roots.</summary>
    public static CacheCollectionBuilder WithProfiling(this CacheCollectionBuilder builder) => Select(builder, "profiling", true);

    /// <summary>Selects optional Common auditing for explicit mutations without recording keys or values.</summary>
    public static CacheCollectionBuilder WithAuditing(this CacheCollectionBuilder builder) => Select(builder, "auditing", false);

    /// <summary>Selects logging in global fluent collection defaults.</summary>
    public static ApplicationCachingBuilderContext WithLogging(this ApplicationCachingBuilderContext builder) => builder.Defaults(c => c.WithLogging());

    /// <summary>Selects metrics and tracing in global fluent collection defaults.</summary>
    public static ApplicationCachingBuilderContext WithMetrics(this ApplicationCachingBuilderContext builder) => builder.Defaults(c => c.WithMetrics());

    /// <summary>Selects Common profiling in global fluent collection defaults.</summary>
    public static ApplicationCachingBuilderContext WithProfiling(this ApplicationCachingBuilderContext builder) => builder.Defaults(c => c.WithProfiling());

    /// <summary>Selects Common auditing in global fluent collection defaults.</summary>
    public static ApplicationCachingBuilderContext WithAuditing(this ApplicationCachingBuilderContext builder) => builder.Defaults(c => c.WithAuditing());

    private static CacheCollectionBuilder Select(CacheCollectionBuilder builder, string alias, bool source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Patch.AppendAlias("Behaviors.CollectionOperations", alias);
        if (source)
        {
            builder.Patch.AppendAlias("Behaviors.SourceExecution", alias);
        }

        return builder;
    }
}