// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// One sanitized resource entry of an application inventory. It never contains credentials.
/// </summary>
/// <param name="ResourceId">The resource ID.</param>
/// <param name="Kind">The adapter kind.</param>
/// <param name="Target">The sanitized target, for example a database name or a relative path.</param>
/// <param name="Server">The sanitized server identity, when applicable.</param>
/// <param name="SourceKeys">The mapped configuration keys.</param>
/// <param name="Shared">Whether the resource is intentionally shared.</param>
/// <example>
/// <code>
/// var entry = new DevAppInstanceInventoryResource("forecasthub_core", "sqlserver", "forecasthub_core_w02", "localhost,14333", ["ConnectionStrings:Core"], false);
/// </code>
/// </example>
public sealed record DevAppInstanceInventoryResource(
    string ResourceId,
    string Kind,
    string Target,
    string Server,
    IReadOnlyList<string> SourceKeys,
    bool Shared);

/// <summary>
/// The sanitized resource inventory of one application instance.
/// </summary>
/// <param name="AppId">The application ID.</param>
/// <param name="WorktreeId">The worktree ID.</param>
/// <param name="Slot">The slot.</param>
/// <param name="ProjectPath">The project path relative to the repository root, using forward slashes.</param>
/// <param name="Resources">The resource entries.</param>
/// <example>
/// <code>
/// var inventory = DevAppInstanceInventory.Read(stateRoot);
/// </code>
/// </example>
public sealed record DevAppInstanceInventory(
    string AppId,
    Guid WorktreeId,
    int Slot,
    string ProjectPath,
    IReadOnlyList<DevAppInstanceInventoryResource> Resources)
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>
    /// Reads the inventory from an application state root.
    /// </summary>
    /// <param name="stateRoot">The state root.</param>
    /// <returns>The inventory, or <c>null</c> when absent.</returns>
    public static DevAppInstanceInventory Read(string stateRoot)
    {
        var path = DevAppInstancePaths.GetInventoryFile(stateRoot);
        var node = DevAppInstanceFiles.ReadJsonObject(path);
        if (node is null)
        {
            return null;
        }

        DevAppInstanceFiles.EnsureVersion(node, path);

        return node["inventory"]?.Deserialize<DevAppInstanceInventory>(JsonOptions);
    }

    /// <summary>
    /// Writes the inventory to an application state root.
    /// </summary>
    /// <param name="stateRoot">The state root.</param>
    public void Write(string stateRoot)
    {
        var node = new JsonObject
        {
            ["version"] = 1,
            ["inventory"] = JsonSerializer.SerializeToNode(this, JsonOptions),
        };
        DevAppInstanceFiles.WriteJsonAtomic(DevAppInstancePaths.GetInventoryFile(stateRoot), node);
    }
}

/// <summary>
/// One worktree assignment of the shared registry.
/// </summary>
/// <param name="WorktreeId">The worktree ID.</param>
/// <param name="Slot">The slot.</param>
/// <param name="Path">The canonical worktree path.</param>
/// <param name="AssignedAt">The assignment timestamp.</param>
/// <param name="Applications">The registry copies of the application inventories.</param>
/// <example>
/// <code>
/// var assignment = registry.FindByWorktreeId(worktreeId);
/// </code>
/// </example>
public sealed record DevAppInstanceAssignment(
    Guid WorktreeId,
    int Slot,
    string Path,
    DateTimeOffset AssignedAt,
    IReadOnlyList<DevAppInstanceInventory> Applications);

/// <summary>
/// The versioned shared registry stored at <c>&lt;commonDir&gt;/bdk-dev/instances.json</c>.
/// </summary>
/// <remarks>
/// The registry keeps unknown properties when it is rewritten. Callers hold the registry lock across load, change and save.
/// </remarks>
/// <example>
/// <code>
/// using (DevAppInstanceFiles.AcquireLock(lockPath, TimeSpan.FromSeconds(10)))
/// {
///     var registry = DevAppInstanceRegistry.Load(registryPath);
///     registry.RemoveAssignment(worktreeId);
///     registry.Save(registryPath);
/// }
/// </code>
/// </example>
public sealed class DevAppInstanceRegistry
{
    /// <summary>The supported registry version.</summary>
    public const int CurrentVersion = 1;

    private readonly JsonObject root;

    private DevAppInstanceRegistry(JsonObject root)
    {
        this.root = root;
    }

    /// <summary>Gets a value indicating whether the registry file existed when loaded.</summary>
    public bool Existed { get; private init; }

    /// <summary>Gets the recorded port policy, or <c>null</c> when none was recorded.</summary>
    public DevAppInstancePortPolicy PortPolicy =>
        this.root["portPolicy"] is JsonObject policy
            ? new DevAppInstancePortPolicy(
                policy["basePort"]?.GetValue<int>() ?? 5000,
                policy["blockSize"]?.GetValue<int>() ?? 100,
                policy["slotCount"]?.GetValue<int>() ?? 49)
            : null;

