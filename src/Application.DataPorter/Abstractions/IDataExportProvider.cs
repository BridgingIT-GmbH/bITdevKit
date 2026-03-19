// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Runtime.CompilerServices;

/// <summary>
/// Provider interface for export operations.
/// </summary>
public interface IDataExportProvider : IDataPorterProvider
{
    /// <summary>
    /// Exports data to a stream.
    /// </summary>
    Task<ExportResult> ExportAsync<TSource>(
        IEnumerable<TSource> data,
        Stream outputStream,
        ExportConfiguration configuration,
        CancellationToken cancellationToken = default)
        where TSource : class;

    /// <summary>
    /// Exports asynchronous data to a stream.
    /// </summary>
    Task<ExportResult> ExportAsync<TSource>(
        IAsyncEnumerable<TSource> data,
        Stream outputStream,
        ExportConfiguration configuration,
        CancellationToken cancellationToken = default)
        where TSource : class;

    /// <summary>
    /// Exports multiple data sets to a stream.
    /// </summary>
    Task<ExportResult> ExportAsync(
        IEnumerable<(IEnumerable<object> Data, ExportConfiguration Configuration)> dataSets,
        Stream outputStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports multiple asynchronous data sets to a stream.
    /// </summary>
    Task<ExportResult> ExportAsync(
        IEnumerable<(IAsyncEnumerable<object> Data, ExportConfiguration Configuration)> dataSets,
        Stream outputStream,
        CancellationToken cancellationToken = default);
}
