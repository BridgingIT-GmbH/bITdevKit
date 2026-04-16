namespace BridgingIT.DevKit.Application.Queueing;

using BridgingIT.DevKit.Common;

/// <summary>
/// Configures the queueing feature runtime.
/// </summary>
/// <example>
/// <code>
/// services.AddQueueing(options =>
/// {
///     options.Options.Enabled = true;
///     options.Options.StartupDelay = TimeSpan.FromSeconds(5);
/// });
/// </code>
/// </example>
public class QueueingOptions : OptionsBase
{
    /// <summary>
    /// Gets or sets a value indicating whether queueing is enabled.
    /// </summary>
    /// <remarks>
    /// Disable this when you want queue producers and workers to remain registered but inactive for the current host.
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the startup delay before subscriptions are applied to the broker.
    /// </summary>
    /// <remarks>
    /// This is useful when the selected broker depends on infrastructure that becomes ready shortly after host startup.
    /// </remarks>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.Zero;
}
