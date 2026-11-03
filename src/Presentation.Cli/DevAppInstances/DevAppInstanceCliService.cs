namespace BridgingIT.DevKit.Cli;

using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>
/// The outcome of one <c>bdk dev</c> operation.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="Code">The stable error code, or <c>null</c> on success.</param>
/// <param name="Summary">The one-line summary.</param>
/// <param name="Data">The machine-readable payload for <c>--json</c>.</param>
/// <param name="Lines">The human-readable detail lines.</param>
public sealed record DevAppInstanceCliOutcome(CliExitCode ExitCode, string Code, string Summary, object Data, IReadOnlyList<string> Lines)
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Succeeded => this.ExitCode == CliExitCode.Success;

    internal static DevAppInstanceCliOutcome Ok(string summary, object data = null, IReadOnlyList<string> lines = null) =>
        new(CliExitCode.Success, null, summary, data, lines ?? []);

    internal static DevAppInstanceCliOutcome Fail(CliExitCode exitCode, string code, string summary, object data = null, IReadOnlyList<string> lines = null) =>
        new(exitCode, code, summary, data, lines ?? []);
}

/// <summary>
/// The verified process state of one application instance.
/// </summary>
/// <param name="State">The state: <c>running</c> while the application lease is held, otherwise <c>stopped</c>.</param>
/// <param name="ProcessId">The lease holder's process ID, when known.</param>
/// <param name="RuntimeId">The runtime descriptor ID, when a descriptor matches the holder's process ID and start time.</param>
/// <param name="LeaseHeld">A value indicating whether the application lease is held.</param>
public sealed record DevAppInstanceProcessStatus(string State, int? ProcessId, string RuntimeId, bool LeaseHeld)
{
    /// <summary>Gets a value indicating whether the instance runs.</summary>
    public bool IsRunning => this.State == "running";
}

