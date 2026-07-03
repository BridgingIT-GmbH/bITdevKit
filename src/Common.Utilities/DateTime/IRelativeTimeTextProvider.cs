// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;

/// <summary>
/// Provides relative-time text by culture.
/// </summary>
/// <remarks><example><code>var provider = new RelativeTimeTextProvider();</code></example></remarks>
public interface IRelativeTimeTextProvider
{
    /// <summary>Formats the current moment.</summary>
    string Now(CultureInfo culture, bool shortText);

    /// <summary>Formats a unit value.</summary>
    string FormatUnit(RelativeTimeUnit unit, long value, CultureInfo culture, bool shortText);

    /// <summary>Formats relative text.</summary>
    string FormatRelative(string durationText, RelativeTimeDirection direction, CultureInfo culture, bool shortText);
}
