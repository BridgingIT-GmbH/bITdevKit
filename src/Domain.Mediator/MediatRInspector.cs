// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System;
using System.Linq;
using MediatR.Pipeline;

/// <summary>
///    Provides extension methods for the IServiceCollection to inspect MediatR registrations.
/// </summary>
public static class MediatRInspector
{
    private static readonly Type[] MediatRHandlerTypes =
    [
        typeof(MediatR.INotificationHandler < >),
        typeof(MediatR.IRequestHandler <,   >),
        typeof(MediatR.IRequestHandler < >),
        typeof(MediatR.IStreamRequestHandler <, >),
        typeof(IRequestExceptionHandler<,,>),
        typeof(IRequestExceptionAction<,>)
    ];

    /// <summary>
    ///    Prints all MediatR registrations to the console.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="enabled"></param>
    public static void PrintMediatRRegistrations(this IServiceCollection services, bool enabled = true)
    {
        if (enabled)
        {
            foreach (var handlerType in MediatRHandlerTypes)
            {
                Print(services, handlerType);
            }
        }
    }

    private static void Print(IServiceCollection services, Type handlerType)
    {
        var registrations = services
            .Where(sd => sd.ServiceType.IsGenericType &&
                         sd.ServiceType.GetGenericTypeDefinition() == handlerType)
            .GroupBy(sd => sd.ServiceType).ToList();

        if (registrations.Count != 0)
        {
            Console.WriteLine("----------------------------------------------------------------");
            Console.WriteLine($"--- {handlerType.PrettyName()} has registrations");

            foreach (var group in registrations)
            {
                var serviceType = group.Key;
                Console.WriteLine($"Service: {FormatGenericType(serviceType)}");
                Console.WriteLine("Handlers:");

                foreach (var registration in group)
                {
                    var implementationType = registration.ImplementationType;
                    Console.WriteLine($"  - {implementationType?.PrettyName()} ({registration.Lifetime})");
                }
                Console.WriteLine();
            }

            Console.WriteLine($"Total unique {handlerType.Name} types: {registrations.Count}");
            Console.WriteLine($"Total {handlerType.Name} registrations: {registrations.Sum(g => g.Count())}");
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine("----------------------------------------------------------------");
            Console.WriteLine($"--- {handlerType.PrettyName()} has no handlers registered");
            Console.WriteLine();
        }
    }

    private static string FormatGenericType(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var genericArguments = string.Join(", ", type.GetGenericArguments().Select(t => t.Name));
        return $"{type.Name.Split('`')[0]}<{genericArguments}>";
    }
}