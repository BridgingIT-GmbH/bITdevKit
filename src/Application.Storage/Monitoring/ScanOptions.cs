// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage.Monitoring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Represents options for scanning, including processing behavior and timing settings.
/// </summary>
/// <param name="waitForProcessing">Indicates whether to wait for processing to complete before proceeding.</param>
/// <param name="timeout">Specifies the maximum duration to wait for the operation to finish.</param>
/// <param name="delayPerFile">Sets the time to pause between processing each file.</param>
public class ScanOptions(bool waitForProcessing = false, TimeSpan? timeout = null, TimeSpan? delayPerFile = null)
{
    public bool WaitForProcessing { get; set; } = waitForProcessing;

    public TimeSpan Timeout { get; set; } = timeout ?? new TimeSpan(0, 5, 0);

    public TimeSpan DelayPerFile { get; set; } = delayPerFile ?? TimeSpan.Zero;

    public static ScanOptions Default => new();
}