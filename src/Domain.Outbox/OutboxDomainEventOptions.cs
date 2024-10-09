// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Outbox;

public class OutboxDomainEventOptions : OptionsBase
{
    public bool Enabled { get; set; } = true;

    public TimeSpan StartupDelay { get; set; } = new(0, 0, 15);

    public TimeSpan ProcessingInterval { get; set; } = new(0, 0, 30);

    public TimeSpan ProcessingDelay { get; set; } = new(0, 0, 0, 0, 1);

    public OutboxDomainEventProcessMode ProcessingMode { get; set; }

    public bool PurgeOnStartup { get; set; }

    public bool PurgeProcessedOnStartup { get; set; }

    public ISerializer Serializer { get; set; }

    public bool AutoSave { get; set; } = true;

    public int ProcessingCount { get; set; } = int.MaxValue; // worker domain events to process each interval

    public int RetryCount { get; set; } = 3; // worker retry for each domain event processing
}