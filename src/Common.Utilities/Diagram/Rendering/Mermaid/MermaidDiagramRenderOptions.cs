// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents Mermaid-specific render options.
/// </summary>
/// <example>
/// <code>
/// var options = new MermaidDiagramRenderOptions
/// {
///     IncludeHeader = true,
///     InitDirective = "{ \"theme\": \"neutral\" }"
/// };
/// </code>
/// </example>
public sealed class MermaidDiagramRenderOptions : DiagramRenderOptions
{
    /// <summary>
    /// Gets or sets the optional Mermaid init directive payload.
    /// </summary>
    public string InitDirective { get; init; }
}