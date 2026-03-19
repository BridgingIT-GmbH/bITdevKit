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
    /// Imports data from a stream using the specified options.
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
    /// Imports data from a stream using a fluent import options builder.
    /// </summary>
    /// <typeparam name="TTarget">The type of the target data.</typeparam>
    /// <param name="inputStream">The stream to read the data from.</param>
    /// <param name="optionsBuilder">The fluent options builder.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the imported data or error details.</returns>
    Task<Result<ImportResult<TTarget>>> ImportAsync<TTarget>(
        Stream inputStream,
        Builder<ImportOptionsBuilder, ImportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TTarget : class, new();

    /// <summary>
    /// Imports data from a byte array using the specified options.
    /// </summary>
    /// <typeparam name="TTarget">The type of the target data.</typeparam>
    /// <param name="data">The byte array containing the data to import.</param>
    /// <param name="options">Optional import options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the imported data or error details.</returns>
    Task<Result<ImportResult<TTarget>>> ImportAsync<TTarget>(
        byte[] data,
        ImportOptions options = null,
        CancellationToken cancellationToken = default)
        where TTarget : class, new();

    /// <summary>
    /// Imports data from a byte array using a fluent import options builder.
    /// </summary>
    /// <typeparam name="TTarget">The type of the target data.</typeparam>
    /// <param name="data">The byte array containing the data to import.</param>
    /// <param name="optionsBuilder">The fluent options builder.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the imported data or error details.</returns>
    Task<Result<ImportResult<TTarget>>> ImportAsync<TTarget>(
        byte[] data,
        Builder<ImportOptionsBuilder, ImportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TTarget : class, new();

    /// <summary>
    /// Imports data from a stream and returns results as an async enumerable.
    /// </summary>
    /// <typeparam name="TTarget">The type of the target data.</typeparam>
    /// <param name="inputStream">The stream to read the data from.</param>
    /// <param name="options">Optional import options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An async enumerable of results for each imported row.</returns>
    IAsyncEnumerable<Result<TTarget>> ImportAsyncEnumerable<TTarget>(
        Stream inputStream,
        ImportOptions options = null,
        CancellationToken cancellationToken = default)
        where TTarget : class, new();

    /// <summary>
    /// Imports data from a stream and returns results as an async enumerable using a fluent import options builder.
    /// </summary>
    /// <typeparam name="TTarget">The type of the target data.</typeparam>
    /// <param name="inputStream">The stream to read the data from.</param>
    /// <param name="optionsBuilder">The fluent options builder.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An async enumerable of results for each imported row.</returns>
    IAsyncEnumerable<Result<TTarget>> ImportAsyncEnumerable<TTarget>(
        Stream inputStream,
        Builder<ImportOptionsBuilder, ImportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TTarget : class, new();

    /// <summary>
    /// Validates the data in a stream against the specified options without importing it.
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

    /// <summary>
    /// Validates the data in a stream against the specified fluent import options builder without importing it.
    /// </summary>
    /// <typeparam name="TTarget">The type of the target data.</typeparam>
    /// <param name="inputStream">The stream to read the data from.</param>
    /// <param name="optionsBuilder">The fluent options builder.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing validation information.</returns>
    Task<Result<ValidationResult>> ValidateAsync<TTarget>(
        Stream inputStream,
        Builder<ImportOptionsBuilder, ImportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TTarget : class, new();
}
