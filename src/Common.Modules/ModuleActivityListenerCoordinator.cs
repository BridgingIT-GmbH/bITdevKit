// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using Serilog;

/// <summary>
///     Coordinates leases for the process-wide activity listener used by module tracing.
/// </summary>
/// <remarks>
///     The first lease creates the listener. Disposing the final lease removes it.
/// </remarks>
/// <example>
/// <code>
/// using var listenerLease = ModuleActivityListenerCoordinator.Acquire();
/// </code>
/// </example>
public static class ModuleActivityListenerCoordinator
{
    private static readonly Lock SyncRoot = new();
    private static ActivityListener listener;
    private static int leaseCount;

    /// <summary>
    ///     Acquires a shared process-wide activity listener lease.
    /// </summary>
    /// <returns>A lease that releases the shared listener when disposed.</returns>
    public static IDisposable Acquire()
    {
        lock (SyncRoot)
        {
            if (leaseCount++ == 0)
            {
                listener = CreateListener();
                ActivitySource.AddActivityListener(listener);
            }
        }

        return new Lease();
    }

    private static ActivityListener CreateListener()
    {
        return new()
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity =>
            {
                if (string.IsNullOrWhiteSpace(activity?.DisplayName))
                {
                    return;
                }

                Log.Logger.Verbose(
                    "[{LogKey}] started activity: {ActivityOperationName} {ActivityDisplayName} (module={ModuleName}, status={ActivityStatus})",
                    "TRC",
                    activity.OperationName,
                    activity.DisplayName,
                    activity.Source.Name,
                    activity.Status);
            },
            ActivityStopped = activity =>
            {
                foreach (var (key, value) in activity.Baggage)
                {
                    activity.SetTag(key, value);
                }

                if (string.IsNullOrWhiteSpace(activity.DisplayName))
                {
                    return;
                }

                Log.Logger.Verbose(
                    "[{LogKey}] finished activity: {ActivityOperationName} {ActivityDisplayName} (module={ModuleName}, status={ActivityStatus}) -> took {TimeElapsed:0.0000} ms",
                    "TRC",
                    activity.OperationName,
                    activity.DisplayName,
                    activity.Source.Name,
                    activity.Status,
                    activity.Duration.TotalMilliseconds);
            }
        };
    }

    private static void Release()
    {
        lock (SyncRoot)
        {
            if (leaseCount == 0 || --leaseCount != 0)
            {
                return;
            }

            listener?.Dispose();
            listener = null;
        }
    }

    private sealed class Lease : IDisposable
    {
        private bool disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            Release();
        }
    }
}
