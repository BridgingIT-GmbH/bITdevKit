// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;

/// <summary>
/// Default business calendar resolver backed by <see cref="BusinessCalendarOptions"/>.
/// </summary>
/// <remarks><example><code>var calendar = resolver.Resolve(CultureInfo.CurrentCulture);</code></example></remarks>
public sealed class BusinessCalendarResolver(BusinessCalendarOptions options, IServiceProvider serviceProvider) : IBusinessCalendarResolver
{
    /// <inheritdoc />
    public IBusinessCalendar Resolve(CultureInfo culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;

        foreach (var code in GetResolutionCodes(culture))
        {
            var registration = options.Registrations.LastOrDefault(item => item.Codes.Contains(code, StringComparer.OrdinalIgnoreCase));
            if (registration is not null)
            {
                return registration.Calendar ?? registration.Factory(serviceProvider);
            }
        }

        return options.DefaultCalendarFactory?.Invoke(serviceProvider) ?? options.DefaultCalendar;
    }

    /// <inheritdoc />
    public IBusinessCalendar Resolve(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return this.Resolve(CultureInfo.CurrentCulture);
        }

        var registration = options.Registrations.LastOrDefault(item => item.Codes.Contains(code.Trim(), StringComparer.OrdinalIgnoreCase));
        if (registration is not null)
        {
            return registration.Calendar ?? registration.Factory(serviceProvider);
        }

        return options.DefaultCalendarFactory?.Invoke(serviceProvider) ?? options.DefaultCalendar;
    }

    private static IEnumerable<string> GetResolutionCodes(CultureInfo culture)
    {
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
