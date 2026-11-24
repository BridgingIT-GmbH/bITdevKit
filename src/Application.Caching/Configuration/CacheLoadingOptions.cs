// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Defines repeatable source access, independent of provider failure handling.</summary>
/// <example><code>var options = new CacheLoadingOptions();</code></example>
/// <remarks>Successful unretained typed completion rechecks storage and reacquires shared authority within the existing node flight and original deadline. A defensive cap of 32 remote handoffs rejects repeated nonretention with CoordinationTimeout; it never transfers a remote graph or resets caller/source budgets.</remarks>
public sealed record CacheLoadingOptions
{
    /// <summary>Gets shared or explicit independent source work.</summary>
    public CacheLoadingStrategy Strategy { get; init; } = CacheLoadingStrategy.Shared;

    /// <summary>Gets explicit bounded completion after the last interested caller leaves, still within the original owned deadline.</summary>
    public bool ContinueWithoutWaiters { get; init; }

    /// <summary>Gets default node-local or explicitly authoritative shared-store ownership.</summary>
    public CacheCoordinationScope CoordinationScope { get; init; } = CacheCoordinationScope.NodeLocal;

    /// <summary>Gets total owned deadline including retry, backoff, and publication.</summary>
    public TimeSpan SourceTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets caller wait budget, distinct from the owned source token.</summary>
    public TimeSpan WaitTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets total owned source attempts. One disables automatic retry.</summary>
    public int MaxAttempts { get; init; } = 1;

    /// <summary>Gets base bounded delay between configured attempts.</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Gets trusted registered process-local dependency resilience alias.</summary>
    public string SourceResilience { get; init; } = null;

    /// <summary>Gets the lower bound for one-per-node remote observations, including their bounded jitter.</summary>
    public TimeSpan ObservationMinimumDelay { get; init; } = TimeSpan.FromMilliseconds(50);

    /// <summary>Gets the remote-observation backoff ceiling; per-flight jitter stays between this ceiling and the configured minimum.</summary>
    public TimeSpan ObservationMaximumDelay { get; init; } = TimeSpan.FromSeconds(1);
}