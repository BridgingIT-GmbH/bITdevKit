namespace BridgingIT.DevKit.Cli;

using System.Text.Json;

/// <summary>
/// Stores and reads workspace-scoped host selections.
/// </summary>
/// <param name="options">The host registry options.</param>
public sealed class HostSelectionStore(HostRegistryOptions options)
{
    /// <summary>
    /// Gets the selection file path for a workspace.
    /// </summary>
    /// <param name="workspace">The workspace context.</param>
    /// <returns>The selection file path.</returns>
    public string GetSelectionPath(CliWorkspaceContext workspace)
        => Path.Combine(options.SelectionPath, workspace.Hash + ".json");

    /// <summary>
    /// Reads the selected runtime id for a workspace.
    /// </summary>
    /// <param name="workspace">The workspace context.</param>
    /// <returns>The selected runtime id, or <see langword="null" /> when no selection exists.</returns>
    public string Read(CliWorkspaceContext workspace)
    {
        var selectionPath = this.GetSelectionPath(workspace);
        if (!File.Exists(selectionPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(selectionPath);
            var selection = JsonSerializer.Deserialize<HostSelection>(json, CliJson.Options);
            return selection?.RuntimeId;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes the selected runtime id for a workspace.
    /// </summary>
    /// <param name="workspace">The workspace context.</param>
    /// <param name="runtimeId">The runtime id to select.</param>
    /// <returns>The selection file path.</returns>
    public string Write(CliWorkspaceContext workspace, string runtimeId)
    {
        Directory.CreateDirectory(options.SelectionPath);
        var selectionPath = this.GetSelectionPath(workspace);
        var selection = new HostSelection(runtimeId, workspace.Path, DateTimeOffset.UtcNow);
        File.WriteAllText(selectionPath, JsonSerializer.Serialize(selection, CliJson.Options));
        return selectionPath;
    }

    /// <summary>
    /// Deletes the selected runtime id for a workspace.
    /// </summary>
    /// <param name="workspace">The workspace context.</param>
    public void Delete(CliWorkspaceContext workspace)
    {
        var selectionPath = this.GetSelectionPath(workspace);
        try
        {
            if (File.Exists(selectionPath))
            {
                File.Delete(selectionPath);
            }
        }
        catch (IOException)
        {
            // best-effort selection cleanup
        }
        catch (UnauthorizedAccessException)
        {
            // best-effort selection cleanup
        }
    }

    private sealed record HostSelection(string RuntimeId, string WorkspacePath, DateTimeOffset SelectedAt);
}
