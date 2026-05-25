namespace BridgingIT.DevKit.Presentation.Web.Orchestrations.Models;

using System.Text.Json;

/// <summary>
/// Represents the request body used to deliver a signal to an orchestration instance.
/// </summary>
public class OrchestrationSignalRequest
{
    /// <summary>
    /// Gets or sets the signal name.
    /// </summary>
    public string SignalName { get; set; }

    /// <summary>
    /// Gets or sets the optional signal payload.
    /// </summary>
    public JsonElement? Payload { get; set; }

    /// <summary>
    /// Gets or sets the optional idempotency key.
    /// </summary>
    public string IdempotencyKey { get; set; }
}