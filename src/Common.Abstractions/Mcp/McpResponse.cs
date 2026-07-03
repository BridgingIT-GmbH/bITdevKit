namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the common agent-oriented MCP response envelope.
/// </summary>
/// <example>
/// <code>
/// var response = McpResponse.Success("Found 3 recent errors.", new { count = 3 });
/// </code>
/// </example>
public sealed record McpResponse
{
    /// <summary>
    /// Gets or initializes a value indicating whether the requested data or operation is available.
    /// </summary>
    public bool Available { get; init; }

    /// <summary>
    /// Gets or initializes the stable error code when unavailable or failed.
    /// </summary>
    public string Code { get; init; }

    /// <summary>
    /// Gets or initializes the short human-readable summary.
    /// </summary>
    public string Summary { get; init; }

    /// <summary>
    /// Gets or initializes the detailed unavailable or failure reason.
    /// </summary>
    public string Reason { get; init; }

    /// <summary>
    /// Gets or initializes structured bounded response data.
    /// </summary>
    public object Data { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the response was truncated.
    /// </summary>
    public bool Truncated { get; init; }

    /// <summary>
    /// Gets or initializes suggested next MCP calls.
    /// </summary>
    public IReadOnlyList<McpNextCall> Next { get; init; } = [];

    /// <summary>
    /// Creates an available response.
    /// </summary>
    /// <param name="summary">The response summary.</param>
    /// <param name="data">The structured response data.</param>
    /// <param name="truncated">A value indicating whether the response was truncated.</param>
    /// <param name="next">Suggested next calls.</param>
    /// <returns>The response.</returns>
    public static McpResponse Success(string summary, object data = null, bool truncated = false, IReadOnlyList<McpNextCall> next = null)
        => new() { Available = true, Summary = summary, Data = data ?? new { }, Truncated = truncated, Next = next ?? [] };

    /// <summary>
    /// Creates an unavailable response.
    /// </summary>
    /// <param name="code">The stable unavailable code.</param>
    /// <param name="summary">The summary.</param>
    /// <param name="reason">The detailed reason.</param>
    /// <param name="next">Suggested next calls.</param>
    /// <returns>The response.</returns>
    public static McpResponse Unavailable(string code, string summary, string reason = null, IReadOnlyList<McpNextCall> next = null)
        => new() { Available = false, Code = code, Summary = summary, Reason = reason, Data = new { }, Next = next ?? [] };

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    /// <param name="code">The stable failure code.</param>
    /// <param name="summary">The failure summary.</param>
    /// <param name="reason">The sanitized reason.</param>
    /// <param name="next">Suggested next calls.</param>
    /// <returns>The response.</returns>
    public static McpResponse Failure(string code, string summary, string reason = null, IReadOnlyList<McpNextCall> next = null)
        => Unavailable(code, summary, reason, next);
}
