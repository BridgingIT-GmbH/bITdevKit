// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Configures outbox message processing.
/// </summary>
public class OutboxMessageOptions : OptionsBase
{
    /// <summary>Gets or sets whether outbox processing is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the delay before processing starts.</summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Gets or sets the interval between processing cycles.</summary>
    public TimeSpan ProcessingInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the delay applied before processing a message.</summary>
    public TimeSpan ProcessingDelay { get; set; } = TimeSpan.FromMilliseconds(0);

    /// <summary>Gets or sets the random jitter applied to processing delays.</summary>
    public TimeSpan ProcessingJitter { get; set; } = TimeSpan.FromMilliseconds(0);

    /// <summary>Gets or sets the outbox processing mode.</summary>
    public OutboxMessageProcessingMode ProcessingMode { get; set; }

    /// <summary>Gets or sets whether processed messages are purged at startup.</summary>
    public bool PurgeProcessedOnStartup { get; set; }

    /// <summary>Gets or sets whether all outbox messages are purged at startup.</summary>
    public bool PurgeOnStartup { get; set; }

    /// <summary>Gets or sets the serializer used for outbox payloads.</summary>
    public ISerializer Serializer { get; set; }

    /// <summary>Gets or sets whether messages are saved automatically.</summary>
    public bool AutoSave { get; set; } = true;

    /// <summary>Gets or sets the maximum number of messages processed per cycle.</summary>
    public int ProcessingCount { get; set; } = int.MaxValue; // worker Take each interval

    /// <summary>Gets or sets the retry count for each message.</summary>
    public int RetryCount { get; set; } = 3; // worker retry for each domain event processing
}
