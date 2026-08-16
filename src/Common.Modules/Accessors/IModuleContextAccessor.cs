// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Resolves a module from the assembly that defines a type.
/// </summary>
public interface IModuleContextAccessor
{
    /// <summary>
    ///     Finds the module associated with a type.
    /// </summary>
    /// <param name="type">The type whose assembly identifies the module.</param>
    /// <returns>The matching module, or <see langword="null"/> when none is found.</returns>
    IModule Find(Type type);
}
