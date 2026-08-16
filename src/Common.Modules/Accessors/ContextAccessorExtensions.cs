// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.AspNetCore.Http;

/// <summary>
///     Provides composite lookup across ordered module-context accessors.
/// </summary>
public static class ContextAccessorExtensions
{
    /// <summary>
    ///     Returns the first module found for a type by the supplied accessors.
    /// </summary>
    /// <param name="source">The accessors to query in enumeration order.</param>
    /// <param name="type">The type used to identify a module.</param>
    /// <returns>The first matching module, or <see langword="null"/> when no accessor resolves one.</returns>
    public static IModule Find(this IEnumerable<IModuleContextAccessor> source, Type type)
    {
        foreach (var accessor in source.SafeNull())
        {
            var result = accessor.Find(type);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    ///     Returns the first module found for a request by the supplied accessors.
    /// </summary>
    /// <param name="source">The accessors to query in enumeration order.</param>
    /// <param name="request">The HTTP request used to identify a module.</param>
    /// <returns>The first matching module, or <see langword="null"/> when no accessor resolves one.</returns>
    public static IModule Find(this IEnumerable<IRequestModuleContextAccessor> source, HttpRequest request)
    {
        foreach (var accessor in source.SafeNull())
        {
            var result = accessor.Find(request);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }
}
