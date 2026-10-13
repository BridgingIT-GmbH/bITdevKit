// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Allows a worker host to isolate inherited profiling context before invoking its behavior pipeline.</summary>
/// <example><code>using var boundary = profilingBehavior.BeginExecutionBoundary(); await ExecuteHandlerAsync();</code></example>
public interface IProfilingExecutionBoundaryBehavior
{
    /// <summary>Starts an independent execution boundary and returns a fault-isolated restoration scope, or null when profiling is unavailable.</summary>
    /// <remarks>Implementations must isolate recoverable observation and restoration failures. Business execution remains owned by the host.</remarks>
    /// <returns>The optional scope that restores the caller's profiling context.</returns>
    /// <example><code>using var boundary = behavior.BeginExecutionBoundary(); await ProcessAsync();</code></example>
    IDisposable BeginExecutionBoundary();
}
