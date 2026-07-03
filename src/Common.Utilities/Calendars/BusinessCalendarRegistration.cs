// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;

/// <summary>
/// Describes one business calendar registration.
/// </summary>
/// <remarks><example><code>var registration = BusinessCalendarRegistration.For("NL", calendar);</code></example></remarks>
public sealed class BusinessCalendarRegistration
{
    private BusinessCalendarRegistration(
        IEnumerable<string> codes,
        IBusinessCalendar calendar,
        Func<IServiceProvider, IBusinessCalendar> factory)
    {
        this.Codes = codes.ToArray();
        this.Calendar = calendar;
        this.Factory = factory;
    }

    /// <summary>Gets the culture, language, or country codes.</summary>
    public IReadOnlyList<string> Codes { get; }

    /// <summary>Gets the registered calendar instance.</summary>
    public IBusinessCalendar Calendar { get; }

    /// <summary>Gets the service-backed calendar factory.</summary>
    public Func<IServiceProvider, IBusinessCalendar> Factory { get; }

    /// <summary>
    /// Creates a calendar-instance registration.
    /// </summary>
    /// <param name="code">The culture, language, or country code.</param>
    /// <param name="calendar">The calendar.</param>
    /// <returns>The registration.</returns>
    /// <remarks><example><code>var registration = BusinessCalendarRegistration.For("NL", calendar);</code></example></remarks>
    public static BusinessCalendarRegistration For(string code, IBusinessCalendar calendar)
    {
        return new BusinessCalendarRegistration([NormalizeCode(code)], calendar ?? throw new ArgumentNullException(nameof(calendar)), null);
    }

    /// <summary>
    /// Creates a service-backed registration.
    /// </summary>
    /// <param name="code">The culture, language, or country code.</param>
    /// <param name="factory">The calendar factory.</param>
    /// <returns>The registration.</returns>
    /// <remarks><example><code>var registration = BusinessCalendarRegistration.For("NL", sp =&gt; sp.GetRequiredService&lt;MyCalendar&gt;());</code></example></remarks>
    public static BusinessCalendarRegistration For(string code, Func<IServiceProvider, IBusinessCalendar> factory)
    {
        return new BusinessCalendarRegistration([NormalizeCode(code)], null, factory ?? throw new ArgumentNullException(nameof(factory)));
    }

    internal static BusinessCalendarRegistration For(CultureInfo culture, IBusinessCalendar calendar)
    {
        return new BusinessCalendarRegistration(GetCodes(culture), calendar ?? throw new ArgumentNullException(nameof(calendar)), null);
    }

    internal static BusinessCalendarRegistration For(CultureInfo culture, Func<IServiceProvider, IBusinessCalendar> factory)
    {
        return new BusinessCalendarRegistration(GetCodes(culture), null, factory ?? throw new ArgumentNullException(nameof(factory)));
    }

    private static IEnumerable<string> GetCodes(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        if (!string.IsNullOrWhiteSpace(culture.Name))
        {
            yield return culture.Name;
        }

        var countryCode = TryGetCountryCode(culture);
        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            yield return countryCode;
        }

        yield return culture.TwoLetterISOLanguageName;
    }

    private static string NormalizeCode(string code)
    {
        return string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentException("A culture, language, or country code is required.", nameof(code))
            : code;
    }

    private static string TryGetCountryCode(CultureInfo culture)
    {
        try
        {
            return new RegionInfo(culture.Name).TwoLetterISORegionName;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