    /// <summary>Gets the recorded catalog applications.</summary>
    public IReadOnlyList<DevAppInstanceApplication> Applications =>
        this.ApplicationsArray.OfType<JsonObject>()
            .Select(a => new DevAppInstanceApplication(
                a["appId"]?.GetValue<string>(),
                a["httpOffset"]?.GetValue<int>() ?? 0,
                a["httpsOffset"]?.GetValue<int>() ?? 1)
            {
                Source = a["source"]?.GetValue<string>(),
            })
            .ToList();

    /// <summary>Gets the recorded worktree assignments.</summary>
    public IReadOnlyList<DevAppInstanceAssignment> Assignments =>
        this.AssignmentsArray.OfType<JsonObject>().Select(ToAssignment).ToList();

    private JsonArray ApplicationsArray => this.GetArray("applications");

    private JsonArray AssignmentsArray => this.GetArray("assignments");

    /// <summary>
    /// Loads the registry, or creates an empty one when the file does not exist.
    /// </summary>
    /// <param name="path">The registry path.</param>
    /// <returns>The registry.</returns>
    /// <exception cref="DevAppInstanceException">Thrown with <c>assignment_invalid</c> for invalid content.</exception>
    public static DevAppInstanceRegistry Load(string path)
    {
        var node = DevAppInstanceFiles.ReadJsonObject(path);
        if (node is null)
        {
            return new DevAppInstanceRegistry(new JsonObject { ["version"] = CurrentVersion }) { Existed = false };
        }

        DevAppInstanceFiles.EnsureVersion(node, path, CurrentVersion);
        var registry = new DevAppInstanceRegistry(node) { Existed = true };
        registry.Validate(path);

        return registry;
    }

    /// <summary>
    /// Saves the registry through an atomic replace.
    /// </summary>
    /// <param name="path">The registry path.</param>
    public void Save(string path) => DevAppInstanceFiles.WriteJsonAtomic(path, this.root);

    /// <summary>Records the port policy.</summary>
    /// <param name="policy">The policy.</param>
    public void SetPortPolicy(DevAppInstancePortPolicy policy) =>
        this.root["portPolicy"] = new JsonObject
        {
            ["basePort"] = policy.BasePort,
            ["blockSize"] = policy.BlockSize,
            ["slotCount"] = policy.SlotCount,
        };

    /// <summary>Records a catalog application.</summary>
    /// <param name="application">The application.</param>
    public void AddApplication(DevAppInstanceApplication application)
    {
        var entry = new JsonObject
        {
            ["appId"] = application.AppId,
            ["httpOffset"] = application.HttpOffset,
            ["httpsOffset"] = application.HttpsOffset,
        };
        if (application.Source is not null)
        {
            entry["source"] = application.Source;
        }

        this.ApplicationsArray.Add(entry);
    }

    /// <summary>Removes a recorded catalog application.</summary>
    /// <param name="appId">The application ID.</param>
    /// <returns><c>true</c> when an entry was removed.</returns>
    public bool RemoveApplication(string appId)
    {
        var node = this.ApplicationsArray.OfType<JsonObject>().FirstOrDefault(a => a["appId"]?.GetValue<string>() == appId);

        return node is not null && this.ApplicationsArray.Remove(node);
    }

    /// <summary>Finds an assignment by worktree ID.</summary>
    /// <param name="worktreeId">The worktree ID.</param>
    /// <returns>The assignment or <c>null</c>.</returns>
    public DevAppInstanceAssignment FindByWorktreeId(Guid worktreeId) =>
        this.Assignments.FirstOrDefault(a => a.WorktreeId == worktreeId);

    /// <summary>Finds an assignment by canonical path.</summary>
    /// <param name="path">The canonical path.</param>
    /// <returns>The assignment or <c>null</c>.</returns>
    public DevAppInstanceAssignment FindByPath(string path) =>
        this.Assignments.FirstOrDefault(a => GitCheckoutDetector.PathComparer.Equals(a.Path, path));

    /// <summary>Finds an assignment by slot.</summary>
    /// <param name="slot">The slot.</param>
    /// <returns>The assignment or <c>null</c>.</returns>
    public DevAppInstanceAssignment FindBySlot(int slot) => this.Assignments.FirstOrDefault(a => a.Slot == slot);

    /// <summary>Adds an assignment.</summary>
    /// <param name="worktreeId">The worktree ID.</param>
    /// <param name="slot">The slot.</param>
    /// <param name="path">The canonical path.</param>
    /// <param name="assignedAt">The timestamp.</param>
    public void AddAssignment(Guid worktreeId, int slot, string path, DateTimeOffset assignedAt) =>
        this.AssignmentsArray.Add(new JsonObject
        {
            ["worktreeId"] = worktreeId.ToString("D"),
            ["slot"] = slot,
            ["path"] = path,
            ["assignedAt"] = assignedAt.ToString("O"),
            ["applications"] = new JsonArray(),
        });

