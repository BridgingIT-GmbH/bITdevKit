// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HandlerCircuitBreakerAttribute : Attribute
{
    public HandlerCircuitBreakerAttribute(int attempts, int breakDurationSeconds, int backoffMilliseconds, bool backoffExponential = false)
    {
        if (attempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attempts), "Attempts must be greater than 0.");
        }
        if (breakDurationSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(breakDurationSeconds), "Break duration must be non-negative.");
        }
        if (backoffMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(backoffMilliseconds), "Backoff milliseconds must be non-negative.");
        }

        this.Attempts = attempts;
        this.BreakDuration = TimeSpan.FromSeconds(breakDurationSeconds);
        this.Backoff = TimeSpan.FromMilliseconds(backoffMilliseconds);
        this.BackoffExponential = backoffExponential;
    }

    public int Attempts { get; }

    public TimeSpan BreakDuration { get; }

    public TimeSpan Backoff { get; }

    public bool BackoffExponential { get; }
}
