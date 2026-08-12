// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Configures the opt-in profiling feature.
/// </summary>
/// <example>
/// <code>
/// services.AddProfiling(options => options
///     .Enabled()
///     .SamplingInterval(TimeSpan.FromSeconds(1))
///     .Duration(TimeSpan.FromSeconds(30)));
/// </code>
/// </example>
public sealed class ProfilingOptions
{
    /// <summary>Gets the fixed minimum supported sampling interval.</summary>
    /// <example><code>var minimum = ProfilingOptions.MinimumSamplingInterval;</code></example>
    public static readonly TimeSpan MinimumSamplingInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>Gets the format used for a generated default session name.</summary>
    /// <example><code>var name = utcNow.ToString(ProfilingOptions.DefaultSessionNameFormat);</code></example>
    public const string DefaultSessionNameFormat = "O";

    /// <summary>Gets or sets whether profiling collection is enabled.</summary>
    /// <example><code>options.Enabled = environment.IsDevelopment();</code></example>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the default interval between scheduled snapshots.</summary>
    /// <example><code>options.SamplingInterval = TimeSpan.FromSeconds(1);</code></example>
    public TimeSpan SamplingInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets or sets the required maximum duration of a collection session.</summary>
    /// <example><code>options.Duration = TimeSpan.FromSeconds(30);</code></example>
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets whether collection stops when the configured duration elapses.</summary>
    /// <example><code>options.AutomaticStop = true;</code></example>
    public bool AutomaticStop { get; set; } = true;

    /// <summary>Gets or sets the maximum number of retained unpinned terminal sessions.</summary>
    /// <example><code>options.MaximumRetainedSessions = 20;</code></example>
    public int MaximumRetainedSessions { get; set; } = 20;

