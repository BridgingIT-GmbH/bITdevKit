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
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string ActionBase { get; set; } = "/_bdk/dashboard/orchestrations";

    public OrchestrationMetricsModel Metrics { get; set; } = new();

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

    public IReadOnlyDictionary<string, long> CountsByStatus { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<OrchestrationDefinitionSummary> Definitions { get; set; } = [];

    public IReadOnlyList<OrchestrationStateSummary> States { get; set; } = [];

    public List<string> Errors { get; } = [];

    public bool IsAvailable { get; set; } = true;
}

public sealed record OrchestrationDefinitionSummary(string Name, long Count);

public sealed record OrchestrationStateSummary(string Name, long Count);
