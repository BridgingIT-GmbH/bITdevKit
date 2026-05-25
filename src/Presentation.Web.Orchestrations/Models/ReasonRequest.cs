namespace BridgingIT.DevKit.Presentation.Web.Orchestrations.Models;

/// <summary>
/// Represents a request body containing an optional reason.
/// </summary>
public class ReasonRequest
{
    /// <summary>
    /// Gets or sets the optional reason text.
    /// </summary>
    public string Reason { get; set; }
}