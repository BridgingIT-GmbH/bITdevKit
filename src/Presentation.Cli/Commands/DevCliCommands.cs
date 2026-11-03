namespace BridgingIT.DevKit.Cli;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

/// <summary>
/// Base class of the <c>bdk dev</c> commands that writes a <see cref="DevAppInstanceCliOutcome"/> as text or JSON.
/// </summary>
/// <param name="name">The command name within the <c>dev</c> group.</param>
/// <param name="description">The command description.</param>
public abstract class DevCliCommandBase(string name, string description) : CliGroupedConsoleCommandBase("dev", name, description)
{
    /// <inheritdoc />
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var context = services.GetRequiredService<CliRuntimeContext>();
        var cliConsole = services.GetRequiredService<CliConsole>();
        var state = services.GetRequiredService<CliExecutionState>();

        DevAppInstanceCliOutcome outcome;
        try
        {
            outcome = this.Validate() ?? await this.ExecuteAsync(services.GetRequiredService<DevAppInstanceCliService>(), context.Workspace, cancellationToken);
        }
        catch (DevAppInstanceException ex)
        {
            var summary = string.IsNullOrWhiteSpace(ex.RecoveryAction) ? ex.Message : $"{ex.Message} {ex.RecoveryAction}";
            outcome = DevAppInstanceCliOutcome.Fail(CliExitCode.CommandFailed, ex.Code, summary);
        }

        state.ExitCode = outcome.ExitCode;
        if (context.Output.IsJson)
        {
            cliConsole.WriteJson(new
            {
                succeeded = outcome.Succeeded,
                code = outcome.Code,
                summary = outcome.Summary,
                exitCode = (int)outcome.ExitCode,
                data = outcome.Data,
            });

            return;
        }

        foreach (var line in outcome.Lines)
        {
            cliConsole.WriteLine(line);
        }

        if (outcome.Succeeded)
        {
            cliConsole.WriteLine(outcome.Summary);
        }
        else
        {
            cliConsole.Error(outcome.Summary, outcome.Code, outcome.ExitCode);
        }
    }

    /// <summary>
    /// Validates the arguments.
    /// </summary>
    /// <returns>A failed outcome, or <c>null</c> when the arguments are valid.</returns>
    protected virtual DevAppInstanceCliOutcome Validate() => null;

    /// <summary>
    /// Runs the operation.
    /// </summary>
    /// <param name="service">The development instance service.</param>
    /// <param name="workspace">The CLI workspace.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    protected abstract Task<DevAppInstanceCliOutcome> ExecuteAsync(DevAppInstanceCliService service, CliWorkspaceContext workspace, CancellationToken cancellationToken);

    /// <summary>
    /// Creates an invalid-arguments outcome.
    /// </summary>
    /// <param name="summary">The summary.</param>
    /// <returns>The outcome.</returns>
    protected static DevAppInstanceCliOutcome Invalid(string summary) =>
        DevAppInstanceCliOutcome.Fail(CliExitCode.InvalidArguments, "invalid_arguments", summary);
}

/// <summary>
/// Shows the development instance of the current worktree: slot, URLs, databases and verified process state.
/// </summary>
/// <example>
/// <code>
/// bdk dev status --json
/// </code>
/// </example>
public sealed class DevStatusCliCommand() : DevCliCommandBase("status", "Shows the development instance of the current worktree")
{
    /// <inheritdoc />
    protected override Task<DevAppInstanceCliOutcome> ExecuteAsync(DevAppInstanceCliService service, CliWorkspaceContext workspace, CancellationToken cancellationToken) =>
        Task.FromResult(service.GetStatus(workspace));
}

/// <summary>
/// Reserves a slot for the current linked worktree, or removes an application from the catalog.
/// </summary>
/// <example>
/// <code>
/// bdk dev configure --slot 7
/// bdk dev configure --forget-app forecasthub
/// </code>
/// </example>
public sealed class DevConfigureCliCommand() : DevCliCommandBase("configure", "Reserves a slot or removes an application from the catalog")
{
    /// <summary>Gets or sets the slot to reserve.</summary>
    [ConsoleCommandOption("slot", Description = "Slot to reserve for the current linked worktree.")]
    public int Slot { get; set; }

    /// <summary>Gets or sets the application to remove from the catalog.</summary>
    [ConsoleCommandOption("forget-app", Description = "Application ID to remove from the catalog.")]
    public string ForgetApp { get; set; }

