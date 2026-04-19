namespace BridgingIT.DevKit.Application.Notifications;

/// <summary>
/// Determines how the notification email outbox worker is triggered.
/// </summary>
public enum OutboxNotificationEmailProcessingMode
{
    /// <summary>
    /// Processes queued mail on the configured background interval.
    /// </summary>
    Interval = 0,

    /// <summary>
    /// Triggers processing immediately when new mail is queued.
    /// </summary>
    Immediate = 1
}
