// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing;

using System.Diagnostics;
using System.Reflection;
using Domain.EventSourcing.Model;
using Domain.EventSourcing.Registration;

public class RegistrationForEventStoreAggregatesAndEvents : IRegistrationForEventStoreAggregatesAndEvents
{
    private readonly IEventStoreAggregateRegistration aggregateRegistration;
    private readonly IEventStoreAggregateEventRegistration aggregateEventRegistration;

    public RegistrationForEventStoreAggregatesAndEvents(
        IEventStoreAggregateRegistration aggregateRegistration,
        IEventStoreAggregateEventRegistration aggregateEventRegistration)
    {
        this.aggregateRegistration = aggregateRegistration;
        this.aggregateEventRegistration = aggregateEventRegistration;
        EnsureArg.IsNotNull(aggregateRegistration, nameof(aggregateRegistration));
        EnsureArg.IsNotNull(aggregateEventRegistration, nameof(aggregateEventRegistration));
    }

    public void RegisterAggregatesAndEvents()
    {
        this.RegisterAggregatesAndEvents(AppDomain.CurrentDomain.GetAssemblies());
    }

    public void RegisterAggregatesAndEvents(Assembly[] assemblies)
    {
        EnsureArg.IsNotNull(assemblies, nameof(assemblies));
        this.AutoregisterAggregates(assemblies);
        this.AutoregisterAggregateEvents(assemblies);
    }

    private void AutoregisterAggregates(Assembly[] assemblies)
    {
        EnsureArg.IsNotNull(assemblies, nameof(assemblies));
        var list = assemblies.SelectMany(x => x.GetTypes())
            .Where(x => typeof(EventSourcingAggregateRoot).IsAssignableFrom(x) &&
                !x.IsInterface &&
                !x.IsAbstract &&
                x.GetCustomAttributes(typeof(ImmutableNameAttribute), true).Length > 0)
            .Select(x => x)
            .ToList();
        foreach (var entry in list)
        {
            try
            {
                var attr =
                    entry.GetCustomAttributes(typeof(ImmutableNameAttribute), true).FirstOrDefault() as
                        ImmutableNameAttribute;
                if (attr is null)
                {
                    continue;
                }

                var method = this.aggregateRegistration.GetType().GetMethod("Register");
                if (method is null)
                {
                    Trace.WriteLine($"Method Register not found at {entry.Name}");
                    continue;
                }

                var generic = method.MakeGenericMethod(entry);
                generic.Invoke(this.aggregateRegistration, [attr.ImmutableName]);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error during automatic registration for aggregate {entry.Name}: {ex.Message}");
            }
        }
    }

    private void AutoregisterAggregateEvents(Assembly[] assemblies)
    {
        EnsureArg.IsNotNull(assemblies, nameof(assemblies));
        var list = assemblies.SelectMany(x => x.GetTypes())
            .Where(x => typeof(AggregateEvent).IsAssignableFrom(x) &&
                !x.IsInterface &&
                !x.IsAbstract &&
                x.GetCustomAttributes(typeof(ImmutableNameAttribute), true).Length > 0)
            .Select(x => x)
            .ToList();
        foreach (var entry in list)
        {
            try
            {
                var attr =
                    entry.GetCustomAttributes(typeof(ImmutableNameAttribute), true).FirstOrDefault() as
                        ImmutableNameAttribute;
                if (attr is null)
                {
                    continue;
                }

                var method = this.aggregateEventRegistration.GetType().GetMethod("Register");
                if (method is null)
                {
                    Trace.WriteLine($"Method Register not found at {entry.Name}");
                    continue;
                }

                var generic = method.MakeGenericMethod(entry);
                generic.Invoke(this.aggregateEventRegistration, [attr.ImmutableName]);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error during automatic registration for aggregate event {entry.Name}: {ex.Message}");
            }
        }
    }
}