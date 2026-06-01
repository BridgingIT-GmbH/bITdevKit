// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Represents retry configuration for a job or trigger.
/// </summary>
/// <param name="MaxAttempts">The maximum number of attempts.</param>
/// <param name="Delay">The retry delay.</param>
/// <param name="UseExponentialBackoff">Indicates whether exponential backoff should be used.</param>
public sealed record JobRetryPolicy(
    int MaxAttempts,
    TimeSpan? Delay,
    bool UseExponentialBackoff);

/// <summary>
/// Builds retry policy definitions.
/// </summary>
public class JobRetryPolicyBuilder
{
    private int maxAttempts;
    private TimeSpan? delay;
    private bool useExponentialBackoff;

    /// <summary>
    /// Sets the maximum number of attempts.
    /// </summary>
    public JobRetryPolicyBuilder MaxAttempts(int value)
    {
        this.maxAttempts = value;
        return this;
    }

    /// <summary>
    /// Sets a fixed retry delay.
    /// </summary>
    public JobRetryPolicyBuilder FixedDelay(TimeSpan value)
    {
        this.delay = value;
        this.useExponentialBackoff = false;
        return this;
    }

    /// <summary>
    /// Sets an exponential retry delay shape.
    /// </summary>
    public JobRetryPolicyBuilder ExponentialBackoff(TimeSpan initialDelay)
    {
        this.delay = initialDelay;
        this.useExponentialBackoff = true;
        return this;
    }

    /// <summary>
    /// Builds the retry policy.
    /// </summary>
    public JobRetryPolicy Build()
    {
        return new JobRetryPolicy(this.maxAttempts, this.delay, this.useExponentialBackoff);
    }
}