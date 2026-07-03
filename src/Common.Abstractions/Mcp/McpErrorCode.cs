namespace BridgingIT.DevKit.Common;

/// <summary>
/// Defines stable MCP error and unavailable reason codes.
/// </summary>
/// <example>
/// <code>
/// var response = McpResponse.Unavailable(McpErrorCode.NoRuntimeFound, "No runtime was found.");
/// </code>
/// </example>
public static class McpErrorCode
{
    /// <summary>
    /// No matching runtime was found.
    /// </summary>
    public const string NoRuntimeFound = "no_runtime_found";

    /// <summary>
    /// More than one runtime requires explicit selection.
    /// </summary>
    public const string RuntimeSelectionRequired = "runtime_selection_required";

    /// <summary>
    /// The selected runtime cannot be used.
    /// </summary>
    public const string SelectedRuntimeUnavailable = "selected_runtime_unavailable";

    /// <summary>
    /// The protocol version is unsupported.
    /// </summary>
    public const string VersionMismatch = "version_mismatch";

    /// <summary>
    /// The feature or operation is unavailable.
    /// </summary>
    public const string FeatureUnavailable = "feature_unavailable";

    /// <summary>
    /// The requested toolset is disabled.
    /// </summary>
    public const string UnauthorizedToolset = "unauthorized_toolset";

    /// <summary>
    /// The request timed out.
    /// </summary>
    public const string Timeout = "timeout";

    /// <summary>
    /// The operation failed.
    /// </summary>
    public const string OperationFailed = "operation_failed";

    /// <summary>
    /// Documentation could not be read.
    /// </summary>
    public const string DocumentationUnavailable = "documentation_unavailable";
}
