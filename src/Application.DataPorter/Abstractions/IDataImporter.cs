// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using BridgingIT.DevKit.Common;

/// <summary>
/// Defines the contract for importing data from various formats.
/// </summary>
public interface IDataImporter
{
    /// <summary>
    /// Imports data from a stream.
    /// </summary>
    /// <typeparam name="TTarget">The type of the target data.</typeparam>
    /// <param name="inputStream">The stream to read the data from.</param>
    /// <param name="options">Optional import options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the imported data or error details.</returns>
    Task<Result<ImportResult<TTarget>>> ImportAsync<TTarget>(
        Stream inputStream,
        ImportOptions options = null,
        CancellationToken cancellationToken = default)
        where TTarget : class, new();

    /// <summary>
    /// Imports data from a byte array.
    /// </summary>
    /// <typeparam name="TTarget">The type of the target data.</typeparam>
    /// <param name="data">The byte array containing the data to import.</param>
    /// <param name="options">Optional import options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the imported data or error details.</returns>
    Task<Result<ImportResult<TTarget>>> ImportFromBytesAsync<TTarget>(
        byte[] data,
        ImportOptions options = null,
        CancellationToken cancellationToken = default)
        where TTarget : class, new();

    /// <summary>
    /// Streams import results for large files (yields rows as they're parsed).
    /// </summary>
    /// <typeparam name="TTarget">The type of the target data.</typeparam>
    /// <param name="inputStream">The stream to read the data from.</param>
    /// <param name="options">Optional import options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An async enumerable of results for each imported row.</returns>
    IAsyncEnumerable<Result<TTarget>> ImportStreamAsync<TTarget>(
        Stream inputStream,
        ImportOptions options = null,
        CancellationToken cancellationToken = default)
        where TTarget : class, new();

    /// <summary>
    /// Validates import data without actually importing.
    /// </summary>
    /// <typeparam name="TTarget">The type of the target data.</typeparam>
    /// <param name="inputStream">The stream to read the data from.</param>
    /// <param name="options">Optional import options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing validation information.</returns>
    Task<Result<ValidationResult>> ValidateAsync<TTarget>(
        Stream inputStream,
        ImportOptions options = null,
        CancellationToken cancellationToken = default)
        where TTarget : class, new();
}
