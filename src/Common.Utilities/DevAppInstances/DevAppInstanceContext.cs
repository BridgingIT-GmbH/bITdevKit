// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Describes one startup log step recorded by the resolver and written later through the host logger.
/// </summary>
/// <param name="Template">The structured message template, without the log key prefix.</param>
/// <param name="Arguments">The template arguments.</param>
/// <example>
/// <code>
/// logger.LogInformation("[{LogKey}] worktree " + step.Template, ["BDK", .. step.Arguments]);
/// </code>
/// </example>
public sealed record DevAppInstanceLogStep(string Template, object[] Arguments);

/// <summary>
/// The immutable development instance context resolved once per host startup.
/// </summary>
/// <remarks>
/// An inactive context has <see cref="IsActive"/> set to <c>false</c>, reports <see cref="InactiveReason"/> and never carries
/// a worktree ID, slot, URL or path. Use <see cref="ToStatus"/> for the sanitized
/// projection. The full context contains local paths and managed values and is never sent to a browser.
/// </remarks>
/// <example>
/// <code>
/// var context = app.Services.GetRequiredService&lt;DevAppInstanceContext&gt;();
/// if (context.IsActive)
/// {
///     Console.WriteLine($"{context.AppId} slot {context.Slot}: {context.HttpsUrl}");
/// }
/// </code>
/// </example>
public sealed record DevAppInstanceContext
{
    /// <summary>Gets a value indicating whether linked-worktree isolation is active.</summary>
    public bool IsActive { get; init; }

    /// <summary>Gets the inactive reason, or <c>null</c> when active.</summary>
    public string InactiveReason { get; init; }

    /// <summary>Gets the sanitized failure message when isolation fell back to the configured defaults, otherwise <c>null</c>.</summary>
    public string FailureMessage { get; init; }

    /// <summary>Gets the error code of the failure that caused the fallback, otherwise <c>null</c>.</summary>
    public string FailureCode { get; init; }

    /// <summary>Gets how the slot was obtained.</summary>
    public DevAppInstanceSelectionReason SelectionReason { get; init; }

    /// <summary>Gets the application ID.</summary>
    public string AppId { get; init; }

    /// <summary>Gets the stable worktree ID.</summary>
    public Guid? WorktreeId { get; init; }

    /// <summary>Gets the slot from 1 to the policy slot count.</summary>
    public int? Slot { get; init; }

    /// <summary>Gets the slot formatted as <c>wNN</c>.</summary>
    public string SlotName => this.Slot is { } slot ? $"w{slot:00}" : null;

    /// <summary>Gets the instance name formatted as <c>appId:wNN</c>.</summary>
    public string InstanceName => this.IsActive ? $"{this.AppId}:{this.SlotName}" : null;

    /// <summary>Gets the canonical worktree root.</summary>
    public string WorktreeRoot { get; init; }

    /// <summary>Gets the canonical common Git directory.</summary>
    public string CommonDirectory { get; init; }

    /// <summary>Gets the resolved HTTP port.</summary>
    public int? HttpPort { get; init; }

    /// <summary>Gets the resolved HTTPS port.</summary>
    public int? HttpsPort { get; init; }

    /// <summary>Gets the resolved HTTP URL.</summary>
    public string HttpUrl => this.HttpPort is { } port ? $"http://localhost:{port}" : null;

    /// <summary>Gets the resolved HTTPS URL, which is also the canonical public origin.</summary>
    public string HttpsUrl => this.HttpsPort is { } port ? $"https://localhost:{port}" : null;

    /// <summary>Gets the canonical public origin.</summary>
    public string PublicOrigin => this.HttpsUrl;

    /// <summary>Gets the application state root <c>.dev/state/&lt;appId&gt;</c>.</summary>
    public string StateRoot { get; init; }

    /// <summary>Gets the payload storage root.</summary>
    public string PayloadRoot => this.StateRoot is null ? null : Path.Combine(this.StateRoot, "payload");

    /// <summary>Gets the log root.</summary>
    public string LogsRoot => this.StateRoot is null ? null : Path.Combine(this.StateRoot, "logs");

    /// <summary>Gets the data protection key ring directory.</summary>
    public string KeysRoot => this.StateRoot is null ? null : Path.Combine(this.StateRoot, "keys");

    /// <summary>Gets the data protection application name <c>appId.wNN</c>.</summary>
    public string DataProtectionApplicationName => this.IsActive ? $"{this.AppId}.{this.SlotName}" : null;

    /// <summary>Gets the dashboard cookie name.</summary>
    public string DashboardCookieName => this.IsActive ? $".Bdk.Dashboard.{this.AppId}.{this.SlotName}" : null;

    /// <summary>Gets the fake identity provider cookie name.</summary>
    public string IdentityProviderCookieName => this.IsActive ? $".Bdk.FakeIdentityProvider.{this.AppId}.{this.SlotName}" : null;

    /// <summary>Gets the resolved resource plans.</summary>
    public IReadOnlyList<DevAppInstanceResourcePlan> Resources { get; init; } = [];

    /// <summary>Gets the managed configuration values. May contain secrets and is never logged.</summary>
    public IReadOnlyDictionary<string, string> ManagedValues { get; init; } = new Dictionary<string, string>();