    /// <summary>Gets or sets the maximum age of an unpinned terminal session.</summary>
    /// <example><code>options.MaximumSessionAge = TimeSpan.FromDays(7);</code></example>
    public TimeSpan MaximumSessionAge { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Gets or sets the default dashboard refresh interval.</summary>
    /// <example><code>options.RefreshInterval = TimeSpan.FromSeconds(5);</code></example>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the deadline for accepting start-command participants.</summary>
    /// <example><code>options.ParticipationDeadline = TimeSpan.FromSeconds(1);</code></example>
    public TimeSpan ParticipationDeadline { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets or sets the delay after the logical end before finalization may occur.</summary>
    /// <example><code>options.FinalizationGracePeriod = TimeSpan.FromSeconds(1);</code></example>
    public TimeSpan FinalizationGracePeriod { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Validates the configured profiling limits.</summary>
    /// <exception cref="InvalidOperationException">Thrown when an enabled configuration is invalid.</exception>
    /// <example><code>options.Validate();</code></example>
    public void Validate()
    {
        if (!this.Enabled)
        {
            return;
        }

        if (this.SamplingInterval < MinimumSamplingInterval)
        {
            throw new InvalidOperationException(
                $"The profiling sampling interval must be at least {MinimumSamplingInterval.TotalMilliseconds:0} ms."
            );
        }

        if (this.Duration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "The profiling collection duration must be greater than zero."
            );
        }

        if (!this.AutomaticStop)
        {
            throw new InvalidOperationException(
                "Profiling collection requires automatic stop at the configured duration."
            );
        }

        if (this.MaximumRetainedSessions <= 0)
        {
            throw new InvalidOperationException(
                "The maximum retained profiling session count must be greater than zero."
            );
        }

        if (
            this.MaximumSessionAge <= TimeSpan.Zero
            || this.RefreshInterval <= TimeSpan.Zero
            || this.ParticipationDeadline <= TimeSpan.Zero
            || this.FinalizationGracePeriod < TimeSpan.Zero
        )
        {
            throw new InvalidOperationException(
                "Profiling time limits must be positive and the finalization grace period cannot be negative."
            );
        }
    }
}

/// <summary>
/// Fluently updates one shared <see cref="ProfilingOptions"/> instance.
/// </summary>
/// <example><code>options.Enabled().Duration(TimeSpan.FromMinutes(1));</code></example>
public sealed class ProfilingOptionsBuilder
{
    /// <summary>Creates a builder for the supplied options.</summary>
    /// <param name="target">The options instance to update.</param>
    /// <example><code>var builder = new ProfilingOptionsBuilder(options);</code></example>
    public ProfilingOptionsBuilder(ProfilingOptions target)
    {
        this.Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    private ProfilingOptions Target { get; }

    /// <summary>Enables or disables profiling collection.</summary>
    /// <param name="enabled">Whether collection is enabled.</param>
    /// <returns>This builder.</returns>
    /// <example><code>options.Enabled(environment.IsDevelopment());</code></example>
    public ProfilingOptionsBuilder Enabled(bool enabled = true)
    {
        this.Target.Enabled = enabled;
        return this;
    }

    /// <summary>Sets the default sampling interval.</summary>
    /// <param name="value">An interval of at least 500 milliseconds.</param>
    /// <returns>This builder.</returns>
    /// <example><code>options.SamplingInterval(TimeSpan.FromSeconds(1));</code></example>
    public ProfilingOptionsBuilder SamplingInterval(TimeSpan value)
    {
        if (value < ProfilingOptions.MinimumSamplingInterval)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"The sampling interval must be at least {ProfilingOptions.MinimumSamplingInterval.TotalMilliseconds:0} ms."
            );
        }

        this.Target.SamplingInterval = value;
        return this;
    }

    /// <summary>Sets the required maximum collection duration.</summary>
    /// <param name="value">A positive duration.</param>
    /// <returns>This builder.</returns>
    /// <example><code>options.Duration(TimeSpan.FromSeconds(30));</code></example>
    public ProfilingOptionsBuilder Duration(TimeSpan value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
        this.Target.Duration = value;
        return this;
    }

    /// <summary>Sets the terminal-session retention limits.</summary>
    /// <param name="maximumSessions">The positive maximum retained session count.</param>
    /// <param name="maximumAge">The positive maximum session age.</param>
    /// <returns>This builder.</returns>
    /// <example><code>options.Retention(20, TimeSpan.FromDays(7));</code></example>
    public ProfilingOptionsBuilder Retention(int maximumSessions, TimeSpan maximumAge)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSessions);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumAge, TimeSpan.Zero);
        this.Target.MaximumRetainedSessions = maximumSessions;
        this.Target.MaximumSessionAge = maximumAge;
        return this;
    }

    /// <summary>Sets the dashboard refresh interval.</summary>
    /// <param name="value">A positive refresh interval.</param>
    /// <returns>This builder.</returns>
    /// <example><code>options.RefreshInterval(TimeSpan.FromSeconds(5));</code></example>
    public ProfilingOptionsBuilder RefreshInterval(TimeSpan value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
        this.Target.RefreshInterval = value;
        return this;
    }

    /// <summary>Sets the start participation deadline.</summary>
    /// <param name="value">A positive deadline.</param>
    /// <returns>This builder.</returns>
    /// <example><code>options.ParticipationDeadline(TimeSpan.FromSeconds(1));</code></example>
    public ProfilingOptionsBuilder ParticipationDeadline(TimeSpan value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
        this.Target.ParticipationDeadline = value;
        return this;
    }

    /// <summary>Sets the session finalization grace period.</summary>
    /// <param name="value">A non-negative grace period.</param>
    /// <returns>This builder.</returns>
    /// <example><code>options.FinalizationGracePeriod(TimeSpan.FromSeconds(1));</code></example>
    public ProfilingOptionsBuilder FinalizationGracePeriod(TimeSpan value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
        this.Target.FinalizationGracePeriod = value;
        return this;
    }
}
