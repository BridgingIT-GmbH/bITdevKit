// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queueing;

using System.Diagnostics;

/// <summary>
/// Backward-compatible alias for <see cref="ModuleScopeQueueHandlerBehavior" />.
/// </summary>
/// <example>
/// <code>
/// services.AddQueueing()
///     .WithBehavior&lt;ModuleScopeQueueHAndlerBehavior&gt;();
/// </code>
/// </example>
[Obsolete("Use ModuleScopeQueueHandlerBehavior instead.")]
public class ModuleScopeQueueHAndlerBehavior(
    ILoggerFactory loggerFactory,
    IEnumerable<IModuleContextAccessor> moduleAccessors = null,
    IEnumerable<ActivitySource> activitySources = null)
    : ModuleScopeQueueHandlerBehavior(loggerFactory, moduleAccessors, activitySources)
{
}
