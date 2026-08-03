// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Configures the scheduling and shutdown behavior of a <see cref="PeriodicBackgroundService" />.
/// </summary>
/// <example>
/// <code>
/// var options = new PeriodicBackgroundServiceOptions
/// {
///     StartupDelay = TimeSpan.FromSeconds(15),
///     Interval = TimeSpan.FromHours(1),
///     StopTimeout = TimeSpan.FromSeconds(10)
/// };
/// </code>
/// </example>
public sealed class PeriodicBackgroundServiceOptions
{
    /// <summary>Gets or sets the delay after the host has started and before the first iteration.</summary>
    /// <example><code>options.StartupDelay = TimeSpan.FromSeconds(15);</code></example>
    public TimeSpan StartupDelay { get; set; }

    /// <summary>Gets or sets the delay between completed iterations.</summary>
    /// <example><code>options.Interval = TimeSpan.FromHours(1);</code></example>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets the maximum time allowed for graceful service shutdown.</summary>
    /// <example><code>options.StopTimeout = TimeSpan.FromSeconds(10);</code></example>
    public TimeSpan StopTimeout { get; set; } = TimeSpan.FromSeconds(10);

    internal void Validate()
    {
        if (this.StartupDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(this.StartupDelay), "Startup delay cannot be negative.");
        }

        if (this.Interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(this.Interval), "Interval must be greater than zero.");
        }

        if (this.StopTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(this.StopTimeout), "Stop timeout must be greater than zero.");
        }
    }
}
