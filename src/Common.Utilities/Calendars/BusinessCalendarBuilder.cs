// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Globalization;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Configures business calendars for dependency injection and static convenience resolution.
/// </summary>
/// <remarks><example><code>calendars.RegisterCountry("NL", new BusinessCalendar());</code></example></remarks>
public sealed class BusinessCalendarBuilder
{
    private readonly IServiceCollection services;
    private readonly BusinessCalendarOptions options;

    internal BusinessCalendarBuilder(IServiceCollection services, BusinessCalendarOptions options)
    {
        this.services = services;
        this.options = options;
    }

    /// <summary>
    /// Sets the default calendar.
    /// </summary>
    /// <param name="calendar">The default calendar.</param>
    /// <returns>The builder.</returns>
    /// <remarks><example><code>calendars.SetDefault(new BusinessCalendar());</code></example></remarks>
    public BusinessCalendarBuilder SetDefault(IBusinessCalendar calendar)
    {
        this.options.DefaultCalendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.options.DefaultCalendarFactory = null;
        return this;
    }

    /// <summary>
    /// Sets the default calendar using the service provider.
    /// </summary>
    /// <param name="factory">The default calendar factory.</param>
    /// <returns>The builder.</returns>
    /// <remarks><example><code>calendars.SetDefault(sp =&gt; sp.GetRequiredService&lt;TenantCalendar&gt;());</code></example></remarks>
    public BusinessCalendarBuilder SetDefault(Func<IServiceProvider, IBusinessCalendar> factory)
    {
        this.options.DefaultCalendarFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        return this;
    }

    /// <summary>
    /// Registers a calendar for a culture, including its country and neutral language code.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <param name="calendar">The calendar.</param>
    /// <returns>The builder.</returns>
    /// <remarks><example><code>calendars.Register(CultureInfo.GetCultureInfo("nl-NL"), calendar);</code></example></remarks>
    public BusinessCalendarBuilder Register(CultureInfo culture, IBusinessCalendar calendar)
    {
        this.options.Add(BusinessCalendarRegistration.For(culture, calendar));
        return this;
    }

    /// <summary>
    /// Registers a service-backed calendar for a culture, including its country and neutral language code.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <param name="factory">The calendar factory.</param>
    /// <returns>The builder.</returns>
    /// <remarks><example><code>calendars.Register(CultureInfo.GetCultureInfo("nl-NL"), sp =&gt; sp.GetRequiredService&lt;NetherlandsCalendar&gt;());</code></example></remarks>
    public BusinessCalendarBuilder Register(CultureInfo culture, Func<IServiceProvider, IBusinessCalendar> factory)
    {
        this.options.Add(BusinessCalendarRegistration.For(culture, factory));
        return this;
    }

    /// <summary>
    /// Registers a calendar by country, culture, or language code.
    /// </summary>
    /// <param name="code">The country, culture, or language code.</param>
    /// <param name="calendar">The calendar.</param>
    /// <returns>The builder.</returns>
    /// <remarks><example><code>calendars.Register("BE", calendar);</code></example></remarks>
    public BusinessCalendarBuilder Register(string code, IBusinessCalendar calendar)
    {
        this.options.Add(BusinessCalendarRegistration.For(code, calendar));
        return this;
    }

    /// <summary>
    /// Registers a service-backed calendar by country, culture, or language code.
    /// </summary>
    /// <param name="code">The country, culture, or language code.</param>
    /// <param name="factory">The calendar factory.</param>
    /// <returns>The builder.</returns>
    /// <remarks><example><code>calendars.Register("BE", sp =&gt; sp.GetRequiredService&lt;BelgiumCalendar&gt;());</code></example></remarks>
    public BusinessCalendarBuilder Register(string code, Func<IServiceProvider, IBusinessCalendar> factory)
    {
        this.options.Add(BusinessCalendarRegistration.For(code, factory));
        return this;
    }

    /// <summary>
    /// Registers a calendar by country code.
    /// </summary>
    /// <param name="countryCode">The country code.</param>
    /// <param name="calendar">The calendar.</param>
    /// <returns>The builder.</returns>
    /// <remarks><example><code>calendars.RegisterCountry("NL", calendar);</code></example></remarks>
    public BusinessCalendarBuilder RegisterCountry(string countryCode, IBusinessCalendar calendar) => this.Register(countryCode, calendar);

    /// <summary>
    /// Registers a service-backed calendar by country code.
    /// </summary>
    /// <param name="countryCode">The country code.</param>
    /// <param name="factory">The calendar factory.</param>
    /// <returns>The builder.</returns>
    /// <remarks><example><code>calendars.RegisterCountry("NL", sp =&gt; sp.GetRequiredService&lt;NetherlandsCalendar&gt;());</code></example></remarks>
    public BusinessCalendarBuilder RegisterCountry(string countryCode, Func<IServiceProvider, IBusinessCalendar> factory) => this.Register(countryCode, factory);

    /// <summary>
    /// Registers a calendar implementation type and resolves it through dependency injection.
    /// </summary>
    /// <typeparam name="TCalendar">The calendar type.</typeparam>
    /// <param name="culture">The culture.</param>
    /// <param name="lifetime">The service lifetime.</param>
    /// <returns>The builder.</returns>
    /// <remarks><example><code>calendars.Register&lt;NetherlandsCalendar&gt;(CultureInfo.GetCultureInfo("nl-NL"), ServiceLifetime.Scoped);</code></example></remarks>
    public BusinessCalendarBuilder Register<TCalendar>(CultureInfo culture, ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TCalendar : class, IBusinessCalendar
    {
        this.services.TryAdd(new ServiceDescriptor(typeof(TCalendar), typeof(TCalendar), lifetime));
        return this.Register(culture, serviceProvider => serviceProvider.GetRequiredService<TCalendar>());
    }

    /// <summary>
    /// Registers a calendar implementation type by country code and resolves it through dependency injection.
    /// </summary>
    /// <typeparam name="TCalendar">The calendar type.</typeparam>
    /// <param name="countryCode">The country code.</param>
    /// <param name="lifetime">The service lifetime.</param>
    /// <returns>The builder.</returns>
    /// <remarks><example><code>calendars.RegisterCountry&lt;NetherlandsCalendar&gt;("NL", ServiceLifetime.Scoped);</code></example></remarks>
    public BusinessCalendarBuilder RegisterCountry<TCalendar>(string countryCode, ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TCalendar : class, IBusinessCalendar
    {
        this.services.TryAdd(new ServiceDescriptor(typeof(TCalendar), typeof(TCalendar), lifetime));
        return this.RegisterCountry(countryCode, serviceProvider => serviceProvider.GetRequiredService<TCalendar>());
    }
}
