// File: BridgingIT.DevKit.Application.FileMonitoring/FileEventType.cs
namespace BridgingIT.DevKit.Application.FileMonitoring;

/// <summary>
/// Represents the type of file event that occurred in the monitored storage location.
/// Used by processors to determine the nature of the change (e.g., Added, Changed, Deleted).
/// </summary>
public enum FileEventType
{
    /// <summary>
    /// Indicates a file was newly created or added to the monitored location.
    /// </summary>
    Added,

    /// <summary>
    /// Indicates an existing file was modified (e.g., content or metadata changed).
    /// </summary>
    Changed,

    /// <summary>
    /// Indicates a file was removed from the monitored location.
    /// </summary>
    Deleted
}