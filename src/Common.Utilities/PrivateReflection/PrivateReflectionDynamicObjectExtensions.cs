// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using PrivateReflection;

public static partial class UtilitiesExtensions
{
    public static dynamic AsReflectionDynamic(this object o)
    {
        return PrivateReflectionDynamicObject.WrapObjectIfNeeded(o);
    }
}