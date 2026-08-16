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
/// <param name="Count">The number of values to process.</param>
public sealed record OrchestrationDefinitionSummary(string Name, long Count);

/// <summary>
/// Represents orchestration state summary.
/// </summary>
/// <param name="Name">The name of the value.</param>
/// <param name="Count">The number of values to process.</param>
public sealed record OrchestrationStateSummary(string Name, long Count);
