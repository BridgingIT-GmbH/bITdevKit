// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;

/// <summary>
/// Resolves business calendars by culture.
/// </summary>
/// <remarks><example><code>var calendar = resolver.Resolve(CultureInfo.GetCultureInfo("nl-NL"));</code></example></remarks>
public interface IBusinessCalendarResolver
{
    /// <summary>
    /// Resolves a calendar by culture.
    /// </summary>
    /// <param name="culture">The culture to resolve, or current culture when omitted.</param>
    /// <returns>The resolved calendar.</returns>
    /// <remarks><example><code>var calendar = resolver.Resolve(CultureInfo.CurrentCulture);</code></example></remarks>
    IBusinessCalendar Resolve(CultureInfo culture = null);

    /// <summary>
    /// Resolves a calendar by country, culture, or language code.
    /// </summary>
    /// <param name="code">The country, culture, or language code to resolve.</param>
    /// <returns>The resolved calendar.</returns>
    /// <remarks><example><code>var calendar = resolver.Resolve("NL");</code></example></remarks>
    IBusinessCalendar Resolve(string code);
}
