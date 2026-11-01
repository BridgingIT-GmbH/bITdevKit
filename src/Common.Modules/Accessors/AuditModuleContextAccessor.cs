// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Adapts optional module lookup to the framework-neutral auditing contract.</summary>
/// <example><code>var name = accessor.Find(typeof(Customer));</code></example>
public sealed class AuditModuleContextAccessor(IEnumerable<IModuleContextAccessor> accessors) : IAuditModuleContextAccessor
{
    /// <inheritdoc />
    public string Find(Type type) => type is null ? null : accessors.Find(type)?.Name;
}
