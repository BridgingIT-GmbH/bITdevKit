// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides localized relative-time text.
/// </summary>
/// <remarks><example><code>var text = language.FormatUnit(RelativeTimeUnit.Minute, 3, false);</code></example></remarks>
public interface IRelativeTimeLanguage
{
    /// <summary>Gets the language code.</summary>
    string LanguageCode { get; }

    /// <summary>Formats the current moment.</summary>
    /// <param name="shortText">Whether short text should be used.</param>
    /// <returns>The localized now text.</returns>
    string Now(bool shortText);

    /// <summary>Formats a unit value.</summary>
    /// <param name="unit">The unit to format.</param>
    /// <param name="value">The unit value.</param>
    /// <param name="shortText">Whether short text should be used.</param>
    /// <returns>The localized duration text.</returns>
    string FormatUnit(RelativeTimeUnit unit, long value, bool shortText);

    /// <summary>Formats past relative text.</summary>
    /// <param name="durationText">The already localized duration text.</param>
    /// <param name="shortText">Whether short text should be used.</param>
    /// <returns>The localized past text.</returns>
    string FormatPast(string durationText, bool shortText);

    /// <summary>Formats future relative text.</summary>
    /// <param name="durationText">The already localized duration text.</param>
    /// <param name="shortText">Whether short text should be used.</param>
    /// <returns>The localized future text.</returns>
    string FormatFuture(string durationText, bool shortText);
}
