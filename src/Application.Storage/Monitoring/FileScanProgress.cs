// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.Diagnostics;

/// <summary>
/// Represents the progress of a file scan operation.
/// </summary>
[DebuggerDisplay("{FilesScanned}/{TotalFiles} files ({PercentageComplete}%)")]
public class FileScanProgress
{
    private double? percentageComplete;

    /// <summary>
    /// Gets or sets the number of files that have been scanned.
    /// </summary>
    public int FilesScanned { get; set; }

    /// <summary>
    /// Gets or sets the total number of files to scan (estimated or actual).
    /// </summary>
    public int TotalFiles { get; set; } // Estimated or actual if known

    /// <summary>
    /// Gets or sets the percentage of files that have been scanned.
    /// </summary>
    public double PercentageComplete
    {
        get => this.percentageComplete ?? (this.TotalFiles > 0 ? (double)this.FilesScanned / this.TotalFiles * 100 : 0);
        set => this.percentageComplete = value;
    }

    /// <summary>
    /// Gets or sets the total time elapsed during the scan operation.
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }

    /// <summary>
    /// Returns a string that represents the current scan progress.
    /// </summary>
    /// <returns>A formatted string containing scan statistics.</returns>
    public override string ToString()
    {
        return $"scanned {this.FilesScanned}/{this.TotalFiles} files ({this.PercentageComplete:F2}%) in {this.ElapsedTime.TotalMilliseconds} ms";
    }
}