// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Globalization;

/// <summary>
/// Globally registers business calendars for convenience APIs.
/// </summary>
/// <remarks><example><code>BusinessCalendars.RegisterCountry("NL", calendar);</code></example></remarks>
public static class BusinessCalendars
{
    private static readonly Lock SyncRoot = new();
    private static Dictionary<string, IBusinessCalendar> calendars = new(StringComparer.OrdinalIgnoreCase);
    private static IBusinessCalendar defaultCalendar = new BusinessCalendar();

    /// <summary>
    /// Registers a calendar for an exact culture name and its country when available.
    /// </summary>
    /// <param name="culture">The culture to register.</param>
    /// <param name="calendar">The calendar to register.</param>
    /// <remarks><example><code>BusinessCalendars.Register(CultureInfo.GetCultureInfo("nl-NL"), calendar);</code></example></remarks>
    [DebuggerStepThrough]
    public static void Register(CultureInfo culture, IBusinessCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(calendar);

        lock (SyncRoot)
        {
            var updated = new Dictionary<string, IBusinessCalendar>(calendars, StringComparer.OrdinalIgnoreCase)
            {
                [culture.Name] = calendar,
                [culture.TwoLetterISOLanguageName] = calendar
            };

            var countryCode = TryGetCountryCode(culture);
            if (!string.IsNullOrWhiteSpace(countryCode))
            {
                updated[countryCode] = calendar;
            }

            calendars = updated;
        }
    }

    /// <summary>
    /// Registers a calendar by country or culture code.
    /// </summary>
    /// <param name="code">The country code, neutral language code, or culture name.</param>
    /// <param name="calendar">The calendar to register.</param>
    /// <remarks><example><code>BusinessCalendars.Register("DE", calendar);</code></example></remarks>
    [DebuggerStepThrough]
    public static void Register(string code, IBusinessCalendar calendar)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A culture, language, or country code is required.", nameof(code));
        }

        ArgumentNullException.ThrowIfNull(calendar);

        lock (SyncRoot)
        {
            var updated = new Dictionary<string, IBusinessCalendar>(calendars, StringComparer.OrdinalIgnoreCase)
            {
                [code] = calendar
            };
            calendars = updated;
        }
    }

    /// <summary>
    /// Registers a calendar by country code.
    /// </summary>
    /// <param name="countryCode">The ISO country code.</param>
    /// <param name="calendar">The calendar to register.</param>
    /// <remarks><example><code>BusinessCalendars.RegisterCountry("US", calendar);</code></example></remarks>
    [DebuggerStepThrough]
    public static void RegisterCountry(string countryCode, IBusinessCalendar calendar) => Register(countryCode, calendar);

    /// <summary>
    /// Sets the default calendar used when no culture-specific calendar is registered.
    /// </summary>
    /// <param name="calendar">The default calendar.</param>
    /// <remarks><example><code>BusinessCalendars.SetDefault(new BusinessCalendar());</code></example></remarks>
    [DebuggerStepThrough]
    public static void SetDefault(IBusinessCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        lock (SyncRoot)
        {
            defaultCalendar = calendar;
        }
    }

    /// <summary>
    /// Resolves a calendar by culture.
    /// </summary>
    /// <param name="culture">The culture to resolve, or current culture when omitted.</param>
    /// <returns>The resolved calendar.</returns>
    /// <remarks><example><code>var calendar = BusinessCalendars.Resolve(CultureInfo.CurrentCulture);</code></example></remarks>
    [DebuggerStepThrough]
    public static IBusinessCalendar Resolve(CultureInfo culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;

        lock (SyncRoot)
        {
            if (!string.IsNullOrWhiteSpace(culture.Name) && calendars.TryGetValue(culture.Name, out var exact))
            {
                return exact;
            }

            var countryCode = TryGetCountryCode(culture);
            if (!string.IsNullOrWhiteSpace(countryCode) && calendars.TryGetValue(countryCode, out var country))
            {
                return country;
            }

            if (calendars.TryGetValue(culture.TwoLetterISOLanguageName, out var language))
            {
                return language;
            }

            return defaultCalendar;
        }
    }

    /// <summary>
    /// Resolves a calendar by country, culture, or language code.
    /// </summary>
    /// <param name="code">The country, culture, or language code to resolve.</param>
    /// <returns>The resolved calendar.</returns>
    /// <remarks><example><code>var calendar = BusinessCalendars.Resolve("NL");</code></example></remarks>
    [DebuggerStepThrough]
    public static IBusinessCalendar Resolve(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Resolve(CultureInfo.CurrentCulture);
        }

        lock (SyncRoot)
        {
            return calendars.TryGetValue(code.Trim(), out var calendar)
                ? calendar
                : defaultCalendar;
        }
    }

    /// <summary>
    /// Resets registrations and the default calendar.
    /// </summary>
    /// <remarks><example><code>BusinessCalendars.ResetDefaults();</code></example></remarks>
    [DebuggerStepThrough]
    public static void ResetDefaults()
    {
        lock (SyncRoot)
        {
            calendars = new Dictionary<string, IBusinessCalendar>(StringComparer.OrdinalIgnoreCase);
            defaultCalendar = new BusinessCalendar();
        }
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
