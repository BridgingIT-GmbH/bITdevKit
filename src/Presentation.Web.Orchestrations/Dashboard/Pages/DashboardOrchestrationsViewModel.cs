// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Orchestrations.Dashboard.Pages;

using BridgingIT.DevKit.Application.Orchestrations;

/// <summary>
/// View model for the server-rendered Orchestrations dashboard content.
/// </summary>
/// <example>
/// <code>
/// var model = new DashboardOrchestrationsViewModel();
/// </code>
/// </example>
public sealed class DashboardOrchestrationsViewModel
{
    /// <summary>
    /// Gets or sets the captured at utc.
    /// </summary>
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the action base.
    /// </summary>
    public string ActionBase { get; set; } = "/_bdk/dashboard/orchestrations";

    /// <summary>
    /// Gets or sets the metrics.
    /// </summary>
    public OrchestrationMetricsModel Metrics { get; set; } = new();

    /// <summary>
    /// Gets or sets the instances.
    /// </summary>
    public IReadOnlyList<OrchestrationInstanceModel> Instances { get; set; } = [];

    /// <summary>Gets or sets the selected number of instances per load.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Gets or sets whether the bounded query found another instance.</summary>
    public bool HasMore { get; set; }

    /// <summary>Gets or sets the next immutable instance cursor.</summary>
    public string NextCursor { get; set; }

    /// <summary>Gets or sets the initial browsing boundary.</summary>
    public string Boundary { get; set; }

    /// <summary>Gets or sets the matching count before this batch is trimmed.</summary>
    public long MatchingCount { get; set; }

    /// <summary>
    /// Gets or sets the latest context snapshots keyed by orchestration instance id.
    /// </summary>
    public IReadOnlyDictionary<Guid, OrchestrationContextSnapshotModel> ContextsByInstanceId { get; set; } = new Dictionary<Guid, OrchestrationContextSnapshotModel>();

    /// <summary>
    /// Gets or sets the persisted history entries keyed by orchestration instance id.
    /// </summary>
    public IReadOnlyDictionary<Guid, IReadOnlyList<OrchestrationHistoryModel>> HistoryByInstanceId { get; set; } = new Dictionary<Guid, IReadOnlyList<OrchestrationHistoryModel>>();

    /// <summary>
    /// Gets or sets the persisted signal records keyed by orchestration instance id.
    /// </summary>
    public IReadOnlyDictionary<Guid, IReadOnlyList<OrchestrationSignalModel>> SignalsByInstanceId { get; set; } = new Dictionary<Guid, IReadOnlyList<OrchestrationSignalModel>>();

    /// <summary>
    /// Gets or sets the persisted timer records keyed by orchestration instance id.
    /// </summary>
    public IReadOnlyDictionary<Guid, IReadOnlyList<OrchestrationTimerModel>> TimersByInstanceId { get; set; } = new Dictionary<Guid, IReadOnlyList<OrchestrationTimerModel>>();

    /// <summary>
    /// Gets or sets the counts by status.
    /// </summary>
    public IReadOnlyDictionary<string, long> CountsByStatus { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the definitions.
    /// </summary>
    public IReadOnlyList<OrchestrationDefinitionSummary> Definitions { get; set; } = [];

    /// <summary>
    /// Gets or sets the states.
    /// </summary>
    public IReadOnlyList<OrchestrationStateSummary> States { get; set; } = [];

    /// <summary>
    /// Gets the errors.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Gets or sets the is available.
    /// </summary>
    public bool IsAvailable { get; set; } = true;
}

/// <summary>
/// Represents orchestration definition summary.
/// </summary>
/// <param name="Name">The name of the value.</param>
/// <param name="TypeName">The registered orchestration type.</param>
public sealed record OrchestrationDefinitionSummary(string Name, string TypeName)
{
    /// <summary>Gets the readable orchestration type label.</summary>
    public string TypeLabel { get; init; }

    /// <summary>Gets the readable data type label.</summary>
    public string DataTypeLabel { get; init; }

    /// <summary>Gets the definition data type.</summary>
    public string DataTypeName { get; init; }

    /// <summary>Gets the initial state.</summary>
    public string InitialState { get; init; }

    /// <summary>Gets the authored states and their activities.</summary>
    public IReadOnlyList<OrchestrationDefinitionState> States { get; init; } = [];

    /// <summary>Gets a readable metadata error without removing the catalog entry.</summary>
    public string Error { get; init; }
}

/// <summary>Describes a registered state without executing its activities or conditions.</summary>
/// <param name="Name">The state name.</param>
/// <param name="Activities">Activities in authored order, including signal activities.</param>
/// <param name="Transitions">Outgoing transitions, signals and timers.</param>
/// <param name="Directive">The configured terminal or wait directive.</param>
public sealed record OrchestrationDefinitionState(string Name, IReadOnlyList<string> Activities, IReadOnlyList<string> Transitions, string Directive);

/// <summary>
/// Represents orchestration state summary.
/// </summary>
/// <param name="Name">The name of the value.</param>
/// <param name="Count">The number of values to process.</param>
public sealed record OrchestrationStateSummary(string Name, long Count);
