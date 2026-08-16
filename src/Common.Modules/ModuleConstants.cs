// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Contains constant values utilized across the module system.
/// </summary>
public struct ModuleConstants
{
    /// <summary>
    ///     The structured-log key used by the module subsystem.
    /// </summary>
    public const string LogKey = "MOD";

    /// <summary>
    ///     The context key that stores the active module name.
    /// </summary>
    public const string ModuleNameKey = "ModuleName";

    /// <summary>
    ///     The context key that stores the origin of a module name.
    /// </summary>
    public const string ModuleNameOriginKey = "ModuleNameOrigin";

    /// <summary>
    ///     The context key that stores a parent activity identifier.
    /// </summary>
    public const string ActivityParentIdKey = "ActivityParentId";

    /// <summary>
    ///     The empty fallback value used when no module name is known.
    /// </summary>
    public const string UnknownModuleName = "";
}
