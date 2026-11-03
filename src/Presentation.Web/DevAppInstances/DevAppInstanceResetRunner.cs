namespace BridgingIT.DevKit.Presentation.Web;

using System.Text.Json;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Runs the one-shot reset and orphan-reset modes of a development instance host.
/// </summary>
/// <remarks>
/// Both modes run after the service provider is built and before the listener starts. They exit the process with a code
/// from <see cref="DevAppInstanceArguments.ExitCodes"/> and write one machine-readable result line for <c>bdk dev</c>.
/// </remarks>
internal static class DevAppInstanceResetRunner
{
    internal const string LoggerCategory = "BridgingIT.DevKit.DevAppInstance";

    internal const string ResultPrefix = "[BDK-WORKTREE-RESULT] ";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    internal static Action<int> Exit { get; set; } = Environment.Exit;

    internal static void Refuse(DevAppInstanceException exception, string mode)
    {
        Console.Error.WriteLine(exception.ToLogLine());
        WriteResult(new ResetResult(mode, exception.AppId, exception.Slot, null, false, [], [], [exception.Message], exception.Code));
        Exit(DevAppInstanceArguments.ExitCodes.Refused);
    }

    internal static void RunInstanceReset(
        WebApplication app,
        DevAppInstanceSession session,
        DevAppInstanceRuntimeState state)
    {
        var context = session.Context;
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory);
        int code;
        try
        {
            foreach (var step in context.LogSteps.Concat(state.Steps))
            {
                DevAppInstanceLifecycleService.WriteStep(logger, step);
            }

            var registry = LoadRegistry(context.CommonDirectory);
            var recorded = registry.FindByWorktreeId(context.WorktreeId.Value)?.Applications
                .FirstOrDefault(a => a.AppId == context.AppId && a.Slot == context.Slot);
            var targets = recorded?.Resources.Where(r => !r.Shared).ToList() ?? [];

            logger.LogInformation(
                "[{LogKey}] worktree reset requested (app={AppId}, slot={Slot}, worktree={WorktreeRoot}, urls={Urls}, databases={Databases}, storage={Storage})",
                DevAppInstanceLifecycleService.LogKey,
                context.AppId,
                context.Slot,
                context.WorktreeRoot,
                $"{context.HttpsUrl};{context.HttpUrl}",
                string.Join(", ", targets.Where(t => DevAppInstanceResolver.IsDatabaseKind(t.Kind)).Select(t => t.Target)),
                string.Join(", ", targets.Where(t => !DevAppInstanceResolver.IsDatabaseKind(t.Kind)).Select(t => t.Target)));

            var unrecorded = context.Resources
                .Where(p => !p.Shared && !targets.Any(t => t.Kind == p.Kind && string.Equals(t.Target, p.Target, StringComparison.Ordinal)))
                .Select(p => p.Target)
                .ToList();
            if (unrecorded.Count > 0)
            {
                throw new DevAppInstanceException(
                    DevAppInstanceErrorCodes.ResourceNotOwned,
                    $"Reset targets are not recorded in the registry inventory: {string.Join(", ", unrecorded)}.",
                    "Start the host once so the inventory is recorded, then retry the reset.")
                { AppId = context.AppId, Slot = context.Slot };
            }

            var outcome = RunAdaptersAsync(session.Adapters, targets, context, app.Configuration).GetAwaiter().GetResult();
            UpdateInventory(context, recorded, outcome);
            code = Report(logger, "reset", context.AppId, context.Slot, context.WorktreeId, outcome);
        }
        catch (DevAppInstanceException ex)
        {
            DevAppInstanceLifecycleService.LogFailure(logger, ex, ex.RecoveryAction);
            WriteResult(new ResetResult("reset", context.AppId, context.Slot, context.WorktreeId, false, [], [], [ex.Message], ex.Code));
            code = DevAppInstanceArguments.ExitCodes.Refused;
        }
        finally
        {
            state.Dispose();
        }

