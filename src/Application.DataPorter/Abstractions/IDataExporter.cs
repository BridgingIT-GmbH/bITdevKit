// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using BridgingIT.DevKit.Common;

/// <summary>
/// Defines the contract for exporting data to various formats.
/// </summary>
public interface IDataExporter
{
    /// <summary>
    /// Exports data to a stream using the specified options.
    /// </summary>
    /// <typeparam name="TSource">The type of the source data.</typeparam>
    /// <param name="data">The data to export.</param>
    /// <param name="outputStream">The stream to write the exported data to.</param>
    /// <param name="options">Optional export options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing export information or error details.</returns>
    Task<Result<ExportResult>> ExportAsync<TSource>(
        IEnumerable<TSource> data,
        Stream outputStream,
        ExportOptions options = null,
        CancellationToken cancellationToken = default)
        where TSource : class;

    /// <summary>
    /// Exports data to a stream using a fluent export options builder.
    /// </summary>
    /// <typeparam name="TSource">The type of the source data.</typeparam>
    /// <param name="data">The data to export.</param>
    /// <param name="outputStream">The stream to write the exported data to.</param>
    /// <param name="optionsBuilder">The fluent options builder.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing export information or error details.</returns>
    Task<Result<ExportResult>> ExportAsync<TSource>(
        IEnumerable<TSource> data,
        Stream outputStream,
        Builder<ExportOptionsBuilder, ExportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TSource : class;

    /// <summary>
    /// Exports asynchronous data to a stream using the specified options.
    /// </summary>
    /// <typeparam name="TSource">The type of the source data.</typeparam>
    /// <param name="data">The data to export.</param>
    /// <param name="outputStream">The stream to write the exported data to.</param>
    /// <param name="options">Optional export options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing export information or error details.</returns>
    Task<Result<ExportResult>> ExportAsync<TSource>(
        IAsyncEnumerable<TSource> data,
        Stream outputStream,
        ExportOptions options = null,
        CancellationToken cancellationToken = default)
        where TSource : class;

    /// <summary>
    /// Exports asynchronous data to a stream using a fluent export options builder.
    /// </summary>
    /// <typeparam name="TSource">The type of the source data.</typeparam>
    /// <param name="data">The data to export.</param>
    /// <param name="outputStream">The stream to write the exported data to.</param>
    /// <param name="optionsBuilder">The fluent options builder.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing export information or error details.</returns>
    Task<Result<ExportResult>> ExportAsync<TSource>(
        IAsyncEnumerable<TSource> data,
        Stream outputStream,
        Builder<ExportOptionsBuilder, ExportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TSource : class;

    /// <summary>
    /// Exports data to a byte array using the specified options.
    /// </summary>
    /// <typeparam name="TSource">The type of the source data.</typeparam>
    /// <param name="data">The data to export.</param>
    /// <param name="options">Optional export options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the exported data as bytes or error details.</returns>
    Task<Result<byte[]>> ExportToBytesAsync<TSource>(
        IEnumerable<TSource> data,
        ExportOptions options = null,
        CancellationToken cancellationToken = default)
        where TSource : class;

    /// <summary>
    /// Exports data to file content using the specified options.
    /// </summary>
    /// <typeparam name="TSource">The type of the source data.</typeparam>
    /// <param name="data">The data to export.</param>
    /// <param name="options">Optional export options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the exported file content or error details.</returns>
    Task<Result<FileContent>> ExportToFileContentAsync<TSource>(
        IEnumerable<TSource> data,
        ExportOptions options = null,
        CancellationToken cancellationToken = default)
        where TSource : class;

    /// <summary>
    /// Exports data to a byte array using a fluent export options builder.
    /// </summary>
    /// <typeparam name="TSource">The type of the source data.</typeparam>
    /// <param name="data">The data to export.</param>
    /// <param name="optionsBuilder">The fluent options builder.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the exported data as bytes or error details.</returns>
    Task<Result<byte[]>> ExportToBytesAsync<TSource>(
        IEnumerable<TSource> data,
        Builder<ExportOptionsBuilder, ExportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TSource : class;

    /// <summary>
    /// Exports data to file content using a fluent export options builder.
    /// </summary>
    /// <typeparam name="TSource">The type of the source data.</typeparam>
    /// <param name="data">The data to export.</param>
    /// <param name="optionsBuilder">The fluent options builder.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the exported file content or error details.</returns>
    Task<Result<FileContent>> ExportToFileContentAsync<TSource>(
        IEnumerable<TSource> data,
        Builder<ExportOptionsBuilder, ExportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TSource : class;

    /// <summary>
    /// Exports asynchronous data to a byte array using the specified options.
    /// </summary>
    /// <typeparam name="TSource">The type of the source data.</typeparam>
    /// <param name="data">The data to export.</param>
    /// <param name="options">Optional export options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the exported data as bytes or error details.</returns>
    Task<Result<byte[]>> ExportToBytesAsync<TSource>(
        IAsyncEnumerable<TSource> data,
        ExportOptions options = null,
        CancellationToken cancellationToken = default)
        where TSource : class;

    /// <summary>
    /// Exports asynchronous data to file content using the specified options.
    /// </summary>
    /// <typeparam name="TSource">The type of the source data.</typeparam>
    /// <param name="data">The data to export.</param>
    /// <param name="options">Optional export options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the exported file content or error details.</returns>
    Task<Result<FileContent>> ExportToFileContentAsync<TSource>(
        IAsyncEnumerable<TSource> data,
        ExportOptions options = null,
        CancellationToken cancellationToken = default)
        where TSource : class;

    /// <summary>
    /// Exports asynchronous data to a byte array using a fluent export options builder.
    /// </summary>
    /// <typeparam name="TSource">The type of the source data.</typeparam>
    /// <param name="data">The data to export.</param>
    /// <param name="optionsBuilder">The fluent options builder.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the exported data as bytes or error details.</returns>
    Task<Result<byte[]>> ExportToBytesAsync<TSource>(
        IAsyncEnumerable<TSource> data,
        Builder<ExportOptionsBuilder, ExportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TSource : class;

    /// <summary>
    /// Exports asynchronous data to file content using a fluent export options builder.
    /// </summary>
    /// <typeparam name="TSource">The type of the source data.</typeparam>
    /// <param name="data">The data to export.</param>
    /// <param name="optionsBuilder">The fluent options builder.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the exported file content or error details.</returns>
    Task<Result<FileContent>> ExportToFileContentAsync<TSource>(
        IAsyncEnumerable<TSource> data,
        Builder<ExportOptionsBuilder, ExportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TSource : class;

    /// <summary>
    /// Exports multiple data sets to different sheets/sections.
    /// </summary>
    /// <param name="dataSets">The collection of data sets to export.</param>
    /// <param name="outputStream">The stream to write the exported data to.</param>
    /// <param name="options">Optional export options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing export information or error details.</returns>
    Task<Result<ExportResult>> ExportAsync(
        IEnumerable<ExportDataSet> dataSets,
        Stream outputStream,
        ExportOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports multiple data sets using a fluent export options builder.
    /// </summary>
    /// <param name="dataSets">The collection of data sets to export.</param>
    /// <param name="outputStream">The stream to write the exported data to.</param>
    /// <param name="optionsBuilder">The fluent options builder.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing export information or error details.</returns>
    Task<Result<ExportResult>> ExportAsync(
        IEnumerable<ExportDataSet> dataSets,
        Stream outputStream,
        Builder<ExportOptionsBuilder, ExportOptions> optionsBuilder,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports multiple asynchronous data sets to different sheets/sections.
    /// </summary>
    /// <param name="dataSets">The collection of data sets to export.</param>
    /// <param name="outputStream">The stream to write the exported data to.</param>
    /// <param name="options">Optional export options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing export information or error details.</returns>
    Task<Result<ExportResult>> ExportAsync(
        IEnumerable<AsyncExportDataSet> dataSets,
        Stream outputStream,
        ExportOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports multiple asynchronous data sets using a fluent export options builder.
    /// </summary>
    /// <param name="dataSets">The collection of data sets to export.</param>
    /// <param name="outputStream">The stream to write the exported data to.</param>
    /// <param name="optionsBuilder">The fluent options builder.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing export information or error details.</returns>
    Task<Result<ExportResult>> ExportAsync(
        IEnumerable<AsyncExportDataSet> dataSets,
        Stream outputStream,
        Builder<ExportOptionsBuilder, ExportOptions> optionsBuilder,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a template to a stream using the specified options.
    /// </summary>
    /// <typeparam name="TTarget">The type the template is for.</typeparam>
    /// <param name="outputStream">The stream to write the generated template to.</param>
    /// <param name="options">Optional template generation options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing export information or error details.</returns>
    Task<Result<ExportResult>> GenerateTemplateAsync<TTarget>(
        Stream outputStream,
        TemplateOptions options = null,
        CancellationToken cancellationToken = default)
        where TTarget : class, new();

    /// <summary>
    /// Generates a template to a stream using a fluent template options builder.
    /// </summary>
    /// <typeparam name="TTarget">The type the template is for.</typeparam>
    /// <param name="outputStream">The stream to write the generated template to.</param>
    /// <param name="optionsBuilder">The fluent options builder.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing export information or error details.</returns>
    Task<Result<ExportResult>> GenerateTemplateAsync<TTarget>(
        Stream outputStream,
        Builder<TemplateOptionsBuilder, TemplateOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TTarget : class, new();

    /// <summary>
    /// Generates a template to a byte array using the specified options.
    /// </summary>
    /// <typeparam name="TTarget">The type the template is for.</typeparam>
    /// <param name="options">Optional template generation options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the generated template as bytes or error details.</returns>
    Task<Result<byte[]>> GenerateTemplateToBytesAsync<TTarget>(
        TemplateOptions options = null,
        CancellationToken cancellationToken = default)
        where TTarget : class, new();

    /// <summary>
    /// Generates a template to a byte array using a fluent template options builder.
    /// </summary>
    /// <typeparam name="TTarget">The type the template is for.</typeparam>
    /// <param name="optionsBuilder">The fluent options builder.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the generated template as bytes or error details.</returns>
    Task<Result<byte[]>> GenerateTemplateToBytesAsync<TTarget>(
        Builder<TemplateOptionsBuilder, TemplateOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TTarget : class, new();
}
