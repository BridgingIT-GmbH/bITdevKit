// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Represents the typed context for an import completion interceptor.
/// </summary>
/// <typeparam name="TTarget">The imported item type.</typeparam>
public sealed class ImportCompletionContext<TTarget>
    where TTarget : class
{
    /// <summary>
    /// Gets the completed import result.
    /// </summary>
    public required ImportResult<TTarget> Result { get; init; }

    /// <summary>
    /// Gets the import format.
    /// </summary>
    public required Format Format { get; init; }

    /// <summary>
    /// Gets the sheet or section name when available.
    /// </summary>
    public string SheetName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation is streaming.
    /// </summary>
    public required bool IsStreaming { get; init; }

    /// <summary>
    /// Gets the operation item bag for interceptor coordination.
    /// </summary>
    public IDictionary<string, object> Items { get; init; } = new Dictionary<string, object>();
}
