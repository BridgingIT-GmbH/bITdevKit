namespace BridgingIT.DevKit.Application.Queueing;

using BridgingIT.DevKit.Common;

/// <summary>
/// Builds <see cref="QueueingOptions"/> instances.
/// </summary>
/// <example><code>var options = new QueueingOptionsBuilder().StartupDelayInvariant("500ms").Build();</code></example>
public class QueueingOptionsBuilder : OptionsBuilderBase<QueueingOptions, QueueingOptionsBuilder>
{
    /// <summary>
    /// Enables or disables queueing.
    /// </summary>
    /// <param name="value">The enabled value.</param>
    /// <returns>The current builder.</returns>
    public QueueingOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;
        return this;
    }

    /// <summary>
    /// Disables queueing.
    /// </summary>
    /// <returns>The current builder.</returns>
    public QueueingOptionsBuilder Disabled()
    {
        this.Target.Enabled = false;
        return this;
    }

    /// <summary>
    /// Sets the startup delay.
    /// </summary>
    /// <param name="timeSpan">The startup delay.</param>
    /// <returns>The current builder.</returns>
    public QueueingOptionsBuilder StartupDelay(TimeSpan timeSpan)
    {
        this.Target.StartupDelay = timeSpan;
        return this;
    }

    /// <summary>
    /// Sets the startup delay in milliseconds.
    /// </summary>
    /// <param name="milliseconds">The startup delay in milliseconds.</param>
    /// <returns>The current builder.</returns>
    public QueueingOptionsBuilder StartupDelay(int milliseconds)
    {
        this.Target.StartupDelay = TimeSpan.FromMilliseconds(milliseconds);
        return this;
    }

    /// <summary>
    /// Sets the startup delay from a string value.
    /// </summary>
    /// <param name="value">The startup delay value.</param>
    /// <returns>The current builder.</returns>
    public QueueingOptionsBuilder StartupDelay(string value)
    {
        if (TimeSpan.TryParse(value, out var timeSpan))
        {
            this.Target.StartupDelay = timeSpan;
        }

        return this;
    }

    /// <summary>Sets the startup delay from invariant TimeSpan text or a duration such as <c>500ms</c>.</summary>
    /// <param name="value">The duration text.</param>
    /// <returns>The current builder.</returns>
    /// <exception cref="ArgumentException">The value is not a supported invariant duration.</exception>
    /// <example><code>builder.StartupDelayInvariant("500ms");</code></example>
    public QueueingOptionsBuilder StartupDelayInvariant(string value)
    {
        if (!value.TryParseDuration(out var duration))
        {
            throw new ArgumentException("Supply a valid invariant duration.", nameof(value));
        }

        return this.StartupDelay(duration);
    }
}
