// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.AspNetCore.Http;

public static class ContextAccessorExtensions
{
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