    /// <summary>Gets the recorded startup log steps.</summary>
    public IReadOnlyList<DevAppInstanceLogStep> LogSteps { get; init; } = [];

    /// <summary>Gets the lease file path, when a lease is held.</summary>
    public string LeasePath { get; init; }

    /// <summary>
    /// Creates an inactive context.
    /// </summary>
    /// <param name="reason">The inactive reason from <see cref="DevAppInstanceInactiveReasons"/>.</param>
    /// <param name="appId">The optional application ID.</param>
    /// <returns>The inactive context.</returns>
    public static DevAppInstanceContext Inactive(string reason, string appId = null) =>
        new() { IsActive = false, InactiveReason = reason, AppId = appId, SelectionReason = DevAppInstanceSelectionReason.None };

    /// <summary>
    /// Creates the inactive context of a host that fell back to its configured defaults after isolation failed. The
    /// context carries one warning log step that names the failure and the recovery action.
    /// </summary>
    /// <param name="exception">The failure that stopped isolation.</param>
    /// <param name="appId">The optional application ID.</param>
    /// <returns>The inactive context with the reason <see cref="DevAppInstanceInactiveReasons.Fallback"/>.</returns>
    /// <example>
    /// <code>
    /// var context = DevAppInstanceContext.Fallback(new DevAppInstanceException(DevAppInstanceErrorCodes.ConfigurationConflict, "Port in use."), "forecasthub");
    /// </code>
    /// </example>
    public static DevAppInstanceContext Fallback(DevAppInstanceException exception, string appId = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        appId ??= exception.AppId;

        return new()
        {
            IsActive = false,
            InactiveReason = DevAppInstanceInactiveReasons.Fallback,
            AppId = appId,
            SelectionReason = DevAppInstanceSelectionReason.None,
            FailureMessage = exception.Message,
            FailureCode = exception.Code,
            LogSteps =
            [
                new(
                    "[warning] isolation failed, the host uses its configured ports and databases (error={ErrorMessage}, code={ErrorCode}, app={AppId}, slot={Slot}) {RecoveryAction}",
                    [exception.Message, exception.Code, appId, exception.Slot, exception.RecoveryAction ?? string.Empty]),
            ],
        };
    }

    /// <summary>
    /// Builds the instance database name <c>&lt;baseline&gt;_wNN</c> from the database name of the baseline configuration.
    /// </summary>
    /// <param name="baselineName">The database name in the baseline connection string, for example <c>ForecastHub</c>.</param>
    /// <returns>The instance database name, for example <c>ForecastHub_w01</c>.</returns>
    /// <example>
    /// <code>
    /// var name = context.GetInstanceDatabaseName("ForecastHub"); // ForecastHub_w01 in slot 1
    /// </code>
    /// </example>
    public string GetInstanceDatabaseName(string baselineName) => $"{baselineName}_{this.SlotName}";

    /// <summary>
    /// Returns the sanitized status projection. It contains no credentials or connection strings.
    /// </summary>
    /// <returns>The status projection.</returns>
    public DevAppInstanceStatus ToStatus() => new(
        1,
        this.IsActive ? "active" : "inactive",
        this.InactiveReason,
        this.AppId,
        this.WorktreeId,
        this.Slot,
        this.SelectionReason.ToString().ToLowerInvariant(),
        this.HttpUrl,
        this.HttpsUrl,
        this.Resources.Select(r => new DevAppInstanceStatusResource(r.ResourceId, r.Kind, r.Target, r.Shared)).ToList());
}

/// <summary>
/// The sanitized machine-readable status projection of a development instance.
/// </summary>
/// <param name="SchemaVersion">The schema version.</param>
/// <param name="Mode">The mode, <c>active</c> or <c>inactive</c>.</param>
/// <param name="Reason">The inactive reason.</param>
/// <param name="AppId">The application ID.</param>
/// <param name="WorktreeId">The worktree ID.</param>
/// <param name="Slot">The slot.</param>
/// <param name="Selection">The selection reason.</param>
/// <param name="HttpUrl">The HTTP URL.</param>
/// <param name="HttpsUrl">The HTTPS URL.</param>
/// <param name="Resources">The sanitized resources.</param>
/// <example>
/// <code>
/// var json = JsonSerializer.Serialize(context.ToStatus());
/// </code>
/// </example>
public sealed record DevAppInstanceStatus(
    int SchemaVersion,
    string Mode,
    string Reason,
    string AppId,
    Guid? WorktreeId,
    int? Slot,
    string Selection,
    string HttpUrl,
    string HttpsUrl,
    IReadOnlyList<DevAppInstanceStatusResource> Resources);

/// <summary>
/// One sanitized resource of the status projection.
/// </summary>
/// <param name="ResourceId">The resource ID.</param>
/// <param name="Kind">The adapter kind.</param>
/// <param name="Target">The sanitized target.</param>
/// <param name="Shared">Whether the resource is intentionally shared.</param>
/// <example>
/// <code>
/// Console.WriteLine($"{resource.ResourceId} -> {resource.Target}");
/// </code>
/// </example>
public sealed record DevAppInstanceStatusResource(string ResourceId, string Kind, string Target, bool Shared);