    /// <summary>Changes the slot of an assignment.</summary>
    /// <param name="worktreeId">The worktree ID.</param>
    /// <param name="slot">The new slot.</param>
    public void SetSlot(Guid worktreeId, int slot)
    {
        if (this.FindAssignmentNode(worktreeId) is { } node)
        {
            node["slot"] = slot;
        }
    }

    /// <summary>Removes an assignment.</summary>
    /// <param name="worktreeId">The worktree ID.</param>
    /// <returns><c>true</c> when an assignment was removed.</returns>
    public bool RemoveAssignment(Guid worktreeId)
    {
        var node = this.FindAssignmentNode(worktreeId);

        return node is not null && this.AssignmentsArray.Remove(node);
    }

    /// <summary>Replaces the registry copy of an application inventory.</summary>
    /// <param name="inventory">The inventory.</param>
    public void SetInventory(DevAppInstanceInventory inventory)
    {
        var node = this.FindAssignmentNode(inventory.WorktreeId);
        if (node is null)
        {
            return;
        }

        if (node["applications"] is not JsonArray apps)
        {
            apps = [];
            node["applications"] = apps;
        }

        var existing = apps.OfType<JsonObject>().FirstOrDefault(a => a["appId"]?.GetValue<string>() == inventory.AppId);
        if (existing is not null)
        {
            apps.Remove(existing);
        }

        apps.Add(JsonSerializer.SerializeToNode(inventory, DevAppInstanceInventory.JsonOptions));
    }

    /// <summary>Removes the registry copy of an application inventory.</summary>
    /// <param name="worktreeId">The worktree ID.</param>
    /// <param name="appId">The application ID.</param>
    public void RemoveInventory(Guid worktreeId, string appId)
    {
        if (this.FindAssignmentNode(worktreeId)?["applications"] is JsonArray apps &&
            apps.OfType<JsonObject>().FirstOrDefault(a => a["appId"]?.GetValue<string>() == appId) is { } existing)
        {
            apps.Remove(existing);
        }
    }

    private static DevAppInstanceAssignment ToAssignment(JsonObject node)
    {
        var apps = node["applications"] is JsonArray array
            ? array.OfType<JsonObject>().Select(a => a.Deserialize<DevAppInstanceInventory>(DevAppInstanceInventory.JsonOptions)).ToList()
            : [];

        return new DevAppInstanceAssignment(
            Guid.TryParse(node["worktreeId"]?.GetValue<string>(), out var id) ? id : Guid.Empty,
            node["slot"]?.GetValue<int>() ?? 0,
            node["path"]?.GetValue<string>(),
            DateTimeOffset.TryParse(node["assignedAt"]?.GetValue<string>(), out var at) ? at : default,
            apps);
    }

    private JsonObject FindAssignmentNode(Guid worktreeId) =>
        this.AssignmentsArray.OfType<JsonObject>()
            .FirstOrDefault(a => Guid.TryParse(a["worktreeId"]?.GetValue<string>(), out var id) && id == worktreeId);

    private JsonArray GetArray(string name)
    {
        if (this.root[name] is not JsonArray array)
        {
            array = [];
            this.root[name] = array;
        }

        return array;
    }

    private void Validate(string path)
    {
        List<DevAppInstanceAssignment> assignments;
        try
        {
            assignments = [.. this.Assignments];
            _ = this.Applications;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            throw Invalid(path, "contains malformed entries", ex);
        }

        if (assignments.Any(a => a.WorktreeId == Guid.Empty || a.Slot < 1 || string.IsNullOrWhiteSpace(a.Path)))
        {
            throw Invalid(path, "contains an assignment without a worktree ID, slot or path");
        }

        if (assignments.GroupBy(a => a.Slot).Any(g => g.Count() > 1))
        {
            throw Invalid(path, "contains duplicate slots");
        }

        if (assignments.GroupBy(a => a.WorktreeId).Any(g => g.Count() > 1))
        {
            throw Invalid(path, "contains duplicate worktree IDs");
        }

        if (assignments.GroupBy(a => a.Path, GitCheckoutDetector.PathComparer).Any(g => g.Count() > 1))
        {
            throw Invalid(path, "contains duplicate worktree paths");
        }
    }

    private static DevAppInstanceException Invalid(string path, string problem, Exception inner = null) =>
        new(
            DevAppInstanceErrorCodes.AssignmentInvalid,
            $"The registry '{path}' {problem}.",
            "Inspect the registry with bdk dev status and correct or restore it. Startup does not rewrite an invalid registry.",
            inner);
}
