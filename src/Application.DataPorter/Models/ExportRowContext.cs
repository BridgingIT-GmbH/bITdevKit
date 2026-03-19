// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Represents the typed context for a row-level export interceptor.
/// </summary>
/// <typeparam name="TSource">The exported item type.</typeparam>
public sealed class ExportRowContext<TSource>
    where TSource : class
{
    /// <summary>
    /// Gets or sets the current item.
    /// </summary>
    public required TSource Item { get; set; }

    /// <summary>
    /// Gets the logical row number.
    /// </summary>
    public required int RowNumber { get; init; }

    /// <summary>
    /// Gets the export format.
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
    /// Gets the per-row item bag for interceptor coordination.
    /// </summary>
    public IDictionary<string, object> Items { get; init; } = new Dictionary<string, object>();
}
