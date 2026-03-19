// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Globalization;

/// <summary>
/// Tracks and reports import progress.
/// </summary>
internal sealed class ImportProgressTracker(IProgress<ImportProgressReport> progress, Format format)
{
    private const int ReportInterval = 25;
    private readonly IProgress<ImportProgressReport> progress = progress;
    private readonly Format format = format;
    private int lastReportedBucket;
    private int processedRows;
    private int successfulRows;
    private int failedRows;
    private int errorCount;
    private int skippedRows;
    private int? totalRows;

    public void ReportStart(string message = "Starting import")
    {
        this.processedRows = 0;
        this.successfulRows = 0;
        this.failedRows = 0;
        this.errorCount = 0;
        this.skippedRows = 0;
        this.totalRows = null;
        this.lastReportedBucket = 0;

        this.progress?.Report(new ImportProgressReport
        {
            Operation = "Import",
            Format = this.format,
            ProcessedRows = 0,
            TotalRows = null,
            PercentageComplete = null,
            SuccessfulRows = 0,
            FailedRows = 0,
            ErrorCount = 0,
            SkippedRows = 0,
            IsCompleted = false,
            Messages = [message]
        });
    }

    public void ReportProgress(
        int processedRows,
        int successfulRows,
        int failedRows,
        int errorCount,
        int? totalRows = null,
        string message = null,
        int? skippedRows = null,
        bool force = false)
    {
        this.processedRows = processedRows;
        this.successfulRows = successfulRows;
        this.failedRows = failedRows;
        this.errorCount = errorCount;
        this.skippedRows = skippedRows ?? this.skippedRows;
        this.totalRows = totalRows ?? this.totalRows;

        if (this.progress is null)
        {
            return;
        }

        if (!force && processedRows < ReportInterval)
        {
            return;
        }

        var bucket = processedRows / ReportInterval;
        if (!force && bucket <= this.lastReportedBucket)
        {
            return;
        }

        if (!force)
        {
            this.lastReportedBucket = bucket;
        }

        this.progress.Report(new ImportProgressReport
        {
            Operation = "Import",
            Format = this.format,
            ProcessedRows = processedRows,
            TotalRows = this.totalRows,
            PercentageComplete = GetPercentage(processedRows, totalRows),
            SuccessfulRows = successfulRows,
            FailedRows = failedRows,
            ErrorCount = errorCount,
            SkippedRows = this.skippedRows,
            IsCompleted = false,
            Messages = [message ?? $"Processed {processedRows.ToString(CultureInfo.InvariantCulture)} rows"]
        });
    }

    public void ReportCompleted<T>(ImportResult<T> result, string message = "Import completed")
        where T : class
    {
        this.processedRows = result.TotalRows;
        this.successfulRows = result.SuccessfulRows;
        this.failedRows = result.FailedRows;
        this.errorCount = result.Errors.Count;
        this.skippedRows = result.SkippedRows;
        this.totalRows = result.TotalRows;

        this.progress?.Report(new ImportProgressReport
        {
            Operation = "Import",
            Format = this.format,
            ProcessedRows = result.TotalRows,
            TotalRows = result.TotalRows,
            PercentageComplete = 100d,
            SuccessfulRows = result.SuccessfulRows,
            FailedRows = result.FailedRows,
            ErrorCount = result.Errors.Count,
            SkippedRows = result.SkippedRows,
            IsCompleted = true,
            Messages = [message]
        });
    }

    public void ReportCompleted(string message = "Import completed")
    {
        this.progress?.Report(new ImportProgressReport
        {
            Operation = "Import",
            Format = this.format,
            ProcessedRows = this.processedRows,
            TotalRows = this.totalRows ?? this.processedRows,
            PercentageComplete = 100d,
            SuccessfulRows = this.successfulRows,
            FailedRows = this.failedRows,
            ErrorCount = this.errorCount,
            SkippedRows = this.skippedRows,
            IsCompleted = true,
            Messages = [message]
        });
    }

    private static double? GetPercentage(int processedRows, int? totalRows)
    {
        if (!totalRows.HasValue || totalRows.Value <= 0)
        {
            return null;
        }

        return Math.Min(100d, processedRows * 100d / totalRows.Value);
    }
}
