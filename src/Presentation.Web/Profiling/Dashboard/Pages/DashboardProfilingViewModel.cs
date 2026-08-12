// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Profiling.Dashboard.Pages;

using BridgingIT.DevKit.Common;

/// <summary>Contains the server-rendered state and endpoint paths for the Profiling dashboard.</summary>
/// <example><code>var selected = model.SelectedNode?.LatestSnapshot;</code></example>
public sealed class DashboardProfilingViewModel
{
    /// <summary>Gets or sets the current feature and active-session status.</summary>
    public ProfilingStatus Status { get; set; }

    /// <summary>Gets or sets stored sessions in provider order.</summary>
    public IReadOnlyList<ProfilingSession> Sessions { get; set; } = [];

    /// <summary>Gets or sets the complete selected-session data.</summary>
    public ProfilingSessionData SelectedSession { get; set; }

    /// <summary>Gets or sets the selected-node timeline.</summary>
    public ProfilingNodeSessionData SelectedNode { get; set; }

    /// <summary>Gets or sets the selected public session key.</summary>
    public string SessionKey { get; set; }

    /// <summary>Gets or sets the selected public node key.</summary>
    public string NodeKey { get; set; }

    /// <summary>Gets or sets the explicitly selected snapshot key; null follows the latest snapshot.</summary>
    public string SnapshotKey { get; set; }

    /// <summary>Gets or sets a safe dashboard loading error.</summary>
    public string Error { get; set; }

    /// <summary>Gets or sets the configured refresh interval in milliseconds.</summary>
    public int RefreshIntervalMilliseconds { get; set; } = 5000;

    /// <summary>Gets or sets the profiling page path.</summary>
    public string PagePath { get; set; }

    /// <summary>Gets or sets the refreshable content path.</summary>
    public string ContentPath { get; set; }

    /// <summary>Gets or sets the start action path.</summary>
    public string StartPath { get; set; }

    /// <summary>Gets or sets the stop action path.</summary>
    public string StopPath { get; set; }

    /// <summary>Gets or sets the manual snapshot action path.</summary>
    public string SnapshotPath { get; set; }

    /// <summary>Gets or sets the manual garbage-collection action path.</summary>
    public string GarbageCollectionPath { get; set; }

    /// <summary>Gets or sets the fixed host-local stress action path.</summary>
    public string StressPath { get; set; }

    /// <summary>Gets or sets the phase-marker action path.</summary>
    public string MarkerPath { get; set; }

    /// <summary>Gets or sets the metadata update path.</summary>
    public string MetadataPath { get; set; }

    /// <summary>Gets or sets the selected-session deletion path.</summary>
    public string DeleteSessionPath { get; set; }

    /// <summary>Gets or sets the bulk unpinned-session deletion path.</summary>
    public string DeleteUnpinnedPath { get; set; }

    /// <summary>Gets or sets the complete-store reset path.</summary>
    public string ClearPath { get; set; }

    /// <summary>Gets or sets the raw snapshot comparison path.</summary>
    public string ComparePath { get; set; }

    /// <summary>Gets or sets the deterministic analysis path.</summary>
    public string AnalyzePath { get; set; }

    /// <summary>Gets or sets the selected-node raw JSON export path.</summary>
    public string NodeExportPath { get; set; }

    /// <summary>Gets or sets the import-compatible complete-session JSON export path.</summary>
    public string SessionExportPath { get; set; }

    /// <summary>Gets or sets the complete-session portable archive download path.</summary>
    public string SessionArchivePath { get; set; }

    /// <summary>Gets or sets the one-way Perfetto trace export path.</summary>
    public string SessionPerfettoPath { get; set; }

    /// <summary>Gets or sets the latest selected-snapshot portable archive download path.</summary>
    public string SnapshotArchivePath { get; set; }

    /// <summary>Gets archive download paths by selected-node snapshot key.</summary>
    public IReadOnlyDictionary<string, string> SnapshotArchivePaths { get; set; } =
        new Dictionary<string, string>();

    /// <summary>Gets or sets the browser archive upload path.</summary>
    public string ArchiveImportPath { get; set; }

    /// <summary>Gets the selectable expected and ad-hoc node keys.</summary>
    public IReadOnlyList<string> NodeKeys =>
        this.SelectedSession is null
            ? []
            : this
                .SelectedSession.Participations.Select(item => item.NodeKey)
                .Concat(this.SelectedSession.Nodes.Select(item => item.Identity.Key))
                .Concat(this.SelectedSession.Snapshots.Select(item => item.NodeKey))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
}