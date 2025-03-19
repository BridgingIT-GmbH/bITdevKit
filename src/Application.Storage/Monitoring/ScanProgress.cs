// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.Diagnostics;

[DebuggerDisplay("ToString()")]
public class ScanProgress
{
    public int FilesScanned { get; set; }

    public int TotalFiles { get; set; } // Estimated or actual if known

    public double PercentageComplete => this.TotalFiles > 0 ? (double)this.FilesScanned / this.TotalFiles * 100 : 0;

    public TimeSpan ElapsedTime { get; set; }

    public override string ToString()
    {
        return $"scanned {this.FilesScanned}/{this.TotalFiles} files ({this.PercentageComplete:F2}%) in {this.ElapsedTime.TotalMilliseconds} ms";
    }
}