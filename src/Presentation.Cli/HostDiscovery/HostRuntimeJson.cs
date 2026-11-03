namespace BridgingIT.DevKit.Cli;

/// <summary>
/// Maps host runtime information to JSON output models.
/// </summary>
public static class HostRuntimeJson
{
    /// <summary>
    /// Maps a host runtime to the shared JSON output shape.
    /// </summary>
    /// <param name="host">The host runtime.</param>
    /// <returns>The JSON output model.</returns>
    public static object Map(HostRuntimeInfo host)
        => new
        {
            host.Descriptor?.RuntimeId,
            host.Descriptor?.ApplicationName,
            host.Descriptor?.EnvironmentName,
            status = host.Status.ToString(),
            url = host.GetPrimaryUrl(),
            urls = host.Descriptor?.Urls ?? [],
            features = host.Descriptor?.Features?.Keys.ToArray() ?? [],
            host.Descriptor?.ProcessId,
            host.Descriptor?.StartedAt,
            host.Descriptor?.WorkspacePath,
            devAppInstance = host.Descriptor?.DevAppInstance is { } instance
                ? new { instance.AppId, instance.Slot, instance.WorktreeId, instance.WorktreeRoot, instance.HttpsUrl, instance.HttpUrl }
                : null,
            reason = host.Reason
        };
}
