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
/// Provider interface for import operations.
/// </summary>
public interface IDataImportProvider : IDataPorterProvider
{
    /// <summary>
    /// Imports data from a stream.
    /// </summary>
    Task<ImportResult<TTarget>> ImportAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration configuration,
        CancellationToken cancellationToken = default)
        where TTarget : class, new();

    /// <summary>
    /// Streams import results for large files.
    /// </summary>
    IAsyncEnumerable<Result<TTarget>> ImportStreamAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration configuration,
        CancellationToken cancellationToken = default)
        where TTarget : class, new();

    /// <summary>
    /// Validates import data without importing.
    /// </summary>
    Task<ValidationResult> ValidateAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration configuration,
        CancellationToken cancellationToken = default)
        where TTarget : class, new();
}
