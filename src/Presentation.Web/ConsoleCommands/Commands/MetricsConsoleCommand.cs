// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

/// <summary>
/// Displays live devkit meter snapshots collected by <see cref="IMetricsSnapshotService" />.
/// </summary>
/// <example>
/// <code>
/// metrics --feature queueing --all
/// metrics --overview
/// </code>
/// </example>
public class MetricsConsoleCommand : ConsoleCommandBase
{
    /// <summary>
    /// Gets or sets the optional feature filter.
    /// </summary>
    [ConsoleCommandOption("feature", Alias = "f", Description = "Filter by feature name")]
    public string Feature { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether raw series tables should be shown.
    /// </summary>
    [ConsoleCommandOption("all", Alias = "a", Description = "Show raw counters/current/durations")]
    public bool All { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether available metric descriptors should be shown.
    /// </summary>
    [ConsoleCommandOption("catalog", Alias = "c", Description = "Show available metric catalog")]
    public bool Catalog { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the dashboard overview projection should be shown.
    /// </summary>
    [ConsoleCommandOption("overview", Alias = "o", Description = "Show dashboard metrics overview")]
    public bool Overview { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of ranked rows to show per table.
    /// </summary>
    [ConsoleCommandOption("take", Alias = "t", Description = "Max ranked rows to show", Default = 10)]
    public int Take { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsConsoleCommand" /> class.
    /// </summary>
    public MetricsConsoleCommand() : base("metrics", "Show devkit meter metrics", "m") { }

    /// <inheritdoc />
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var snapshotService = services.GetService<IMetricsSnapshotService>();
        if (snapshotService is null)
        {
            console.MarkupLine("[yellow]Metrics snapshot service is not registered. Enable metrics with AddMetrics/AddMetricsEndpoints.[/]");
            return Task.CompletedTask;
        }

        var snapshot = snapshotService.GetSnapshot();
        if (this.Overview)
        {
            WriteOverview(console, BuildOverview(snapshot));
            return Task.CompletedTask;
        }

        var features = snapshot.Features.Values
            .Where(feature => string.IsNullOrWhiteSpace(this.Feature) || string.Equals(feature.Name, this.Feature, StringComparison.OrdinalIgnoreCase))
            .OrderBy(feature => feature.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (features.Count == 0)
        {
            console.MarkupLine($"[yellow]No metrics found{(string.IsNullOrWhiteSpace(this.Feature) ? string.Empty : $" for feature '{Markup.Escape(this.Feature)}'")}.[/]");
            WriteAvailableFeatures(console, snapshot);
            return Task.CompletedTask;
        }

        WriteSummary(console, snapshot, features);
        WriteFeatureOverview(console, features);
        WriteRankedTables(console, snapshot, features);

        if (this.All)
        {
            foreach (var feature in features)
            {
                WriteRawFeature(console, feature);
            }
        }

        if (this.Catalog)
        {
            WriteCatalog(console, snapshot);
        }

        return Task.CompletedTask;
    }

    private void WriteOverview(IAnsiConsole console, MetricsOverviewSnapshotModel overview)
    {
        var table = new Table().Border(TableBorder.Minimal).Title("[bold cyan]Metrics Overview[/]");
        table.AddColumn("Metric");
        table.AddColumn("Value");
        table.AddRow("Captured", Markup.Escape(overview.CapturedAtUtc.ToString("u")));
        table.AddRow("Uptime", FormatDuration(TimeSpan.FromSeconds(overview.UptimeSeconds)));
        foreach (var card in overview.SummaryCards.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            table.AddRow(Markup.Escape(card.Key), Format(card.Value));
        }

        console.Write(table);
        WriteOverviewFeatures(console, overview.Features.Values.OrderBy(feature => feature.Name, StringComparer.OrdinalIgnoreCase));
        WriteValueTable(console, "Top Throughput", overview.TopThroughput.Take(this.RowLimit()));
        WriteValueTable(console, "Top Failures", overview.TopFailures.Take(this.RowLimit()));
        WriteValueTable(console, "Top Current", overview.TopCurrent.Take(this.RowLimit()));
        WriteLatencyTable(console, "Latency Highlights", overview.LatencyHighlights.Take(this.RowLimit()));
    }

    private static MetricsOverviewSnapshotModel BuildOverview(MetricsSnapshotModel snapshot)
    {
        var overview = new MetricsOverviewSnapshotModel
        {
            CapturedAtUtc = snapshot.CapturedAtUtc,
            ProcessStartedAtUtc = snapshot.ProcessStartedAtUtc,
            UptimeSeconds = snapshot.UptimeSeconds,
            TopFailures = snapshot.TopFailures,
            TopThroughput = snapshot.TopThroughput,
            TopCurrent = snapshot.TopCurrent,
            LatencyHighlights = snapshot.LatencyHighlights,
            SummaryCards = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["totalSuccess"] = snapshot.Features.Values.Sum(feature => feature.SuccessTotal),
                ["totalFailure"] = snapshot.Features.Values.Sum(feature => feature.FailureTotal),
                ["totalCurrent"] = snapshot.Features.Values.Sum(feature => feature.CurrentTotal),
                ["featureCount"] = snapshot.Features.Count,
                ["latencySeriesCount"] = snapshot.Features.Values.Sum(feature => feature.Durations.Count)
            }
        };

        foreach (var feature in snapshot.Features.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            overview.Features[feature.Key] = new MetricsOverviewFeatureModel
            {
                Name = feature.Value.Name,
                SuccessTotal = feature.Value.SuccessTotal,
                FailureTotal = feature.Value.FailureTotal,
                CurrentTotal = feature.Value.CurrentTotal,
                TopThroughput = feature.Value.TopThroughput.FirstOrDefault(),
                TopCurrent = feature.Value.TopCurrent.FirstOrDefault(),
                SlowestSeries = feature.Value.LatencyHighlights.FirstOrDefault()
            };
        }

        return overview;
    }

    private static void WriteSummary(IAnsiConsole console, MetricsSnapshotModel snapshot, IReadOnlyCollection<BdkFeatureSnapshotModel> features)
    {
        var table = new Table().Border(TableBorder.Minimal).Title("[bold cyan]Devkit Metrics[/]");
        table.AddColumn("Metric");
        table.AddColumn("Value");
        table.AddRow("Meter", Markup.Escape(snapshot.Meter ?? string.Empty));
        table.AddRow("Captured", Markup.Escape(snapshot.CapturedAtUtc.ToString("u")));
        table.AddRow("Uptime", FormatDuration(TimeSpan.FromSeconds(snapshot.UptimeSeconds)));
        table.AddRow("Features", features.Count.ToString());
        table.AddRow("Success Total", Format(features.Sum(feature => feature.SuccessTotal)));
        table.AddRow("Failure Total", Format(features.Sum(feature => feature.FailureTotal)));
        table.AddRow("Current Total", Format(features.Sum(feature => feature.CurrentTotal)));
        console.Write(table);
    }

    private static void WriteFeatureOverview(IAnsiConsole console, IEnumerable<BdkFeatureSnapshotModel> features)
    {
        var table = new Table().Border(TableBorder.Minimal).Title("[bold cyan]Features[/]");
        table.AddColumn("Feature");
        table.AddColumn("Success");
        table.AddColumn("Failures");
        table.AddColumn("Current");
        table.AddColumn("Counters");
        table.AddColumn("Durations");

        foreach (var feature in features)
        {
            table.AddRow(
                Markup.Escape(feature.Name ?? string.Empty),
                Format(feature.SuccessTotal),
                Format(feature.FailureTotal),
                Format(feature.CurrentTotal),
                feature.Counters.Count.ToString(),
                feature.Durations.Count.ToString());
        }

        console.Write(table);
    }

    private static void WriteOverviewFeatures(IAnsiConsole console, IEnumerable<MetricsOverviewFeatureModel> features)
    {
        var rows = features.ToList();
        if (rows.Count == 0)
        {
            return;
        }

        var table = new Table().Border(TableBorder.Minimal).Title("[bold cyan]Overview Features[/]");
        table.AddColumn("Feature");
        table.AddColumn("Success");
        table.AddColumn("Failures");
        table.AddColumn("Current");
        table.AddColumn("Top Throughput");
        table.AddColumn("Top Current");
        table.AddColumn("Slowest");

        foreach (var feature in rows)
        {
            table.AddRow(
                Markup.Escape(feature.Name ?? string.Empty),
                Format(feature.SuccessTotal),
                Format(feature.FailureTotal),
                Format(feature.CurrentTotal),
                FormatMetricValue(feature.TopThroughput),
                FormatMetricValue(feature.TopCurrent),
                FormatLatencyValue(feature.SlowestSeries));
        }

        console.Write(table);
    }

    private void WriteRankedTables(IAnsiConsole console, MetricsSnapshotModel snapshot, IReadOnlyCollection<BdkFeatureSnapshotModel> features)
    {
        var topThroughput = string.IsNullOrWhiteSpace(this.Feature)
            ? snapshot.TopThroughput
            : features.SelectMany(feature => feature.TopThroughput).OrderByDescending(model => model.Value).ThenBy(model => model.Name, StringComparer.Ordinal).ToList();
        var topFailures = string.IsNullOrWhiteSpace(this.Feature)
            ? snapshot.TopFailures
            : features.SelectMany(feature => feature.TopFailures).OrderByDescending(model => model.Value).ThenBy(model => model.Name, StringComparer.Ordinal).ToList();
        var topCurrent = string.IsNullOrWhiteSpace(this.Feature)
            ? snapshot.TopCurrent
            : features.SelectMany(feature => feature.TopCurrent).OrderByDescending(model => model.Value).ThenBy(model => model.Name, StringComparer.Ordinal).ToList();
        var latency = string.IsNullOrWhiteSpace(this.Feature)
            ? snapshot.LatencyHighlights
            : features.SelectMany(feature => feature.LatencyHighlights).OrderByDescending(model => model.Average).ThenByDescending(model => model.Max).ThenBy(model => model.Name, StringComparer.Ordinal).ToList();

        WriteValueTable(console, "Top Throughput", topThroughput.Take(this.RowLimit()));
        WriteValueTable(console, "Top Failures", topFailures.Take(this.RowLimit()));
        WriteValueTable(console, "Top Current", topCurrent.Take(this.RowLimit()));
        WriteLatencyTable(console, "Latency Highlights", latency.Take(this.RowLimit()));
    }

    private static void WriteValueTable(IAnsiConsole console, string title, IEnumerable<MetricValueModel> values)
    {
        var rows = values.ToList();
        if (rows.Count == 0)
        {
            return;
        }

        var table = new Table().Border(TableBorder.Minimal).Title($"[bold cyan]{Markup.Escape(title)}[/]");
        table.AddColumn("Series");
        table.AddColumn("Value");

        foreach (var row in rows)
        {
            table.AddRow(Markup.Escape(row.Name ?? string.Empty), Format(row.Value));
        }

        console.Write(table);
    }

    private static void WriteLatencyTable(IAnsiConsole console, string title, IEnumerable<MetricLatencyModel> values)
    {
        var rows = values.ToList();
        if (rows.Count == 0)
        {
            return;
        }

        var table = new Table().Border(TableBorder.Minimal).Title($"[bold cyan]{Markup.Escape(title)}[/]");
        table.AddColumn("Series");
        table.AddColumn("Count");
        table.AddColumn("Avg ms");
        table.AddColumn("Max ms");

        foreach (var row in rows)
        {
            table.AddRow(
                Markup.Escape(row.Name ?? string.Empty),
                row.Count.ToString(),
                Format(row.Average),
                Format(row.Max));
        }

        console.Write(table);
    }

    private static void WriteRawFeature(IAnsiConsole console, BdkFeatureSnapshotModel feature)
    {
        WriteRawValues(console, $"{feature.Name} Counters", feature.Counters);
        WriteRawValues(console, $"{feature.Name} Current", feature.Current);
        WriteRawDurations(console, $"{feature.Name} Durations", feature.Durations);
    }

    private static void WriteRawValues(IAnsiConsole console, string title, IReadOnlyDictionary<string, double> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        var table = new Table().Border(TableBorder.Minimal).Title($"[bold cyan]{Markup.Escape(title)}[/]");
        table.AddColumn("Series");
        table.AddColumn("Value");
        foreach (var pair in values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            table.AddRow(Markup.Escape(pair.Key), Format(pair.Value));
        }

        console.Write(table);
    }

    private static void WriteRawDurations(IAnsiConsole console, string title, IReadOnlyDictionary<string, BdkHistogramSnapshotModel> durations)
    {
        if (durations.Count == 0)
        {
            return;
        }

        var table = new Table().Border(TableBorder.Minimal).Title($"[bold cyan]{Markup.Escape(title)}[/]");
        table.AddColumn("Series");
        table.AddColumn("Count");
        table.AddColumn("Avg ms");
        table.AddColumn("Max ms");
        foreach (var pair in durations.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            table.AddRow(
                Markup.Escape(pair.Key),
                pair.Value.Count.ToString(),
                Format(pair.Value.Average),
                Format(pair.Value.Max));
        }

        console.Write(table);
    }

    private static void WriteCatalog(IAnsiConsole console, MetricsSnapshotModel snapshot)
    {
        if (snapshot.AvailableMetrics.Count == 0)
        {
            return;
        }

        var table = new Table().Border(TableBorder.Minimal).Title("[bold cyan]Metric Catalog[/]");
        table.AddColumn("Feature");
        table.AddColumn("Kind");
        table.AddColumn("Name");
        table.AddColumn("Unit");
        foreach (var metric in snapshot.AvailableMetrics)
        {
            table.AddRow(
                Markup.Escape(metric.Feature ?? string.Empty),
                Markup.Escape(metric.Kind ?? string.Empty),
                Markup.Escape(metric.Name ?? string.Empty),
                Markup.Escape(metric.Unit ?? string.Empty));
        }

        console.Write(table);
    }

    private static void WriteAvailableFeatures(IAnsiConsole console, MetricsSnapshotModel snapshot)
    {
        if (snapshot.Features.Count == 0)
        {
            return;
        }

        console.MarkupLine($"Available features: [cyan]{Markup.Escape(string.Join(", ", snapshot.Features.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)))}[/]");
    }

    private int RowLimit() => Math.Max(1, this.Take);

    private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string FormatMetricValue(MetricValueModel value) =>
        value is null ? string.Empty : $"{Markup.Escape(value.Name ?? string.Empty)} ({Format(value.Value)})";

    private static string FormatLatencyValue(MetricLatencyModel value) =>
        value is null ? string.Empty : $"{Markup.Escape(value.Name ?? string.Empty)} ({Format(value.Average)} ms avg)";

    private static string FormatDuration(TimeSpan value) => $"{(int)value.TotalHours:D2}:{value.Minutes:D2}:{value.Seconds:D2}";
}
