// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System;
using System.Threading;

/// <summary>
/// Provides an asynchronous-flow-local scope that temporarily suppresses change-history capture.
/// </summary>
/// <remarks>
/// Suppression scopes may be nested. Capture resumes when the outermost scope is disposed.
/// </remarks>
/// <example>
/// <code>
/// using (ChangeHistoryCaptureScope.Suppress())
/// {
///     await repository.UpdateAsync(entity, cancellationToken);
/// }
/// </code>
/// </example>
public static class ChangeHistoryCaptureScope
{
    private static readonly AsyncLocal<int> SuppressionDepth = new();

    /// <summary>
    /// Gets a value indicating whether change-history capture is currently suppressed for the asynchronous flow.
    /// </summary>
    /// <example>
    /// <code>
    /// if (ChangeHistoryCaptureScope.IsSuppressed)
    /// {
    ///     return;
    /// }
    /// </code>
    /// </example>
    public static bool IsSuppressed => SuppressionDepth.Value > 0;

    /// <summary>
    /// Begins a nested scope that suppresses change-history capture until the returned handle is disposed.
    /// </summary>
    /// <returns>A handle that restores capture when disposed.</returns>
    /// <example>
    /// <code>
    /// using var scope = ChangeHistoryCaptureScope.Suppress();
    /// await repository.UpdateAsync(entity, cancellationToken);
    /// </code>
    /// </example>
    public static IDisposable Suppress()
    {
        SuppressionDepth.Value++;

        return new SuppressionHandle();
    }

    private sealed class SuppressionHandle : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            SuppressionDepth.Value = Math.Max(0, SuppressionDepth.Value - 1);
            this.disposed = true;
        }
    }
}
