namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Cleans the current host descriptor during graceful shutdown.
/// </summary>
/// <example>
/// <code>
/// cleanup.RemoveCurrentDescriptor();
/// </code>
/// </example>
public sealed class HostDescriptorCleanupService(HostRuntimeDescriptorWriter writer)
{
    /// <summary>
    /// Removes the current host descriptor if it exists.
    /// </summary>
    public void RemoveCurrentDescriptor()
        => writer.Remove();
}