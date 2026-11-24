// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

/// <summary>Reports deferred relational cleanup without changing an acknowledged cache result.</summary>
/// <example><code>CacheRelationalLog.Deferred(logger, "database", "reader-release");</code></example>
public static class CacheRelationalLog
{
    /// <summary>Writes a best-effort warning when native cleanup must be retried or reclaimed later.</summary>
    /// <param name="logger">The optional host logger.</param>
    /// <param name="provider">The registered cache provider name.</param>
    /// <param name="kind">The bounded cleanup category, without keys or payload content.</param>
    public static void Deferred(ILogger logger, string provider, string kind)
    {
        try { logger?.LogWarning("[{LogKey}] cache native cleanup remains deferred (Provider={Provider}, Kind={Kind})", "ApplicationCaching", provider, kind); }
        catch { /* Logging cannot rewrite an acknowledged result or release uncertain quota. */ }
    }
}