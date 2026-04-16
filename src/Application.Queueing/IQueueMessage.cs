namespace BridgingIT.DevKit.Application.Queueing;

using FluentValidation.Results;

/// <summary>
/// Represents a unit of queued work.
/// </summary>
/// <example>
/// <code>
/// public sealed class GenerateInvoiceQueueMessage : QueueMessageBase
/// {
///     public Guid InvoiceId { get; init; }
/// }
/// </code>
/// </example>
public interface IQueueMessage
{
    /// <summary>
    /// Gets the logical message identifier.
    /// </summary>
    string MessageId { get; }

    /// <summary>
    /// Gets the timestamp when the message was created.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets arbitrary metadata associated with the queued message.
    /// </summary>
    IDictionary<string, object> Properties { get; }

    /// <summary>
    /// Validates the message before enqueue.
    /// </summary>
    /// <returns>The validation result.</returns>
    ValidationResult Validate();
}
