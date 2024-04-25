// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Configuration;

public static class ConfigurationExtensions
{
    public static T Get<T>(this IConfiguration source, IModule module)
        where T : class
    {
        return source.GetSection(module)?.Get<T>() ?? Factory<T>.Create();
    }

    public static IConfiguration GetSection(this IConfiguration source, IModule module)
    {
        return source?.GetSection($"Modules:{module?.Name}");
    }
}