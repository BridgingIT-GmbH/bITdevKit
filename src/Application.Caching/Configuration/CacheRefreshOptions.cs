// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Configures optional cache-owned refresh-ahead.</summary>
/// <example><code>var options = new CacheRefreshOptions();</code></example>
public sealed record CacheRefreshOptions
{
    /// <summary>Gets whether proactive refresh is enabled.</summary>
    public bool Enabled { get; init; } = false;

    /// <summary>Gets trusted registered scoped refresh loader alias.</summary>
    public string Loader { get; init; } = null;

    /// <summary>Gets delay after a completed worker iteration.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Gets maximum refresh lead, also capped to twenty percent of actual lifetime.</summary>
    public TimeSpan RefreshAhead { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets foreground activity eligibility window.</summary>
    public TimeSpan ActivityWindow { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Gets bounded candidate count per collection iteration.</summary>
    public int CandidateLimit { get; init; } = 128;

    /// <summary>Gets maximum new source admission work per sweep.</summary>
    public TimeSpan NewWorkBudget { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets initial source failure backoff.</summary>
    public TimeSpan FailureBackoff { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets maximum source failure backoff.</summary>
    public TimeSpan MaximumFailureBackoff { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets consecutive failures before pausing this version.</summary>
    public int FailuresBeforePause { get; init; } = 3;

    /// <summary>Gets per-version pause after repeated failures.</summary>
    public TimeSpan FailurePause { get; init; } = TimeSpan.FromMinutes(5);
}