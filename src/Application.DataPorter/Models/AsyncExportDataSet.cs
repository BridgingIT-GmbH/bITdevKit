// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Represents an asynchronous data set for export operations with multiple sheets/sections.
/// </summary>
public sealed record AsyncExportDataSet
{
    /// <summary>
    /// Gets or sets the data to export.
    /// </summary>
    public required IAsyncEnumerable<object> Data { get; init; }

    /// <summary>
    /// Gets or sets the type of the data items.
    /// </summary>
    public required Type ItemType { get; init; }

    /// <summary>
    /// Gets or sets the sheet/section name.
    /// </summary>
    public required string SheetName { get; init; }

    /// <summary>
    /// Creates an AsyncExportDataSet from a typed asynchronous collection.
    /// </summary>
    /// <typeparam name="T">The type of the data items.</typeparam>
    /// <param name="data">The data to export.</param>
    /// <param name="sheetName">The sheet/section name.</param>
    /// <returns>A new AsyncExportDataSet instance.</returns>
    public static AsyncExportDataSet Create<T>(IAsyncEnumerable<T> data, string sheetName)
        where T : class
    {
        return new AsyncExportDataSet
        {
            Data = CastAsync(data),
            ItemType = typeof(T),
            SheetName = sheetName
        };
    }

    private static async IAsyncEnumerable<object> CastAsync<T>(IAsyncEnumerable<T> data)
        where T : class
    {
        await foreach (var item in data)
        {
            yield return item;
        }
    }
}
