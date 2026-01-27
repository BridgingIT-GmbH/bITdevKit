// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.DataPorter;

/// <summary>
/// Represents a data set for export operations with multiple sheets/sections.
/// </summary>
public sealed record ExportDataSet
{
    /// <summary>
    /// Gets or sets the data to export.
    /// </summary>
    public required IEnumerable<object> Data { get; init; }

    /// <summary>
    /// Gets or sets the type of the data items.
    /// </summary>
    public required Type ItemType { get; init; }

    /// <summary>
    /// Gets or sets the sheet/section name.
    /// </summary>
    public required string SheetName { get; init; }

    /// <summary>
    /// Creates an ExportDataSet from a typed collection.
    /// </summary>
    /// <typeparam name="T">The type of the data items.</typeparam>
    /// <param name="data">The data to export.</param>
    /// <param name="sheetName">The sheet/section name.</param>
    /// <returns>A new ExportDataSet instance.</returns>
    public static ExportDataSet Create<T>(IEnumerable<T> data, string sheetName)
        where T : class
    {
        return new ExportDataSet
        {
            Data = data.Cast<object>(),
            ItemType = typeof(T),
            SheetName = sheetName
        };
    }
}
