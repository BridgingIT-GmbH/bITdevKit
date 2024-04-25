// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static partial class UtilitiesExtensions
{
    /// <summary>
    /// Makes a copy from the object.
    /// Doesn't copy the reference memory, only data.
    /// </summary>
    /// <typeparam name="T">Type of the object.</typeparam>
    /// <param name="source">Object to be copied.</param>
    /// <returns>Returns the copied object.</returns>
    public static T Clone<T>(this T source)
         where T : class
    {
        return CloneHelper.Clone(source);
    }

    /// <summary>
    /// Makes a copy from the object.
    /// Doesn't copy the reference memory, only data.
    /// </summary>
    /// <typeparam name="T">Type of the object.</typeparam>
    /// <param name="source">Object to be copied.</param>
    /// <param name="type">Object type.</param>
    /// <returns>Returns the copied object.</returns>
    public static object Clone<T>(this T source, Type type)
         where T : class
    {
        return CloneHelper.Clone(source, type);
    }
}
