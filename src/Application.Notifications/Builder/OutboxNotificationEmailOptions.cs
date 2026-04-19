namespace BridgingIT.DevKit.Application.Notifications;

using System;
using BridgingIT.DevKit.Common;

/// <summary>
/// Configures background outbox processing for notification emails.
/// </summary>
/// <example>
/// <code>
/// builder.Options.OutboxOptions = new OutboxNotificationEmailOptions
/// {
///     ProcessingMode = OutboxNotificationEmailProcessingMode.Immediate,
///     ProcessingCount = 50,
///     LeaseDuration = TimeSpan.FromMinutes(2)
/// };
/// </code>
/// </example>
public class OutboxNotificationEmailOptions : OptionsBase
{
    /// <summary>
    /// Gets or sets a value indicating whether outbox processing is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the delay applied before background processing starts.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets the interval between background polling cycles.
    /// </summary>
    public TimeSpan ProcessingInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the delay applied immediately before processing a batch.
    /// </summary>
    public TimeSpan ProcessingDelay { get; set; } = TimeSpan.FromMilliseconds(0);

    /// <summary>
    /// Gets or sets the random jitter added to <see cref="ProcessingDelay" />.
    /// </summary>
    public TimeSpan ProcessingJitter { get; set; } = TimeSpan.FromMilliseconds(0);

    /// <summary>
    /// Gets or sets the outbox processing strategy.
    /// </summary>
    public OutboxNotificationEmailProcessingMode ProcessingMode { get; set; } = OutboxNotificationEmailProcessingMode.Interval;

    /// <summary>
    /// Gets or sets a value indicating whether processed rows should be purged on startup.
    /// </summary>
    public bool PurgeProcessedOnStartup { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether all rows should be purged on startup.
    /// </summary>
    public bool PurgeOnStartup { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether queued messages should be persisted automatically.
    /// </summary>
    public bool AutoSave { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of messages to claim in one processing batch.
    /// </summary>
    public int ProcessingCount { get; set; } = 100;

    /// <summary>
    /// Gets or sets the retry limit for a single message.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the duration of a processing lease claimed by a worker.
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(5);
}
