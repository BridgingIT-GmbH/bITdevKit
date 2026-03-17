// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Globalization;

/// <summary>
/// Tracks and reports export progress.
/// </summary>
internal sealed class ExportProgressTracker(IProgress<ExportProgressReport> progress, Format format)
{
    private const int ReportInterval = 25;
    private readonly IProgress<ExportProgressReport> progress = progress;
    private readonly Format format = format;
    private int lastReportedBucket;

    public void ReportStart(string message = "Starting export")
    {
        this.progress?.Report(new ExportProgressReport
        {
            Operation = "Export",
            Format = this.format,
            ProcessedRows = 0,
            TotalRows = null,
            PercentageComplete = null,
            BytesWritten = 0,
            IsCompleted = false,
            Messages = [message]
        });
    }

    public void ReportProgress(int processedRows, long bytesWritten, int? totalRows = null, string message = null)
    {
        if (this.progress is null || processedRows < ReportInterval)
        {
            return;
        }

        var bucket = processedRows / ReportInterval;
        if (bucket <= this.lastReportedBucket)
        {
            return;
        }

        this.lastReportedBucket = bucket;
        this.progress.Report(new ExportProgressReport
        {
            Operation = "Export",
            Format = this.format,
            ProcessedRows = processedRows,
            TotalRows = totalRows,
            PercentageComplete = GetPercentage(processedRows, totalRows),
            BytesWritten = bytesWritten,
            IsCompleted = false,
            Messages = [message ?? $"Exported {processedRows.ToString(CultureInfo.InvariantCulture)} rows"]
        });
    }

    public void ReportCompleted(ExportResult result, string message = "Export completed")
    {
        this.progress?.Report(new ExportProgressReport
        {
            Operation = "Export",
            Format = result.Format,
            ProcessedRows = result.TotalRows,
            TotalRows = result.TotalRows,
            PercentageComplete = 100d,
            BytesWritten = result.BytesWritten,
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
