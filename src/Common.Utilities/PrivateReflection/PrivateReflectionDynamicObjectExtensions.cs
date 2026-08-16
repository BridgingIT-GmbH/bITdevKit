// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using PrivateReflection;

public static partial class UtilitiesExtensions
{
    /// <summary>
    ///     Wraps a non-primitive object for dynamic access to its private members.
    /// </summary>
    /// <param name="o">The object to wrap.</param>
    /// <returns>The original value for null, primitive, and string inputs; otherwise, a dynamic reflection wrapper.</returns>
    public static dynamic AsReflectionDynamic(this object o)
    {
        return PrivateReflectionDynamicObject.WrapObjectIfNeeded(o);
    }
}
