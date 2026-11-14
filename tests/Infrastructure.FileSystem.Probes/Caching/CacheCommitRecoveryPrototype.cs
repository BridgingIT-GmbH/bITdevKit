// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

/// <summary>Preserves the early probe entry point while executing the complete production cache provider and recovery APIs.</summary>
/// <remarks>No commit, journal, manifest or quota algorithm is duplicated in this compatibility entry point.</remarks>
/// <example><code>var passed = await CacheCommitRecoveryPrototype.RunAsync(markedWorkspace);</code></example>
public static class CacheCommitRecoveryPrototype
{
    /// <summary>Runs the nine retained early protocol regressions through the production implementation.</summary>
    /// <param name="workspace">An existing marked isolated scratch workspace.</param>
    /// <returns>The identifiers of actual passed production scenarios.</returns>
    public static Task<IReadOnlyList<string>> RunAsync(string workspace) => CacheProcessProbe.RunAsync(workspace);

    /// <summary>Runs a production worker for a controlled legacy or current crash-boundary identifier.</summary>
    /// <param name="workspace">The parent-owned marked workspace.</param>
    /// <param name="scenario">The controlled native publication boundary.</param>
    /// <returns>A failure code if execution unexpectedly passes the intended termination point.</returns>
    public static Task<int> WorkerAsync(string workspace, string scenario) => CacheProcessProbe.WorkerAsync(workspace, scenario);
}