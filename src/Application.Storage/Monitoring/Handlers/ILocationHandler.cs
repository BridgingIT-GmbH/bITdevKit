// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface ILocationHandler
{
    IFileStorageProvider Provider { get; }
    LocationOptions Options { get; }
    Task StartAsync(CancellationToken token = default);
    Task StopAsync(CancellationToken token = default);
    Task<ScanContext> ScanAsync(IProgress<ScanProgress> progress = null, CancellationToken token = default);
    Task<ScanContext> ScanAsync(bool waitForProcessing = false, TimeSpan timeout = default, IProgress<ScanProgress> progress = null, CancellationToken token = default);
    Task PauseAsync(CancellationToken token = default);
    Task ResumeAsync(CancellationToken token = default);
    Task<LocationStatus> GetStatusAsync();
    int GetQueueSize();
    Task<bool> IsQueueEmptyAsync();
    Task WaitForQueueEmptyAsync(TimeSpan timeout);
    Task<IEnumerable<string>> GetActiveProcessorsAsync();
    Task EnableProcessorAsync(string processorName);
    Task DisableProcessorAsync(string processorName);
    IEnumerable<IFileEventProcessor> GetProcessors();
}