/// <summary>
/// Implements the <c>bdk dev</c> management operations over the shared registry, the local assignment, the inventories
/// and the host runtime descriptors.
/// </summary>
/// <remarks>
/// Every registry change happens under the registry lock. The service never deletes databases itself: reset and prune
/// start the application project with the reserved reset arguments so the host's own adapters do the work.
/// </remarks>
/// <example>
/// <code>
/// var outcome = service.GetStatus(workspace);
/// Console.WriteLine(outcome.Summary);
/// </code>
/// </example>
public sealed class DevAppInstanceCliService(
    HostRuntimeDiscovery discovery,
    IHostProcessManager processManager,
    IDevAppInstanceProcessRunner processRunner,
    IGitCommandRunner gitRunner)
{
    /// <summary>The status schema version.</summary>
    public const int StatusSchemaVersion = 1;

    internal const string ResultPrefix = "[BDK-WORKTREE-RESULT] ";

    private static readonly JsonSerializerOptions ResultOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Gets the registry lock timeout.</summary>
    public TimeSpan LockTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets the graceful stop timeout.</summary>
    public TimeSpan StopTimeout { get; init; } = TimeSpan.FromSeconds(45);

    /// <summary>Gets the time <c>bdk dev run</c> waits for its hosts to stop before it terminates them.</summary>
    public TimeSpan RunShutdownTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets the clock.</summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    /// <summary>
    /// Reports the assignment of the current worktree, the catalog applications and their verified process state.
    /// </summary>
    /// <param name="workspace">The CLI workspace.</param>
    /// <returns>The outcome.</returns>
    public DevAppInstanceCliOutcome GetStatus(CliWorkspaceContext workspace)
    {
        var checkout = this.Detect(workspace.Path);
        if (checkout.Kind == GitCheckoutKind.OutsideGit)
        {
            return OutsideGit(workspace.Path, checkout.Warning);
        }

        var registry = this.LoadSnapshot(checkout.CommonDirectory);
        var policy = registry.PortPolicy ?? DevAppInstancePortPolicy.Default;
        var current = checkout.IsLinkedWorktree ? FindCurrent(registry, checkout) : null;
        var hosts = discovery.Discover(workspace, includeAll: true);
        var mode = checkout.IsLinkedWorktree ? current is null ? "unassigned" : "managed" : "ordinary";

        var applications = registry.Applications.Select(app =>
        {
            var inventory = current?.Applications.FirstOrDefault(i => string.Equals(i.AppId, app.AppId, StringComparison.Ordinal));
            var process = current is null ? null : this.GetProcessStatus(checkout.CommonDirectory, current.WorktreeId, app.AppId, hosts);

            return new
            {
                appId = app.AppId,
                httpOffset = app.HttpOffset,
                httpsOffset = app.HttpsOffset,
                httpUrl = current is null ? null : $"http://localhost:{policy.GetPort(current.Slot, app.HttpOffset)}",
                httpsUrl = current is null ? null : $"https://localhost:{policy.GetPort(current.Slot, app.HttpsOffset)}",
                instanceToken = current is null ? null : DevAppInstanceArguments.FormatInstanceToken(app.AppId, current.Slot),
                projectPath = inventory?.ProjectPath,
                resources = (inventory?.Resources ?? []).Select(r => new { resourceId = r.ResourceId, kind = r.Kind, target = r.Target, server = r.Server, shared = r.Shared }).ToList(),
                process = process is null ? null : new { state = process.State, processId = process.ProcessId, runtimeId = process.RuntimeId, leaseHeld = process.LeaseHeld },
            };
        }).ToList();

        var assignments = registry.Assignments.Select(a => new
        {
            worktreeId = a.WorktreeId,
            slot = a.Slot,
            path = a.Path,
            assignedAt = a.AssignedAt,
            pathExists = Directory.Exists(a.Path),
            current = current is not null && a.WorktreeId == current.WorktreeId,
            applications = a.Applications.Select(i => i.AppId).ToList(),
        }).OrderBy(a => a.slot).ToList();

        var data = new
        {
            schemaVersion = StatusSchemaVersion,
            mode,
            reason = checkout.Kind switch
            {
                GitCheckoutKind.LinkedWorktree => current is null ? "linked worktree without an assignment; the next host start assigns a slot" : "linked worktree",
                _ => "ordinary checkout; hosts keep their baseline settings",
            },
            worktreeRoot = checkout.WorktreeRoot,
            registryPath = DevAppInstancePaths.GetRegistryFile(checkout.CommonDirectory),
            portPolicy = new { basePort = policy.BasePort, blockSize = policy.BlockSize, slotCount = policy.SlotCount },
            worktreeId = current?.WorktreeId,
            slot = current?.Slot,
            applications,
            assignments,
        };

        var lines = new List<string>
        {
            $"Worktree:  {checkout.WorktreeRoot}",
            $"Mode:      {mode}{(current is null ? string.Empty : $" (slot {current.Slot}, worktree {current.WorktreeId})")}",
        };
        foreach (var app in applications)
        {
            lines.Add($"App:       {app.appId} {app.httpsUrl ?? "(no slot)"} {app.process?.state ?? string.Empty}".TrimEnd());
            foreach (var resource in app.resources.Where(r => !r.shared))
            {
                lines.Add($"           {resource.kind} {resource.resourceId}: {resource.target}");
            }
        }

        lines.Add($"Slots:     {assignments.Count} assigned ({string.Join(", ", assignments.Select(a => $"{a.slot}{(a.current ? "*" : string.Empty)}{(a.pathExists ? string.Empty : " missing")}"))})");

        return DevAppInstanceCliOutcome.Ok($"{mode} ({checkout.WorktreeRoot})", data, lines);
    }

    /// <summary>
    /// Reserves a slot for the current linked worktree, or moves an idle assignment without resources to another slot.
    /// </summary>
    /// <param name="workspace">The CLI workspace.</param>
    /// <param name="slot">The requested slot.</param>
    /// <returns>The outcome.</returns>
    public DevAppInstanceCliOutcome ConfigureSlot(CliWorkspaceContext workspace, int slot)
    {
        var checkout = this.Detect(workspace.Path);
        if (!checkout.IsLinkedWorktree)
        {
            return NotLinked(checkout, workspace.Path);
        }

        using (this.Lock(checkout.CommonDirectory))
        {
            var registryPath = DevAppInstancePaths.GetRegistryFile(checkout.CommonDirectory);
            var registry = DevAppInstanceRegistry.Load(registryPath);
            var policy = registry.PortPolicy ?? DevAppInstancePortPolicy.Default;
            if (slot < 1 || slot > policy.SlotCount)
            {
                return DevAppInstanceCliOutcome.Fail(CliExitCode.InvalidArguments, "invalid_arguments", $"Slot must be within 1..{policy.SlotCount}.");
            }

            var current = FindCurrent(registry, checkout);
            if (current?.Slot == slot)
            {
                new DevAppInstanceLocalAssignment(current.WorktreeId, slot).Write(checkout.WorktreeRoot);

                return DevAppInstanceCliOutcome.Ok($"Slot {slot} is already assigned to this worktree.", new { worktreeId = current.WorktreeId, slot, changed = false });
            }

            if (registry.FindBySlot(slot) is { } owner)
            {
                return DevAppInstanceCliOutcome.Fail(CliExitCode.CommandFailed, DevAppInstanceErrorCodes.SlotInUse, $"Slot {slot} belongs to '{owner.Path}'.");
            }

            if (current is not null)
            {
                var blockers = this.GetBlockers(registry, checkout.CommonDirectory, current);
                if (blockers is not null)
                {
                    return blockers;
                }

                registry.SetSlot(current.WorktreeId, slot);
                foreach (var inventory in current.Applications)
                {
                    registry.RemoveInventory(current.WorktreeId, inventory.AppId);
                }

                registry.Save(registryPath);
                new DevAppInstanceLocalAssignment(current.WorktreeId, slot).Write(checkout.WorktreeRoot);

                return DevAppInstanceCliOutcome.Ok($"Moved this worktree from slot {current.Slot} to slot {slot}.", new { worktreeId = current.WorktreeId, slot, previousSlot = current.Slot, changed = true });
            }

            var worktreeId = Guid.NewGuid();
            registry.AddAssignment(worktreeId, slot, checkout.WorktreeRoot, this.TimeProvider.GetUtcNow());
            registry.Save(registryPath);
            new DevAppInstanceLocalAssignment(worktreeId, slot).Write(checkout.WorktreeRoot);

            return DevAppInstanceCliOutcome.Ok($"Reserved slot {slot} for this worktree.", new { worktreeId, slot, changed = true });
        }
    }

    /// <summary>
    /// Removes an application's recorded catalog entry so its next start records a changed declaration.
    /// </summary>
    /// <param name="workspace">The CLI workspace.</param>
    /// <param name="appId">The application ID.</param>
    /// <returns>The outcome.</returns>
    public DevAppInstanceCliOutcome ForgetApplication(CliWorkspaceContext workspace, string appId)
    {
        var checkout = this.Detect(workspace.Path);
        if (checkout.Kind == GitCheckoutKind.OutsideGit)
        {
            return OutsideGit(workspace.Path, checkout.Warning);
        }

        using (this.Lock(checkout.CommonDirectory))
        {
            var registryPath = DevAppInstancePaths.GetRegistryFile(checkout.CommonDirectory);
            var registry = DevAppInstanceRegistry.Load(registryPath);
            if (!registry.Applications.Any(a => string.Equals(a.AppId, appId, StringComparison.Ordinal)))
            {
                return DevAppInstanceCliOutcome.Fail(CliExitCode.CommandFailed, DevAppInstanceInactiveReasons.NotRegistered, $"Application '{appId}' is not in the catalog.");
            }

            var referenced = registry.Assignments
                .Where(a => a.Applications.Any(i => i.AppId == appId && i.Resources.Any(r => !r.Shared)))
                .ToList();
            if (referenced.Count > 0)
            {
                return DevAppInstanceCliOutcome.Fail(
                    CliExitCode.CommandFailed,
                    "resources_remaining",
                    $"Application '{appId}' still has instance resources in slot(s) {string.Join(", ", referenced.Select(a => a.Slot))}. Run 'bdk dev reset' in those worktrees, or 'bdk dev prune --confirm' for deleted ones, first.",
                    lines: referenced.Select(a => $"slot {a.Slot}: {a.Path}").ToList());
            }

            foreach (var assignment in registry.Assignments.Where(a => a.Applications.Any(i => i.AppId == appId)))
            {
                registry.RemoveInventory(assignment.WorktreeId, appId);
            }

            registry.RemoveApplication(appId);
            registry.Save(registryPath);

            return DevAppInstanceCliOutcome.Ok($"Removed '{appId}' from the catalog. Its next start records the current declaration.", new { appId, removed = true });
        }
    }

    /// <summary>
    /// Requests a graceful shutdown of a verified running instance and waits for its lease to be released.
    /// </summary>
    /// <param name="workspace">The CLI workspace.</param>
    /// <param name="appId">The application ID.</param>
    /// <param name="force">A value indicating whether the process is killed after the graceful timeout.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    public async Task<DevAppInstanceCliOutcome> StopAsync(CliWorkspaceContext workspace, string appId, bool force, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        var checkout = this.Detect(workspace.Path);
        if (!checkout.IsLinkedWorktree)
        {
            return NotLinked(checkout, workspace.Path);
        }

        var registry = this.LoadSnapshot(checkout.CommonDirectory);
        var current = FindCurrent(registry, checkout);
        if (current is null)
        {
            return NoAssignment();
        }

        var leasePath = DevAppInstancePaths.GetLeaseFile(checkout.CommonDirectory, current.WorktreeId, appId);
        var status = this.GetProcessStatus(checkout.CommonDirectory, current.WorktreeId, appId, discovery.Discover(workspace, includeAll: true));
        if (!status.IsRunning)
        {
            return DevAppInstanceCliOutcome.Fail(CliExitCode.HostNotFound, "no_host_found", $"No running host of '{appId}' was found for this worktree.");
        }

        var stopFile = DevAppInstanceLease.GetStopRequestFile(leasePath);
        await File.WriteAllTextAsync(stopFile, $"{{\"requestedAt\":\"{this.TimeProvider.GetUtcNow():O}\"}}", cancellationToken);

        var stopTimeout = timeout ?? this.StopTimeout;
        var deadline = this.TimeProvider.GetUtcNow() + stopTimeout;
        while (DevAppInstanceLease.IsHeld(leasePath) && this.TimeProvider.GetUtcNow() < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        if (!DevAppInstanceLease.IsHeld(leasePath))
        {
            return DevAppInstanceCliOutcome.Ok($"Stopped '{appId}' (pid {status.ProcessId}).", new { appId, processId = status.ProcessId, stopped = true, forced = false });
        }

        TryDelete(stopFile);
        if (!force)
        {
            return DevAppInstanceCliOutcome.Fail(
                CliExitCode.CommandFailed,
                "stop_timeout",
                $"'{appId}' (pid {status.ProcessId}) did not stop within {stopTimeout.TotalSeconds:0}s. Run 'bdk dev stop --app {appId} --force' to terminate it.",
                new { appId, processId = status.ProcessId, stopped = false, forced = false });
        }

        var holder = DevAppInstanceLease.ReadHolder(leasePath);
        if (holder is null || !DevAppInstanceLease.IsHeld(leasePath) || !holder.IsSameProcessRunning())
        {
            return DevAppInstanceCliOutcome.Fail(CliExitCode.SelectedHostUnavailable, "host_changed", $"The lease holder of '{appId}' could not be verified (process ID and start time). Nothing was killed.");
        }

        var kill = processManager.Kill(holder.ProcessId);

        return kill.Succeeded
            ? DevAppInstanceCliOutcome.Ok($"Terminated '{appId}' (pid {holder.ProcessId}) after the graceful timeout.", new { appId, processId = holder.ProcessId, stopped = true, forced = true })
            : DevAppInstanceCliOutcome.Fail(CliExitCode.CommandFailed, "kill_failed", $"Could not terminate pid {holder.ProcessId}: {kill.Reason}");
    }

    /// <summary>
    /// Drops the instance resources of one application by starting its project with the reserved reset argument.
    /// </summary>
    /// <param name="workspace">The CLI workspace.</param>
    /// <param name="appId">The application ID.</param>
    /// <param name="confirmInstance">The confirmation token, for example <c>forecasthub:w02</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome. The exit code is the reset host's exit code: 0 success, 2 refused, 3 incomplete.</returns>
    public async Task<DevAppInstanceCliOutcome> ResetAsync(CliWorkspaceContext workspace, string appId, string confirmInstance, CancellationToken cancellationToken)
    {
        var checkout = this.Detect(workspace.Path);
        if (!checkout.IsLinkedWorktree)
        {
            return NotLinked(checkout, workspace.Path);
        }

        var registry = this.LoadSnapshot(checkout.CommonDirectory);
        var current = FindCurrent(registry, checkout);
        if (current is null)
        {
            return NoAssignment();
        }

        var expected = DevAppInstanceArguments.FormatInstanceToken(appId, current.Slot);
        if (!string.Equals(confirmInstance, expected, StringComparison.Ordinal))
        {
            return DevAppInstanceCliOutcome.Fail(CliExitCode.InvalidArguments, "confirmation_mismatch", $"Confirm the reset with '--confirm-instance {expected}'.");
        }

        var inventory = current.Applications.FirstOrDefault(i => i.AppId == appId)
            ?? DevAppInstanceInventory.Read(DevAppInstancePaths.GetStateRoot(checkout.WorktreeRoot, appId));
        if (inventory?.ProjectPath is null)
        {
            return DevAppInstanceCliOutcome.Fail(CliExitCode.CommandFailed, DevAppInstanceInactiveReasons.NotRegistered, $"No inventory is recorded for '{appId}' in this worktree. Start the host once first.");
        }

        var status = this.GetProcessStatus(checkout.CommonDirectory, current.WorktreeId, appId, discovery.Discover(workspace, includeAll: true));
        if (status.LeaseHeld)
        {
            return DevAppInstanceCliOutcome.Fail(CliExitCode.CommandFailed, DevAppInstanceErrorCodes.InstanceRunning, $"'{appId}' is running{(status.ProcessId is null ? string.Empty : $" (pid {status.ProcessId})")}. Run 'bdk dev stop --app {appId}' first.");
        }

        var project = Path.Combine(checkout.WorktreeRoot, inventory.ProjectPath.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(project) && !File.Exists(project))
        {
            return DevAppInstanceCliOutcome.Fail(CliExitCode.CommandFailed, "project_not_found", $"The project '{inventory.ProjectPath}' no longer exists in this worktree.");
        }

        var policy = registry.PortPolicy ?? DevAppInstancePortPolicy.Default;
        var application = registry.Applications.FirstOrDefault(a => a.AppId == appId);
        var summary = new List<string>
        {
            $"Application: {appId}",
            $"Worktree:    {checkout.WorktreeRoot}",
            $"Slot:        {current.Slot}",
        };
        if (application is not null)
        {
            summary.Add($"URLs:        https://localhost:{policy.GetPort(current.Slot, application.HttpsOffset)};http://localhost:{policy.GetPort(current.Slot, application.HttpOffset)}");
        }

        summary.Add($"Databases:   {string.Join(", ", inventory.Resources.Where(r => !r.Shared && DevAppInstanceResolver.IsDatabaseKind(r.Kind)).Select(r => r.Target))}");
        summary.Add($"Storage:     {string.Join(", ", inventory.Resources.Where(r => !r.Shared && !DevAppInstanceResolver.IsDatabaseKind(r.Kind)).Select(r => r.Target))}");

        var result = await processRunner.RunAsync(
            "dotnet",
            ["run", "--project", project, "--no-launch-profile", "--", DevAppInstanceArguments.FormatReset(appId, current.Slot)],
            checkout.WorktreeRoot,
            cancellationToken);

        return ToOutcome("reset", appId, result, summary);
    }

    /// <summary>
    /// Releases the slot of the current worktree when no instance runs and no instance resources remain.
    /// </summary>
    /// <param name="workspace">The CLI workspace.</param>
    /// <param name="confirmSlot">The confirmation slot.</param>
    /// <returns>The outcome.</returns>
    public DevAppInstanceCliOutcome Release(CliWorkspaceContext workspace, int confirmSlot)
    {
        var checkout = this.Detect(workspace.Path);
        if (!checkout.IsLinkedWorktree)
        {
            return NotLinked(checkout, workspace.Path);
        }

        using (this.Lock(checkout.CommonDirectory))
        {
            var registryPath = DevAppInstancePaths.GetRegistryFile(checkout.CommonDirectory);
            var registry = DevAppInstanceRegistry.Load(registryPath);
            var current = FindCurrent(registry, checkout);
            if (current is null)
            {
                return NoAssignment();
            }

            if (current.Slot != confirmSlot)
            {
                return DevAppInstanceCliOutcome.Fail(CliExitCode.InvalidArguments, "confirmation_mismatch", $"This worktree has slot {current.Slot}. Confirm with '--confirm-slot {current.Slot}'.");
            }

            var blockers = this.GetBlockers(registry, checkout.CommonDirectory, current);
            if (blockers is not null)
            {
                return blockers;
            }

            registry.RemoveAssignment(current.WorktreeId);
            registry.Save(registryPath);
            TryDelete(DevAppInstancePaths.GetAssignmentFile(checkout.WorktreeRoot));

            return DevAppInstanceCliOutcome.Ok($"Released slot {current.Slot}. Logs and key rings stay under {Path.Combine(checkout.WorktreeRoot, DevAppInstancePaths.LocalDirectoryName)}.", new { worktreeId = current.WorktreeId, slot = current.Slot, released = true });
        }
    }

    /// <summary>
    /// Lists orphaned assignments of deleted worktrees and, when confirmed, drops their databases and frees their slots.
    /// </summary>
    /// <param name="workspace">The CLI workspace.</param>
    /// <param name="confirm">A value indicating whether orphaned resources are dropped.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    public async Task<DevAppInstanceCliOutcome> PruneAsync(CliWorkspaceContext workspace, bool confirm, CancellationToken cancellationToken)
    {
        var checkout = this.Detect(workspace.Path);
        if (checkout.Kind == GitCheckoutKind.OutsideGit)
        {
            return OutsideGit(workspace.Path, checkout.Warning);
        }

        var registry = this.LoadSnapshot(checkout.CommonDirectory);
        var listed = this.ListWorktrees(checkout.WorktreeRoot);
        var hosts = discovery.Discover(workspace, includeAll: true);
        var orphans = registry.Assignments
            .Where(a => IsOrphaned(a, listed))
            .Where(a => !registry.Applications.Select(app => app.AppId).Concat(a.Applications.Select(i => i.AppId)).Distinct()
                .Any(appId => this.GetProcessStatus(checkout.CommonDirectory, a.WorktreeId, appId, hosts).IsRunning))
            .OrderBy(a => a.Slot)
            .ToList();

        var lines = new List<string>();
        var entries = new List<object>();
        var failures = 0;
        foreach (var orphan in orphans)
        {
            lines.Add($"slot {orphan.Slot}: {orphan.Path} (worktree {orphan.WorktreeId})");
            var apps = new List<object>();
            var allSucceeded = true;
            foreach (var inventory in orphan.Applications)
            {
                var databases = inventory.Resources.Where(r => !r.Shared && DevAppInstanceResolver.IsDatabaseKind(r.Kind)).Select(r => r.Target).ToList();
                lines.Add($"  {inventory.AppId}: {(databases.Count == 0 ? "no databases" : string.Join(", ", databases))}");
                if (!confirm || databases.Count == 0)
                {
                    apps.Add(new { appId = inventory.AppId, databases, state = confirm ? "nothing_to_drop" : "pending" });

                    continue;
                }

                var project = inventory.ProjectPath is null ? null : Path.Combine(checkout.WorktreeRoot, inventory.ProjectPath.Replace('/', Path.DirectorySeparatorChar));
                if (project is null || (!Directory.Exists(project) && !File.Exists(project)))
                {
                    allSucceeded = false;
                    lines.Add($"    project '{inventory.ProjectPath}' does not exist in this checkout; drop the databases manually");
                    apps.Add(new { appId = inventory.AppId, databases, state = "project_not_found", remaining = databases });

                    continue;
                }

                var result = await processRunner.RunAsync(
                    "dotnet",
                    ["run", "--project", project, "--no-launch-profile", "--", DevAppInstanceArguments.FormatOrphanReset(orphan.WorktreeId)],
                    checkout.WorktreeRoot,
                    cancellationToken);
                var outcome = ToOutcome("orphan-reset", inventory.AppId, result, []);
                allSucceeded &= outcome.Succeeded;
                lines.Add($"    {outcome.Summary}");
                apps.Add(new { appId = inventory.AppId, databases, state = outcome.Succeeded ? "dropped" : outcome.Code, result = outcome.Data });
            }

            var released = false;
            if (confirm && allSucceeded)
            {
                using (this.Lock(checkout.CommonDirectory))
                {
                    var registryPath = DevAppInstancePaths.GetRegistryFile(checkout.CommonDirectory);
                    var latest = DevAppInstanceRegistry.Load(registryPath);
                    released = latest.RemoveAssignment(orphan.WorktreeId);
                    latest.Save(registryPath);
                }

                lines.Add($"  slot {orphan.Slot} released");
            }
            else if (confirm)
            {
                failures++;
                lines.Add($"  slot {orphan.Slot} kept; run 'bdk dev prune --confirm' again after fixing the errors");
            }

            entries.Add(new { worktreeId = orphan.WorktreeId, slot = orphan.Slot, path = orphan.Path, applications = apps, released });
        }

        var data = new { confirmed = confirm, gitWorktreeListAvailable = listed is not null, orphans = entries };
        if (orphans.Count == 0)
        {
            return DevAppInstanceCliOutcome.Ok("No orphaned assignments.", data);
        }

        if (!confirm)
        {
            lines.Add("Run 'bdk dev prune --confirm' to drop these databases and free the slots.");

            return DevAppInstanceCliOutcome.Ok($"{orphans.Count} orphaned assignment(s).", data, lines);
        }

        return failures == 0
            ? DevAppInstanceCliOutcome.Ok($"Pruned {orphans.Count} orphaned assignment(s).", data, lines)
            : DevAppInstanceCliOutcome.Fail((CliExitCode)DevAppInstanceArguments.ExitCodes.Incomplete, DevAppInstanceErrorCodes.ResetIncomplete, $"{failures} of {orphans.Count} orphaned assignment(s) could not be pruned.", data, lines);
    }

    /// <summary>
    /// Builds and starts DevKit web hosts of the current checkout and streams their output until they exit.
    /// </summary>
    /// <remarks>
    /// The hosts build one after another, because parallel builds of shared projects collide on <c>obj</c> files. They then
    /// run concurrently with <c>dotnet run --no-build</c> and their launch profile. In a linked worktree each host isolates
    /// itself, so the launch profile ports are replaced by the slot ports. Hosts whose instance is already running are
    /// skipped. When <paramref name="stopToken"/> fires, the service asks managed instances to stop through their stop
    /// file, waits <see cref="RunShutdownTimeout"/>, then terminates the remaining process trees.
    /// </remarks>
    /// <param name="workspace">The CLI workspace.</param>
    /// <param name="appIds">The application IDs to start, or an empty list for all hosts.</param>
    /// <param name="noBuild">A value indicating whether the build step is skipped.</param>
    /// <param name="output">Receives progress lines and the prefixed host output lines.</param>
    /// <param name="stopToken">Signals that the hosts should stop, usually Ctrl+C.</param>
    /// <param name="killToken">Signals that the hosts must be terminated immediately.</param>
    /// <returns>The outcome after all hosts exited.</returns>
    public async Task<DevAppInstanceCliOutcome> RunAsync(
        CliWorkspaceContext workspace,
        IReadOnlyList<string> appIds,
        bool noBuild,
        Action<string> output,
        CancellationToken stopToken,
        CancellationToken killToken = default)
    {
        output ??= _ => { };
        var checkout = this.Detect(workspace.Path);
        if (checkout.Kind == GitCheckoutKind.OutsideGit)
        {
            return OutsideGit(workspace.Path, checkout.Warning);
        }

        var registry = this.LoadSnapshot(checkout.CommonDirectory);
        var current = checkout.IsLinkedWorktree ? FindCurrent(registry, checkout) : null;
        var projects = DevAppInstanceHostProjects.Find(checkout.WorktreeRoot, registry, current);
        if (projects.Count == 0)
        {
            return DevAppInstanceCliOutcome.Fail(CliExitCode.HostNotFound, "no_host_project", $"No DevKit web host project was found in '{checkout.WorktreeRoot}'.");
        }

        var requested = (appIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.Ordinal).ToList();
        var unknown = requested.Where(id => !projects.Any(p => string.Equals(p.AppId, id, StringComparison.Ordinal))).ToList();
        if (unknown.Count > 0)
        {
            return DevAppInstanceCliOutcome.Fail(
                CliExitCode.InvalidArguments,
                "app_not_found",
                $"Unknown application(s): {string.Join(", ", unknown)}. Available: {string.Join(", ", projects.Select(p => p.AppId))}.",
                new { available = projects.Select(p => p.AppId).ToList() });
        }

        var selected = requested.Count == 0 ? projects : projects.Where(p => requested.Contains(p.AppId, StringComparer.Ordinal)).ToList();
        var width = selected.Max(p => p.AppId.Length);
        string Prefix(string appId) => $"[{appId.PadRight(width)}] ";

        var hosts = current is null ? (IReadOnlyList<HostRuntimeInfo>)[] : discovery.Discover(workspace, includeAll: true);
        var toStart = new List<DevAppInstanceHostProject>();
        var skipped = new List<object>();
        foreach (var project in selected)
        {
            var status = current is null ? null : this.GetProcessStatus(checkout.CommonDirectory, current.WorktreeId, project.AppId, hosts);
            if (status?.LeaseHeld == true)
            {
                output($"{Prefix(project.AppId)}already running{(status.ProcessId is null ? string.Empty : $" (pid {status.ProcessId})")}, skipped");
                skipped.Add(new { appId = project.AppId, projectPath = project.ProjectPath, state = "skipped", processId = status.ProcessId });
            }
            else
            {
                toStart.Add(project);
            }
        }

        if (toStart.Count == 0)
        {
            return DevAppInstanceCliOutcome.Ok("All selected hosts are already running.", new { worktreeRoot = checkout.WorktreeRoot, hosts = skipped });
        }

        if (!noBuild)
        {
            foreach (var project in toStart)
            {
                output($"{Prefix(project.AppId)}building {project.ProjectPath}");
                DevAppInstanceProcessResult build;
                try
                {
                    build = await processRunner.RunAsync("dotnet", ["build", project.ProjectFile, "--nologo", "-v", "q"], checkout.WorktreeRoot, stopToken);
                }
                catch (OperationCanceledException)
                {
                    return DevAppInstanceCliOutcome.Fail(CliExitCode.CommandFailed, "canceled", "Canceled before the hosts started.");
                }

                if (build.ExitCode != 0)
                {
                    return DevAppInstanceCliOutcome.Fail(
                        CliExitCode.CommandFailed,
                        "build_failed",
                        $"The build of '{project.AppId}' failed with exit code {build.ExitCode}. No host was started.",
                        new { appId = project.AppId, projectPath = project.ProjectPath, exitCode = build.ExitCode },
                        [.. Tail(build.StandardOutput, 30), .. Tail(build.StandardError, 30)]);
                }
            }
        }

        using var killSource = CancellationTokenSource.CreateLinkedTokenSource(killToken);
        using var stopRegistration = stopToken.Register(() =>
        {
            if (current is not null)
            {
                foreach (var project in toStart)
                {
                    RequestStop(DevAppInstancePaths.GetLeaseFile(checkout.CommonDirectory, current.WorktreeId, project.AppId));
                }
            }

            killSource.CancelAfter(this.RunShutdownTimeout);
        });

        var mode = current is not null ? $"slot {current.Slot}" : checkout.IsLinkedWorktree ? "unassigned linked worktree" : "ordinary checkout";
        output($"Starting {toStart.Count} host(s) in {checkout.WorktreeRoot} ({mode}). Press Ctrl+C to stop.");
        var runs = toStart.Select(async project =>
        {
            var exitCode = await processRunner.RunStreamingAsync(
                "dotnet",
                ["run", "--project", project.ProjectFile, "--no-build"],
                checkout.WorktreeRoot,
                line => output(Prefix(project.AppId) + line),
                stopToken,
                killSource.Token);
            var failed = exitCode != 0 && !stopToken.IsCancellationRequested;
            output($"{Prefix(project.AppId)}exited with code {exitCode}");

            return (Project: project, ExitCode: exitCode, Failed: failed);
        }).ToList();
        var results = await Task.WhenAll(runs);

        var data = new
        {
            worktreeRoot = checkout.WorktreeRoot,
            slot = current?.Slot,
            hosts = results
                .Select(r => (object)new { appId = r.Project.AppId, projectPath = r.Project.ProjectPath, state = r.Failed ? "failed" : "exited", exitCode = r.ExitCode })
                .Concat(skipped)
                .ToList(),
        };
        var failures = results.Where(r => r.Failed).Select(r => r.Project.AppId).ToList();

        return failures.Count == 0
            ? DevAppInstanceCliOutcome.Ok($"{results.Length} host(s) stopped.", data)
            : DevAppInstanceCliOutcome.Fail(CliExitCode.CommandFailed, "host_failed", $"{failures.Count} host(s) exited with an error: {string.Join(", ", failures)}.", data);
    }

    /// <summary>
    /// Determines the verified process state of one application instance from its runtime descriptor and lease.
    /// </summary>
    /// <param name="commonDirectory">The common Git directory.</param>
    /// <param name="worktreeId">The worktree ID.</param>
    /// <param name="appId">The application ID.</param>
    /// <param name="hosts">The discovered hosts.</param>
    /// <returns>The process status.</returns>
    public DevAppInstanceProcessStatus GetProcessStatus(string commonDirectory, Guid worktreeId, string appId, IReadOnlyList<HostRuntimeInfo> hosts)
    {
        var leasePath = DevAppInstancePaths.GetLeaseFile(commonDirectory, worktreeId, appId);
        var host = (hosts ?? [])
            .Where(h => h.Descriptor?.DevAppInstance is { } d && d.WorktreeId == worktreeId && string.Equals(d.AppId, appId, StringComparison.Ordinal))
            .OrderByDescending(h => h.Descriptor.StartedAt)
            .FirstOrDefault();

        if (!DevAppInstanceLease.IsHeld(leasePath))
        {
            return new DevAppInstanceProcessStatus("stopped", null, null, false);
        }

        var holder = DevAppInstanceLease.ReadHolder(leasePath);
        var verifiedHost = host is not null &&
            host.Status == HostRuntimeStatus.Ready &&
            (holder is null || holder.ProcessId == host.Descriptor.ProcessId) &&
            new DevAppInstanceLeaseHolder(host.Descriptor.ProcessId, host.Descriptor.DevAppInstance.ProcessStartedAt, null, null).IsSameProcessRunning();

        return new DevAppInstanceProcessStatus(
            "running",
            holder?.ProcessId ?? (verifiedHost ? host.Descriptor.ProcessId : null),
            verifiedHost ? host.Descriptor.RuntimeId : null,
            true);
    }

    internal static DevAppInstanceCliOutcome ToOutcome(string mode, string appId, DevAppInstanceProcessResult result, IReadOnlyList<string> summary)
    {
        var line = (result.StandardOutput ?? string.Empty)
            .Split('\n')
            .Select(l => l.Trim())
            .LastOrDefault(l => l.StartsWith(ResultPrefix, StringComparison.Ordinal));
        JsonElement? payload = null;
        if (line is not null)
        {
            try
            {
                payload = JsonSerializer.Deserialize<JsonElement>(line[ResultPrefix.Length..], ResultOptions);
            }
            catch (JsonException)
            {
                payload = null;
            }
        }

        var lines = new List<string>(summary);
        if (payload is null)
        {
            lines.AddRange(Tail(result.StandardError, 20));
            lines.AddRange(Tail(result.StandardOutput, 20));

            return DevAppInstanceCliOutcome.Fail(
                CliExitCode.CommandFailed,
                "reset_no_result",
                $"The {mode} host of '{appId}' exited with code {result.ExitCode} without a result. Check that the project builds and uses DevKitWebApplication.CreateBuilder.",
                new { mode, appId, exitCode = result.ExitCode },
                lines);
        }

        var value = payload.Value;
        var completed = ReadStrings(value, "completed");
        var remaining = ReadStrings(value, "remaining");
        var errors = ReadStrings(value, "errors");
        var code = value.TryGetProperty("errorCode", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
        lines.AddRange(completed.Select(t => $"dropped:   {t}"));
        lines.AddRange(remaining.Select(t => $"remaining: {t}"));
        lines.AddRange(errors.Select(e => $"error:     {e}"));

        var exitCode = result.ExitCode switch
        {
            DevAppInstanceArguments.ExitCodes.Success => CliExitCode.Success,
            DevAppInstanceArguments.ExitCodes.Refused => (CliExitCode)DevAppInstanceArguments.ExitCodes.Refused,
            DevAppInstanceArguments.ExitCodes.Incomplete => (CliExitCode)DevAppInstanceArguments.ExitCodes.Incomplete,
            _ => CliExitCode.CommandFailed,
        };

        return exitCode == CliExitCode.Success
            ? DevAppInstanceCliOutcome.Ok($"{mode} of '{appId}' completed ({completed.Count} target(s) dropped).", value, lines)
            : DevAppInstanceCliOutcome.Fail(exitCode, code ?? "reset_failed", $"{mode} of '{appId}' failed ({code ?? $"exit code {result.ExitCode}"}).", value, lines);
    }

    internal static IReadOnlyList<string> ParseWorktreeList(string porcelain) =>
        ParseWorktreeEntries(porcelain).Select(e => e.Path).ToList();

    internal static IReadOnlyList<ListedWorktree> ParseWorktreeEntries(string porcelain)
    {
        var entries = new List<ListedWorktree>();
        foreach (var line in (porcelain ?? string.Empty).Split('\n').Select(l => l.TrimEnd('\r')))
        {
            if (line.StartsWith("worktree ", StringComparison.Ordinal))
            {
                entries.Add(new ListedWorktree(line["worktree ".Length..].Trim(), false, false));
            }
            else if (entries.Count > 0 && (line == "locked" || line.StartsWith("locked ", StringComparison.Ordinal)))
            {
                entries[^1] = entries[^1] with { Locked = true };
            }
            else if (entries.Count > 0 && (line == "prunable" || line.StartsWith("prunable ", StringComparison.Ordinal)))
            {
                entries[^1] = entries[^1] with { Prunable = true };
            }
        }

        return entries;
    }

    // A worktree is live while Git lists it (locked, or not prunable) at the recorded path, or at a new
    // path after `git worktree move` whose .dev/instance.json still carries the worktree ID.
    private static bool IsOrphaned(DevAppInstanceAssignment assignment, IReadOnlyList<ListedWorktree> listed)
    {
        if (listed is null)
        {
            return !Directory.Exists(assignment.Path);
        }

        var live = listed.Where(w => w.Locked || !w.Prunable).ToList();
        if (live.Any(w => GitCheckoutDetector.PathsEqual(w.Path, assignment.Path)))
        {
            return false;
        }

        return !live.Any(w => CarriesWorktreeId(w.Path, assignment.WorktreeId));
    }

    private static bool CarriesWorktreeId(string worktreeRoot, Guid worktreeId)
    {
        try
        {
            return DevAppInstanceLocalAssignment.Read(worktreeRoot)?.WorktreeId == worktreeId;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private IReadOnlyList<ListedWorktree> ListWorktrees(string worktreeRoot)
    {
        var output = gitRunner?.Run(worktreeRoot, "worktree", "list", "--porcelain");

        return output is null ? null : ParseWorktreeEntries(output);
    }

    internal sealed record ListedWorktree(string Path, bool Locked, bool Prunable);

    private DevAppInstanceCliOutcome GetBlockers(DevAppInstanceRegistry registry, string commonDirectory, DevAppInstanceAssignment assignment)
    {
        var appIds = registry.Applications.Select(a => a.AppId).Concat(assignment.Applications.Select(i => i.AppId)).Distinct().ToList();
        var running = appIds.Where(id => DevAppInstanceLease.IsHeld(DevAppInstancePaths.GetLeaseFile(commonDirectory, assignment.WorktreeId, id))).ToList();
        if (running.Count > 0)
        {
            return DevAppInstanceCliOutcome.Fail(
                CliExitCode.CommandFailed,
                DevAppInstanceErrorCodes.InstanceRunning,
                $"Stop the running instance(s) first: {string.Join(", ", running.Select(id => $"bdk dev stop --app {id}"))}.",
                new { running });
        }

        var remaining = assignment.Applications
            .Where(i => i.Resources.Any(r => !r.Shared))
            .ToList();
        if (remaining.Count > 0)
        {
            return DevAppInstanceCliOutcome.Fail(
                CliExitCode.CommandFailed,
                "resources_remaining",
                $"Instance resources remain. Run {string.Join(" and ", remaining.Select(i => $"'bdk dev reset --app {i.AppId} --confirm-instance {DevAppInstanceArguments.FormatInstanceToken(i.AppId, assignment.Slot)}'"))} first.",
                new { remaining = remaining.Select(i => new { appId = i.AppId, targets = i.Resources.Where(r => !r.Shared).Select(r => r.Target).ToList() }) },
                remaining.SelectMany(i => i.Resources.Where(r => !r.Shared).Select(r => $"{i.AppId}: {r.Kind} {r.Target}")).ToList());
        }

        return null;
    }

    private static DevAppInstanceAssignment FindCurrent(DevAppInstanceRegistry registry, GitCheckoutInfo checkout)
    {
        var local = DevAppInstanceLocalAssignment.Read(checkout.WorktreeRoot);
        if (local is not null && registry.FindByWorktreeId(local.WorktreeId) is { } byId && GitCheckoutDetector.PathsEqual(byId.Path, checkout.WorktreeRoot))
        {
            return byId;
        }

        return registry.FindByPath(checkout.WorktreeRoot);
    }

    private GitCheckoutInfo Detect(string path) => GitCheckoutDetector.Detect(RestoreCasing(path), gitRunner);

    // The CLI workspace path is upper-cased on Windows for hashing; registry and output use the on-disk casing.
    private static string RestoreCasing(string path)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            return path;
        }

        try
        {
            var directory = new DirectoryInfo(path);
            var segments = new Stack<string>();
            while (directory.Parent is not null)
            {
                segments.Push(directory.Parent.EnumerateDirectories(directory.Name).FirstOrDefault()?.Name ?? directory.Name);
                directory = directory.Parent;
            }

            return Path.Combine([directory.FullName, .. segments]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return path;
        }
    }

    private FileStream Lock(string commonDirectory) =>
        DevAppInstanceFiles.AcquireLock(DevAppInstancePaths.GetRegistryLockFile(commonDirectory), this.LockTimeout);

    private DevAppInstanceRegistry LoadSnapshot(string commonDirectory)
    {
        using (this.Lock(commonDirectory))
        {
            return DevAppInstanceRegistry.Load(DevAppInstancePaths.GetRegistryFile(commonDirectory));
        }
    }

    private static DevAppInstanceCliOutcome OutsideGit(string path, string warning = null) =>
        DevAppInstanceCliOutcome.Fail(
            CliExitCode.CommandFailed,
            DevAppInstanceInactiveReasons.OutsideGit,
            warning is null ? $"'{path}' is not inside a Git checkout." : $"'{path}' is not inside a usable Git checkout. {warning}");

    private static DevAppInstanceCliOutcome NotLinked(GitCheckoutInfo checkout, string path) =>
        checkout.Kind == GitCheckoutKind.OutsideGit
            ? OutsideGit(path, checkout.Warning)
            : DevAppInstanceCliOutcome.Fail(CliExitCode.CommandFailed, DevAppInstanceInactiveReasons.OrdinaryCheckout, "This command applies only to linked worktrees. The ordinary checkout keeps its baseline settings.");

    private static DevAppInstanceCliOutcome NoAssignment() =>
        DevAppInstanceCliOutcome.Fail(CliExitCode.CommandFailed, DevAppInstanceInactiveReasons.NotRegistered, "This worktree has no slot assignment. Start a host or run 'bdk dev configure --slot N' first.");

    private static IReadOnlyList<string> ReadStrings(JsonElement element, string name) =>
        element.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()).ToList()
            : [];

    private static IEnumerable<string> Tail(string text, int count) =>
        (text ?? string.Empty).Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).TakeLast(count);

    private static void RequestStop(string leasePath)
    {
        if (!DevAppInstanceLease.IsHeld(leasePath))
        {
            return;
        }

        try
        {
            File.WriteAllText(DevAppInstanceLease.GetStopRequestFile(leasePath), $"{{\"requestedAt\":\"{DateTimeOffset.UtcNow:O}\"}}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The host also receives the console Ctrl+C, and the shutdown timeout terminates it otherwise.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort; a leftover file is handled by the next host start.
        }
    }
}
