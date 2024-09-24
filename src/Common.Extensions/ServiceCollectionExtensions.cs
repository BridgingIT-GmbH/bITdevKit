// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static bool IsAdded<TServiceType>(this IServiceCollection services)
    {
        return !services.IsNullOrEmpty() && services.Any(s => s.ServiceType == typeof(TServiceType));
    }

    public static ServiceDescriptor Find<TServiceType>(this IServiceCollection services)
    {
        return services.IsNullOrEmpty() ? default : services.FirstOrDefault(s => s.ServiceType == typeof(TServiceType));
    }

    public static int IndexOf<TServiceType>(this IServiceCollection services)
    {
        if (services.IsNullOrEmpty())
        {
            return -1;
        }

        var descriptor = services.Find<TServiceType>();
        if (descriptor is not null)
        {
            return services.IndexOf(descriptor);
        }

        return -1;
    }
}