    /// <inheritdoc />
    protected override DevAppInstanceCliOutcome Validate() =>
        (this.Slot > 0, !string.IsNullOrWhiteSpace(this.ForgetApp)) switch
        {
            (true, true) => Invalid("Specify either --slot or --forget-app, not both."),
            (false, false) => Invalid("Specify --slot N or --forget-app <appId>."),
            _ => null,
        };

    /// <inheritdoc />
    protected override Task<DevAppInstanceCliOutcome> ExecuteAsync(DevAppInstanceCliService service, CliWorkspaceContext workspace, CancellationToken cancellationToken) =>
        Task.FromResult(this.Slot > 0 ? service.ConfigureSlot(workspace, this.Slot) : service.ForgetApplication(workspace, this.ForgetApp.Trim()));
}

/// <summary>
/// Requests a graceful shutdown of a running application instance of the current worktree.
/// </summary>
/// <example>
/// <code>
/// bdk dev stop --app forecasthub
/// </code>
/// </example>
public sealed class DevStopCliCommand() : DevCliCommandBase("stop", "Stops a running application instance of the current worktree")
{
    /// <summary>Gets or sets the application ID.</summary>
    [ConsoleCommandOption("app", Description = "Application ID to stop.")]
    public string App { get; set; }

    /// <summary>Gets or sets a value indicating whether the process is killed after the graceful timeout.</summary>
    [ConsoleCommandOption("force", Description = "Terminate the verified process when it does not stop gracefully.")]
    public bool Force { get; set; }

    /// <summary>Gets or sets the graceful timeout in seconds.</summary>
    [ConsoleCommandOption("timeout", Description = "Seconds to wait for a graceful stop (default 45).")]
    public int Timeout { get; set; }

    /// <inheritdoc />
    protected override DevAppInstanceCliOutcome Validate() =>
        string.IsNullOrWhiteSpace(this.App) ? Invalid("Specify --app <appId>.") :
        this.Timeout < 0 ? Invalid("--timeout must be positive.") :
        null;

    /// <inheritdoc />
    protected override Task<DevAppInstanceCliOutcome> ExecuteAsync(DevAppInstanceCliService service, CliWorkspaceContext workspace, CancellationToken cancellationToken) =>
        service.StopAsync(workspace, this.App.Trim(), this.Force, cancellationToken, this.Timeout > 0 ? TimeSpan.FromSeconds(this.Timeout) : null);
}

/// <summary>
/// Drops the instance databases and storage of one application of the current worktree.
/// </summary>
/// <example>
/// <code>
/// bdk dev reset --app forecasthub --confirm-instance forecasthub:w07
/// </code>
/// </example>
public sealed class DevResetCliCommand() : DevCliCommandBase("reset", "Drops the instance resources of one application")
{
    /// <summary>Gets or sets the application ID.</summary>
    [ConsoleCommandOption("app", Description = "Application ID to reset.")]
    public string App { get; set; }

    /// <summary>Gets or sets the confirmation token.</summary>
    [ConsoleCommandOption("confirm-instance", Description = "Confirmation token <appId>:wNN of the current slot.")]
    public string ConfirmInstance { get; set; }

    /// <inheritdoc />
    protected override DevAppInstanceCliOutcome Validate() =>
        string.IsNullOrWhiteSpace(this.App) ? Invalid("Specify --app <appId>.") :
        string.IsNullOrWhiteSpace(this.ConfirmInstance) ? Invalid("Specify --confirm-instance <appId>:wNN. 'bdk dev status' shows the token.") :
        null;

    /// <inheritdoc />
    protected override Task<DevAppInstanceCliOutcome> ExecuteAsync(DevAppInstanceCliService service, CliWorkspaceContext workspace, CancellationToken cancellationToken) =>
        service.ResetAsync(workspace, this.App.Trim(), this.ConfirmInstance.Trim(), cancellationToken);
}

/// <summary>
/// Releases the slot of the current worktree after its instances are stopped and reset.
/// </summary>
/// <example>
/// <code>
/// bdk dev release --confirm-slot 7
/// </code>
/// </example>
public sealed class DevReleaseCliCommand() : DevCliCommandBase("release", "Releases the slot of the current worktree")
{
    /// <summary>Gets or sets the confirmation slot.</summary>
    [ConsoleCommandOption("confirm-slot", Description = "The current slot, as confirmation.")]
    public int ConfirmSlot { get; set; }

