// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

/// <summary>Identifies an aggregate event counter or millisecond duration histogram.</summary>
/// <param name="InstrumentName">The exact instrument name; typed copies are excluded.</param>
/// <param name="Label">The visible series label.</param>
/// <param name="IsDuration">Whether samples are averaged instead of converted to events per second.</param>
/// <example><code>new DashboardWorkloadSeries("jobs_execution_duration", "Execution", true);</code></example>
public sealed record DashboardWorkloadSeries(string InstrumentName, string Label, bool IsDuration = false);

/// <summary>Describes a workload chart and its units.</summary>
/// <param name="Title">The chart title.</param>
/// <param name="Unit">The vertical axis unit.</param>
/// <param name="Series">The measured series.</param>
/// <example><code>new DashboardWorkloadChart("Execution rate", "executions/s", series);</code></example>
public sealed record DashboardWorkloadChart(string Title, string Unit, IReadOnlyList<DashboardWorkloadSeries> Series);

/// <summary>Configures the shared workload diagnostics renderer.</summary>
/// <param name="Prefix">The owning feature's existing refresh-control prefix.</param>
/// <param name="DataUrl">The JSON snapshot URL.</param>
/// <param name="Charts">The chart definitions.</param>
/// <param name="HistoryUrl">An optional retained history URL, separate from runtime measurements.</param>
/// <param name="HistoryTitle">The retained-history panel label.</param>
/// <param name="HistoryUnit">The retained item label used in axes and status.</param>
/// <param name="HistorySeries">Optional retained count field names; defaults to completed, failed and running.</param>
/// <example><code>new DashboardWorkloadChartsModel("jobs", "/ops/jobs/data/realtime", charts);</code></example>
public sealed record DashboardWorkloadChartsModel(string Prefix, string DataUrl, IReadOnlyList<DashboardWorkloadChart> Charts, string HistoryUrl = null, string HistoryTitle = "Retained job history", string HistoryUnit = "occurrences", IReadOnlyList<string> HistorySeries = null);
