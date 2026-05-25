// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

/// <summary>
/// Represents options used when projecting orchestration instance diagrams.
/// </summary>
public sealed class OrchestrationDiagramOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether persisted execution history should be included.
    /// </summary>
    public bool IncludeHistory { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether persisted signals should be reflected in the diagram.
    /// </summary>
    public bool IncludeSignals { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether persisted timers should be reflected in the diagram.
    /// </summary>
    public bool IncludeTimers { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether completed activities should be reflected as notes.
    /// </summary>
    public bool IncludeActivities { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the current state should be highlighted.
    /// </summary>
    public bool HighlightCurrentState { get; init; } = true;
}