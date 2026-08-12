// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Profiling.Models;

using BridgingIT.DevKit.Common;

/// <summary>Defines a dashboard request to start one profiling session.</summary>
/// <param name="Name">The optional session name.</param>
/// <param name="SamplingInterval">The optional sampling interval override.</param>
/// <param name="Duration">The optional collection duration override.</param>
/// <param name="Tags">The optional plain session tags.</param>
/// <example><code>var request = new ProfilingDashboardStartRequest("warm-up", duration: TimeSpan.FromSeconds(30));</code></example>
public sealed record ProfilingDashboardStartRequest(
    string Name = null,
    TimeSpan? SamplingInterval = null,
    TimeSpan? Duration = null,
    IReadOnlyList<string> Tags = null
);

/// <summary>Defines a dashboard request for a manual snapshot.</summary>
/// <param name="Name">The optional standalone session name.</param>
/// <example><code>var request = new ProfilingDashboardSnapshotRequest("checkpoint");</code></example>
public sealed record ProfilingDashboardSnapshotRequest(string Name = null);

/// <summary>Defines a dashboard request to add an active-session marker.</summary>
/// <param name="Name">The required marker name.</param>
/// <example><code>var request = new ProfilingDashboardMarkerRequest("load started");</code></example>
public sealed record ProfilingDashboardMarkerRequest(string Name);

/// <summary>Defines editable dashboard session metadata.</summary>
/// <param name="Name">The optional display name.</param>
/// <param name="Tags">The complete plain-tag replacement.</param>
/// <param name="Note">The optional note.</param>
/// <param name="IsPinned">Whether retention excludes the session.</param>
/// <example><code>var request = new ProfilingDashboardMetadataRequest("run", ["local"], null, true);</code></example>
public sealed record ProfilingDashboardMetadataRequest(
    string Name,
    IReadOnlyList<string> Tags,
    string Note,
    bool IsPinned
);

/// <summary>Defines explicit confirmation for the destructive profiling reset.</summary>
/// <param name="Confirmed">Whether the user confirmed removal including pinned sessions.</param>
/// <example><code>var request = new ProfilingDashboardClearRequest(true);</code></example>
public sealed record ProfilingDashboardClearRequest(bool Confirmed);

/// <summary>Defines an exact two-snapshot comparison selection.</summary>
/// <param name="SessionKey">The public session key.</param>
/// <param name="NodeKey">The public node key.</param>
/// <param name="SnapshotAKey">The earlier public snapshot key.</param>
/// <param name="SnapshotBKey">The later public snapshot key.</param>
/// <example><code>var request = new ProfilingDashboardCompareRequest("sess0001", "node0001", "snap0001", "snap0002");</code></example>
public sealed record ProfilingDashboardCompareRequest(
    string SessionKey,
    string NodeKey,
    string SnapshotAKey,
    string SnapshotBKey
);

/// <summary>Defines an unpersisted dashboard analysis selection.</summary>
/// <param name="SessionKey">The public session key.</param>
/// <param name="NodeKey">The public node key.</param>
/// <param name="SnapshotAKey">The optional earlier public snapshot key.</param>
/// <param name="SnapshotBKey">The optional later public snapshot key.</param>
/// <example><code>var request = new ProfilingDashboardAnalyzeRequest("sess0001", "node0001");</code></example>
public sealed record ProfilingDashboardAnalyzeRequest(
    string SessionKey,
    string NodeKey,
    string SnapshotAKey = null,
    string SnapshotBKey = null
);

/// <summary>Contains the dashboard status, session list, and optional selected data.</summary>
/// <param name="Status">The current profiling availability and active-session status.</param>
/// <param name="Sessions">The stored session summaries.</param>
/// <param name="SelectedSession">The optional complete selected session.</param>
/// <param name="SelectedNode">The optional selected-node timeline.</param>
/// <example><code>var selected = response.SelectedNode;</code></example>
public sealed record ProfilingDashboardDataResponse(
    ProfilingStatus Status,
    IReadOnlyList<ProfilingSession> Sessions,
    ProfilingSessionData SelectedSession,
    ProfilingNodeSessionData SelectedNode
);