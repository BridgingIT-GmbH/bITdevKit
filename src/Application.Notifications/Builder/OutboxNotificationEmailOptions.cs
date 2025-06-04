namespace BridgingIT.DevKit.Application.Notifications;

using System;
using BridgingIT.DevKit.Common;

public class OutboxNotificationEmailOptions : OptionsBase
{
    public bool Enabled { get; set; } = true;

    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(15);

    public TimeSpan ProcessingInterval { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan ProcessingDelay { get; set; } = TimeSpan.FromMilliseconds(1);

    public OutboxNotificationEmailProcessingMode ProcessingMode { get; set; } = OutboxNotificationEmailProcessingMode.Interval;

    public bool PurgeProcessedOnStartup { get; set; }

    public bool PurgeOnStartup { get; set; }

    public bool AutoSave { get; set; } = true;

    public int ProcessingCount { get; set; } = 100;

    public int RetryCount { get; set; } = 3;
}