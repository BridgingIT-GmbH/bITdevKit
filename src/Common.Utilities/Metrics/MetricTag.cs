// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a name/value tag attached to a metric measurement.
/// </summary>
/// <param name="Name">The stable metric tag name.</param>
/// <param name="Value">The metric tag value.</param>
/// <example>
/// <code>
/// ReadOnlySpan&lt;MetricTag&gt; tags =
/// [
///     new("storage.operation", "upload"),
///     new("storage.outcome", "success"),
/// ];
/// </code>
/// </example>
public readonly record struct MetricTag(string Name, object Value);