        Finish(app, code);
    }

    internal static void RunOrphanReset(
        WebApplication app,
        DevAppInstanceSession session,
        DevAppInstanceInventory inventory,
        DevAppInstanceContext orphanContext)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory);
        int code;
        try
        {
            var adapters = session.Adapters;
            var targets = new List<DevAppInstanceInventoryResource>();
            var refusals = new List<string>();
            foreach (var target in inventory?.Resources ?? [])
            {
                if (target.Shared)
                {
                    continue;
                }

                var adapter = adapters.FirstOrDefault(a => a.Kind == target.Kind);
                if (adapter is null)
                {
                    refusals.Add($"No declared adapter handles kind '{target.Kind}' of target '{target.Target}'.");

                    continue;
                }

                if (!adapter.RequiresOrphanCleanup)
                {
                    continue;
                }

                var reason = adapter.ValidateOrphanTarget(target, orphanContext, app.Configuration);
                if (reason is not null)
                {
                    refusals.Add(reason);
                }
                else
                {
                    targets.Add(target);
                }
            }

            logger.LogInformation(
                "[{LogKey}] worktree orphan reset requested (app={AppId}, worktreeId={WorktreeId}, slot={Slot}, databases={Databases})",
                DevAppInstanceLifecycleService.LogKey,
                orphanContext.AppId,
                orphanContext.WorktreeId,
                orphanContext.Slot,
                string.Join(", ", targets.Select(t => t.Target)));

            if (refusals.Count > 0)
            {
                throw new DevAppInstanceException(
                    DevAppInstanceErrorCodes.ResourceNotOwned,
                    string.Join(" ", refusals),
                    "Drop the listed databases manually after verifying them, then run 'bdk dev prune --confirm' again.")
                { AppId = orphanContext.AppId, Slot = orphanContext.Slot };
            }

            var outcome = RunAdaptersAsync(adapters, targets, orphanContext, app.Configuration).GetAwaiter().GetResult();
            code = Report(logger, "orphan-reset", orphanContext.AppId, orphanContext.Slot, orphanContext.WorktreeId, outcome);
        }
        catch (DevAppInstanceException ex)
        {
            DevAppInstanceLifecycleService.LogFailure(logger, ex, ex.RecoveryAction);
            WriteResult(new ResetResult("orphan-reset", orphanContext.AppId, orphanContext.Slot, orphanContext.WorktreeId, false, [], [], [ex.Message], ex.Code));
            code = DevAppInstanceArguments.ExitCodes.Refused;
        }

        Finish(app, code);
    }

    private static async Task<DevAppInstanceResetOutcome> RunAdaptersAsync(
        IReadOnlyList<IDevAppInstanceResourceAdapter> adapters,
        IReadOnlyList<DevAppInstanceInventoryResource> targets,
        DevAppInstanceContext context,
        IConfiguration configuration)
    {
        var completed = new List<string>();
        var remaining = new List<string>();
        var errors = new List<string>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        foreach (var group in targets.GroupBy(t => t.Kind))
        {
            var adapter = adapters.First(a => a.Kind == group.Key);
            var outcome = await adapter.ResetAsync(group.ToList(), context, configuration, cancellation.Token);
            completed.AddRange(outcome.Completed);
            remaining.AddRange(outcome.Remaining);
            errors.AddRange(outcome.Errors);
        }

        return new DevAppInstanceResetOutcome(completed, remaining, errors);
    }

    private static void UpdateInventory(DevAppInstanceContext context, DevAppInstanceInventory recorded, DevAppInstanceResetOutcome outcome)
    {
        if (recorded is null || outcome.Completed.Count == 0)
        {
            return;
        }

        var updated = recorded with
        {
            Resources = recorded.Resources.Where(r => r.Shared || !outcome.Completed.Contains(r.Target, StringComparer.Ordinal)).ToList(),
        };
        var registryPath = DevAppInstancePaths.GetRegistryFile(context.CommonDirectory);
        using (DevAppInstanceFiles.AcquireLock(DevAppInstancePaths.GetRegistryLockFile(context.CommonDirectory), TimeSpan.FromSeconds(10)))
        {
            var registry = DevAppInstanceRegistry.Load(registryPath);
            registry.SetInventory(updated);
            registry.Save(registryPath);
        }

        updated.Write(context.StateRoot);
    }

    private static DevAppInstanceRegistry LoadRegistry(string commonDirectory)
    {
        using (DevAppInstanceFiles.AcquireLock(DevAppInstancePaths.GetRegistryLockFile(commonDirectory), TimeSpan.FromSeconds(10)))
        {
            return DevAppInstanceRegistry.Load(DevAppInstancePaths.GetRegistryFile(commonDirectory));
        }
    }

    private static int Report(ILogger logger, string mode, string appId, int? slot, Guid? worktreeId, DevAppInstanceResetOutcome outcome)
    {
        foreach (var error in outcome.Errors)
        {
            logger.LogError("[{LogKey}] worktree {Mode} target failed ({Error})", DevAppInstanceLifecycleService.LogKey, mode, error);
        }

        if (outcome.Succeeded)
        {
            logger.LogInformation(
                "[{LogKey}] worktree {Mode} completed (app={AppId}, slot={Slot}, completed={Completed})",
                DevAppInstanceLifecycleService.LogKey, mode, appId, slot, string.Join(", ", outcome.Completed));
        }
        else
        {
            var exception = new DevAppInstanceException(
                DevAppInstanceErrorCodes.ResetIncomplete,
                $"Completed: {string.Join(", ", outcome.Completed)}. Remaining: {string.Join(", ", outcome.Remaining)}.",
                "Fix the reported errors and run the same command again. Completed targets are skipped safely.")
            { AppId = appId, Slot = slot };
            DevAppInstanceLifecycleService.LogFailure(logger, exception, exception.RecoveryAction);
        }

        WriteResult(new ResetResult(
            mode, appId, slot, worktreeId, outcome.Succeeded, outcome.Completed, outcome.Remaining, outcome.Errors,
            outcome.Succeeded ? null : DevAppInstanceErrorCodes.ResetIncomplete));

        return outcome.Succeeded ? DevAppInstanceArguments.ExitCodes.Success : DevAppInstanceArguments.ExitCodes.Incomplete;
    }

    private static void WriteResult(ResetResult result) =>
        Console.Out.WriteLine(ResultPrefix + JsonSerializer.Serialize(result, SerializerOptions));

    private static void Finish(WebApplication app, int code)
    {
        try
        {
            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
        {
            // The provider may already be disposed; the exit code still applies.
        }

        Console.Out.Flush();
        Exit(code);
    }

    internal sealed record ResetResult(
        string Mode,
        string AppId,
        int? Slot,
        Guid? WorktreeId,
        bool Succeeded,
        IReadOnlyList<string> Completed,
        IReadOnlyList<string> Remaining,
        IReadOnlyList<string> Errors,
        string ErrorCode);
}
