namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Builds <see cref="QueueingOptions"/> instances.
/// </summary>
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
}
