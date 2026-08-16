// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
///     Provides logger initialization for startup-task behaviors.
/// </summary>
public abstract class StartupTaskBehaviorBase : IStartupTaskBehavior
{
    /// <summary>Initializes a new startup-task behavior.</summary>
    /// <param name="loggerFactory">The logger factory, or <see langword="null"/> to use a null logger.</param>
    protected StartupTaskBehaviorBase(ILoggerFactory loggerFactory)
    {
        this.Logger = loggerFactory?.CreateLogger(this.GetType()) ??
            NullLoggerFactory.Instance.CreateLogger(this.GetType());
    }

    /// <summary>Gets the behavior logger.</summary>
    protected ILogger Logger { get; }

    /// <inheritdoc/>
    public abstract Task Execute(IStartupTask task, CancellationToken cancellationToken, TaskDelegate next);
}
