// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Common;

/// <summary>Supplies immutable entry data to a singleton sampler without a live HTTP context.</summary>
/// <param name="Method">The incoming HTTP method.</param>
/// <param name="Path">The parsed incoming path, excluding query values.</param>
/// <param name="InitialKey">The bounded default operation key.</param>
/// <param name="Node">The cached executing process descriptor.</param>
/// <param name="StartedUtc">The UTC middleware entry time.</param>
/// <example><code>var decision = sampler.Decide(context);</code></example>
public sealed record RequestProfilingSamplingContext(string Method, string Path, string InitialKey, ProfilingNode Node, DateTimeOffset StartedUtc);

/// <summary>Describes a head-sampling decision without affecting application execution.</summary>
/// <param name="Capture">Whether to attempt capture under ordinary admission limits.</param>
/// <param name="ReasonCode">A bounded, safe decision code.</param>
/// <param name="InclusionProbability">The known selection probability, or null for rate budgets.</param>
/// <example><code>return new RequestProfilingSamplingDecision(true, "Selected", 1);</code></example>
public sealed record RequestProfilingSamplingDecision(bool Capture, string ReasonCode, double? InclusionProbability = null);

/// <summary>Provides thread-safe, synchronous, bounded request-entry sampling without I/O.</summary>
/// <example><code>var selected = sampler.Decide(context).Capture;</code></example>
public interface IRequestProfilingSamplingStrategy
{
    /// <summary>Decides once at entry; implementations do not retain contexts or wait for capacity.</summary>
    /// <example><code>var decision = strategy.Decide(context);</code></example>
    RequestProfilingSamplingDecision Decide(RequestProfilingSamplingContext context);
}

/// <summary>Supplies unbiased independent probability input; tests may inject a controlled source.</summary>
/// <example><code>var value = random.NextDouble();</code></example>
public interface IRequestProfilingRandomSource
{
    /// <summary>Returns a finite random sample in the half-open interval [0, 1).</summary>
    /// <example><code>var selected = random.NextDouble() &lt; probability;</code></example>
    double NextDouble();
}

/// <summary>Selects every eligible request, still subject to recording and persistence limits.</summary>
/// <example><code>var sampler = new AllRequestsProfilingSamplingStrategy();</code></example>
public sealed class AllRequestsProfilingSamplingStrategy : IRequestProfilingSamplingStrategy
{
    /// <inheritdoc />
    public RequestProfilingSamplingDecision Decide(RequestProfilingSamplingContext context) => new(true, "Selected", 1);
}

/// <summary>Selects independent eligible requests with a validated probability.</summary>
/// <example><code>var sampler = new ProbabilityRequestProfilingSamplingStrategy(0.1);</code></example>
public sealed class ProbabilityRequestProfilingSamplingStrategy : IRequestProfilingSamplingStrategy
{
    private readonly double probability;
    private readonly IRequestProfilingRandomSource random;

    /// <summary>Creates a probability policy with optional controlled random input.</summary>
    /// <example><code>var sampler = new ProbabilityRequestProfilingSamplingStrategy(0.25, random);</code></example>
    public ProbabilityRequestProfilingSamplingStrategy(double probability, IRequestProfilingRandomSource random = null)
    {
        if (!double.IsFinite(probability) || probability < 0 || probability > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(probability));
        }

        this.probability = probability;
        this.random = random;
    }

    /// <inheritdoc />
    public RequestProfilingSamplingDecision Decide(RequestProfilingSamplingContext context)
    {
        var value = this.random?.NextDouble() ?? Random.Shared.NextDouble();
        if (!double.IsFinite(value) || value < 0 || value >= 1)
        {
            throw new InvalidOperationException("Sampling random source returned an invalid observation.");
        }

        return new(value < this.probability, "Probability", this.probability);
    }
}

/// <summary>Limits selections with a process-local monotonic token bucket, without delaying requests.</summary>
/// <example><code>var sampler = new RateLimitRequestProfilingSamplingStrategy(25, 50);</code></example>
public sealed class RateLimitRequestProfilingSamplingStrategy : IRequestProfilingSamplingStrategy
{
    private readonly object sync = new();
    private readonly double rate;
    private readonly int burst;
    private readonly TimeProvider clock;
    private double tokens;
    private long last;

    /// <summary>Creates one initially full node-local bucket for all eligible paths.</summary>
    /// <example><code>var sampler = new RateLimitRequestProfilingSamplingStrategy(10, 20, clock);</code></example>
    public RateLimitRequestProfilingSamplingStrategy(double requestsPerSecond, int burst, TimeProvider clock = null)
    {
        if (!double.IsFinite(requestsPerSecond) || requestsPerSecond <= 0 || burst <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestsPerSecond));
        }

        this.rate = requestsPerSecond;
        this.burst = burst;
        this.tokens = burst;
        this.clock = clock ?? TimeProvider.System;
        this.last = this.clock.GetTimestamp();
    }

    /// <inheritdoc />
    public RequestProfilingSamplingDecision Decide(RequestProfilingSamplingContext context)
    {
        lock (this.sync)
        {
            var now = this.clock.GetTimestamp();
            this.tokens = Math.Min(this.burst, this.tokens + Math.Max(0, this.clock.GetElapsedTime(this.last, now).TotalSeconds) * this.rate);
            this.last = now;
            if (this.tokens < 1)
            {
                return new(false, "RateBudget");
            }

            this.tokens--;
            return new(true, "Selected");
        }
    }
}
