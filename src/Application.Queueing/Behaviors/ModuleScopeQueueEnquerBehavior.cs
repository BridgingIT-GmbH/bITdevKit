// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Backward-compatible alias for <see cref="ModuleScopeQueueEnqueuerBehavior" />.
/// </summary>
/// <example>
/// <code>
/// services.AddQueueing()
///     .WithBehavior&lt;ModuleScopeQueueEnquerBehavior&gt;();
/// </code>
/// </example>
[Obsolete("Use ModuleScopeQueueEnqueuerBehavior instead.")]
public class ModuleScopeQueueEnquerBehavior(
    ILoggerFactory loggerFactory,
    IEnumerable<IModuleContextAccessor> moduleAccessors = null)
    : ModuleScopeQueueEnqueuerBehavior(loggerFactory, moduleAccessors)
{
}

