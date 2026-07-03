// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Globalization;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Provides dependency injection registration for business calendars.
/// </summary>
/// <remarks><example><code>builder.Services.AddBusinessCalendars(calendars =&gt; calendars.RegisterCountry("NL", calendar));</code></example></remarks>
public static class BusinessCalendarServiceCollectionExtensions
{
    /// <summary>
    /// Registers business calendars and the culture-based resolver.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The calendar configuration.</param>
    /// <returns>The updated service collection.</returns>
    /// <remarks><example><code>builder.Services.AddBusinessCalendars(calendars =&gt; calendars.Register(CultureInfo.GetCultureInfo("nl-NL"), new BusinessCalendar()));</code></example></remarks>
    public static IServiceCollection AddBusinessCalendars(
        this IServiceCollection services,
        Action<BusinessCalendarBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new BusinessCalendarOptions();
        var builder = new BusinessCalendarBuilder(services, options);
        configure(builder);

        services.AddSingleton(options);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, BusinessCalendarStartupDiagnosticsService>());
        services.TryAddScoped<IBusinessCalendarResolver, BusinessCalendarResolver>();

        if (options.DefaultCalendar is not null)
        {
            BusinessCalendars.SetDefault(options.DefaultCalendar);
        }

        foreach (var registration in options.Registrations.Where(item => item.Calendar is not null))
        {
            foreach (var code in registration.Codes)
            {
                BusinessCalendars.Register(code, registration.Calendar);
            }
        }

        return services;
    }
}
