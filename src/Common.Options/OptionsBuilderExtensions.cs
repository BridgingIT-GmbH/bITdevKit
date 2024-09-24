// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Provides extension methods for the IOptionsBuilder interface.
/// </summary>
public static class OptionsBuilderExtensions
{
    /// <summary>
    ///     Provides access to the target options for the specified builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The target options of the specified builder.</returns>
    public static T Target<T>(this IOptionsBuilder builder)
    {
        return (T)builder.Target;
    }
}