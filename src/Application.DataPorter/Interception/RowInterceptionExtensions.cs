// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using BridgingIT.DevKit.Common;

/// <summary>
/// Provides helpers for accessing configured row interception executors.
/// </summary>
public static class RowInterceptionExtensions
{
    /// <summary>
    /// Gets the configured typed import row interception executor.
    /// </summary>
    /// <typeparam name="TTarget">The import target type.</typeparam>
    /// <param name="configuration">The import configuration.</param>
    /// <returns>The configured executor, or <see langword="null"/> when none is available.</returns>
    public static IImportRowInterceptionExecutor<TTarget> GetImportRowInterceptionExecutor<TTarget>(this ImportConfiguration configuration)
        where TTarget : class
    {
        return configuration.RowInterceptionExecutor as IImportRowInterceptionExecutor<TTarget>;
    }

    /// <summary>
    /// Gets the configured typed export row interception executor.
    /// </summary>
    /// <typeparam name="TSource">The export source type.</typeparam>
    /// <param name="configuration">The export configuration.</param>
    /// <returns>The configured executor, or <see langword="null"/> when none is available.</returns>
    public static IExportRowInterceptionExecutor<TSource> GetExportRowInterceptionExecutor<TSource>(this ExportConfiguration configuration)
        where TSource : class
    {
        return configuration.RowInterceptionExecutor as IExportRowInterceptionExecutor<TSource>;
    }

    internal static async Task<ImportResult<TTarget>> CompleteImportAsync<TTarget>(
        this IImportRowInterceptionExecutor<TTarget> executor,
        ImportResult<TTarget> result,
        Format format,
        string sheetName,
        bool isStreaming,
        ImportConfiguration configuration,
        CancellationToken cancellationToken = default)
        where TTarget : class
    {
        if (executor is null)
        {
            return result;
        }

        var completionResult = await executor.CompleteAsync(result, format, sheetName, isStreaming, cancellationToken);
        if (completionResult.IsSuccess)
        {
            return result;
        }

        var errors = result.Errors.ToList();
        var completionErrors = completionResult.Errors.SafeNull().ToList();
        if (completionErrors.Count == 0)
        {
            ImportErrorLimit.TryAdd(errors, new ImportRowError
            {
                RowNumber = 0,
                Column = "N/A",
                Message = completionResult.Messages.FirstOrDefault() ?? "Import completion interceptor failed.",
                Severity = ErrorSeverity.Critical
            }, configuration);
        }

        foreach (var error in completionErrors)
        {
            ImportErrorLimit.TryAdd(errors, new ImportRowError
            {
                RowNumber = 0,
                Column = "N/A",
                Message = error.Message,
                Severity = ErrorSeverity.Critical
            }, configuration);
        }

        return result with
        {
            FailedRows = result.FailedRows + Math.Max(1, completionErrors.Count),
            Errors = errors
        };
    }
}
