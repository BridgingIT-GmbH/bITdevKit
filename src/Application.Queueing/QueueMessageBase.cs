namespace BridgingIT.DevKit.Application.Queueing;

using System.Diagnostics;
using FluentValidation.Results;

[DebuggerDisplay("Id={MessageId}")]
/// <summary>
/// Provides a reusable base class for queue messages.
/// </summary>
public abstract class QueueMessageBase : IQueueMessage
{
    /// <summary>
    /// Gets the logical message identifier.
    /// </summary>
    public string MessageId { get; init; } = GuidGenerator.CreateSequential().ToString("N");

    /// <summary>
    /// Gets the timestamp when the message instance was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets message metadata that flows with the queued item.
    /// </summary>
    public IDictionary<string, object> Properties { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Validates the message.
    /// </summary>
    /// <returns>An empty successful validation result by default.</returns>
    public virtual ValidationResult Validate()
    {
        return new ValidationResult();
    }
}
