// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Outbox;

/// <summary>
/// Configures background outbox processing for persisted domain events.
/// </summary>
/// <example>
/// <code>
/// var options = new OutboxDomainEventOptions
/// {
///     ProcessingMode = OutboxDomainEventProcessMode.Immediate,
///     ProcessingCount = 50,
///     LeaseDuration = TimeSpan.FromSeconds(30),
///     LeaseRenewalInterval = TimeSpan.FromSeconds(10),
///     AutoArchiveAfter = TimeSpan.FromHours(1)
/// };
/// </code>
/// </example>
public class OutboxDomainEventOptions : OptionsBase
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
    public OutboxDomainEventProcessMode ProcessingMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether all rows should be purged on startup.
    /// </summary>
    public bool PurgeOnStartup { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether processed rows should be purged on startup.
    /// </summary>
    public bool PurgeProcessedOnStartup { get; set; }

    /// <summary>
    /// Gets or sets the serializer used to persist and restore domain events.
    /// </summary>
    public ISerializer Serializer { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether queued domain events should be persisted immediately.
    /// </summary>
    public bool AutoSave { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of domain events to claim in one processing batch.
    /// </summary>
    public int ProcessingCount { get; set; } = int.MaxValue;

    /// <summary>
    /// Gets or sets the retry limit for a single domain event.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the duration of a processing lease claimed by a worker.
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how often an active worker should renew its lease while processing.
    /// </summary>
    public TimeSpan LeaseRenewalInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets the age after which processed domain events may be archived automatically.
    /// </summary>
    public TimeSpan? AutoArchiveAfter { get; set; }
}