    /// <inheritdoc />
    protected override DevAppInstanceCliOutcome Validate() =>
        this.ConfirmSlot <= 0 ? Invalid("Specify --confirm-slot N with the current slot.") : null;

    /// <inheritdoc />
    protected override Task<DevAppInstanceCliOutcome> ExecuteAsync(DevAppInstanceCliService service, CliWorkspaceContext workspace, CancellationToken cancellationToken) =>
        Task.FromResult(service.Release(workspace, this.ConfirmSlot));
}

/// <summary>
/// Lists, and with <c>--confirm</c> drops, the databases of deleted worktrees and frees their slots.
/// </summary>
/// <example>
/// <code>
/// bdk dev prune
/// bdk dev prune --confirm
/// </code>
/// </example>
public sealed class DevPruneCliCommand() : DevCliCommandBase("prune", "Cleans up assignments of deleted worktrees")
{
    /// <summary>Gets or sets a value indicating whether orphaned resources are dropped.</summary>
    [ConsoleCommandOption("confirm", Description = "Drop the orphaned databases and free their slots.")]
    public bool Confirm { get; set; }

    /// <inheritdoc />
    protected override Task<DevAppInstanceCliOutcome> ExecuteAsync(DevAppInstanceCliService service, CliWorkspaceContext workspace, CancellationToken cancellationToken) =>
        service.PruneAsync(workspace, this.Confirm, cancellationToken);
}

/// <summary>
/// Builds and starts DevKit web hosts of the current checkout and streams their prefixed output until Ctrl+C.
/// </summary>
/// <remarks>
/// Without <c>--app</c> the command starts every host it finds: the projects recorded in the registry inventory and the
/// <c>Microsoft.NET.Sdk.Web</c> projects that call <c>DevKitWebApplication.CreateBuilder</c>. The first Ctrl+C asks all
/// hosts to stop and waits up to 30 seconds. A second Ctrl+C terminates them. With <c>--json</c> the host output goes to
/// standard error and the final result to standard output.
/// </remarks>
/// <example>
/// <code>
/// bdk dev run
/// bdk dev run --app forecasthub,forecastadmin
/// </code>
/// </example>
public sealed class DevRunCliCommand() : DevCliCommandBase("run", "Builds and starts the DevKit web hosts of the current checkout")
{
    private readonly object outputLock = new();
    private CancellationTokenSource stopSource;
    private CancellationTokenSource killSource;
    private bool isJson;
    private bool quiet;

    /// <summary>Gets or sets the comma-separated application IDs. All hosts start when omitted.</summary>
    [ConsoleCommandOption("app", Description = "Comma-separated application IDs to start. Starts all hosts when omitted.")]
    public string App { get; set; }

    /// <summary>Gets or sets a value indicating whether the build step is skipped.</summary>
    [ConsoleCommandOption("no-build", Description = "Skip the build and start the last build output.")]
    public bool NoBuild { get; set; }

    /// <inheritdoc />
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var settings = services.GetRequiredService<CliRuntimeContext>().Output;
        this.isJson = settings?.IsJson == true;
        this.quiet = settings?.Quiet == true;
        using var stopSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var killSource = new CancellationTokenSource();
        this.stopSource = stopSource;
        this.killSource = killSource;
        Console.CancelKeyPress += this.OnCancelKeyPress;
        try
        {
            await base.ExecuteAsync(console, services, cancellationToken);
        }
        finally
        {
            Console.CancelKeyPress -= this.OnCancelKeyPress;
            this.stopSource = null;
            this.killSource = null;
        }
    }

    /// <inheritdoc />
    protected override Task<DevAppInstanceCliOutcome> ExecuteAsync(DevAppInstanceCliService service, CliWorkspaceContext workspace, CancellationToken cancellationToken) =>
        service.RunAsync(
            workspace,
            ParseAppIds(this.App),
            this.NoBuild,
            this.Write,
            this.stopSource?.Token ?? cancellationToken,
            this.killSource?.Token ?? CancellationToken.None);

    internal static IReadOnlyList<string> ParseAppIds(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        if (this.stopSource?.IsCancellationRequested == false)
        {
            this.Write("Stopping hosts. Press Ctrl+C again to terminate them.");
            this.stopSource.Cancel();
        }
        else
        {
            this.killSource?.Cancel();
        }
    }

    private void Write(string line)
    {
        if (this.quiet)
        {
            return;
        }

        lock (this.outputLock)
        {
            (this.isJson ? Console.Error : Console.Out).WriteLine(line);
        }
    }
}
