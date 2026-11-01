// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Optional module-name lookup without referencing the module hosting framework.</summary>
/// <example><code>var module = accessor.Find(typeof(Customer));</code></example>
public interface IAuditModuleContextAccessor
{
    /// <summary>Returns the current type's module name, or null.</summary>
    /// <example><code>var module = accessor.Find(typeof(Customer));</code></example>
    string Find(Type type);
}
