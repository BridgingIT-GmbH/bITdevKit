// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;

/// <summary>
/// Default relative-time provider with built-in relative-time languages.
/// </summary>
/// <remarks><example><code>var provider = new RelativeTimeTextProvider();</code></example></remarks>
public sealed class RelativeTimeTextProvider : IRelativeTimeTextProvider
{
    /// <summary>Gets the default relative-time provider.</summary>
    public static RelativeTimeTextProvider Default { get; } = new();

    /// <summary>Gets a provider backed by the global <see cref="RelativeTimeLanguages"/> registry.</summary>
    public static RelativeTimeTextProvider Global { get; } = new(useGlobalRegistry: true);

    private readonly IReadOnlyDictionary<string, IRelativeTimeLanguage> languages;
    private readonly string fallbackLanguageCode;
    private readonly bool useGlobalRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelativeTimeTextProvider"/> class.
    /// </summary>
    /// <param name="languages">Optional language implementations. Built-in languages are used when omitted.</param>
    /// <param name="fallbackLanguageCode">The fallback language code.</param>
    public RelativeTimeTextProvider(IEnumerable<IRelativeTimeLanguage> languages = null, string fallbackLanguageCode = "en")
    {
        var configuredLanguages = languages?.ToArray() ??
        [
            new EnglishRelativeTimeLanguage(),
            new GermanRelativeTimeLanguage(),
            new FrenchRelativeTimeLanguage(),
            new DutchRelativeTimeLanguage(),
            new SpanishRelativeTimeLanguage(),
            new ItalianRelativeTimeLanguage()
        ];
        this.languages = configuredLanguages.ToDictionary(language => language.LanguageCode, StringComparer.OrdinalIgnoreCase);
        this.fallbackLanguageCode = fallbackLanguageCode;
    }

    private RelativeTimeTextProvider(bool useGlobalRegistry)
    {
        this.languages = new Dictionary<string, IRelativeTimeLanguage>();
        this.fallbackLanguageCode = "en";
        this.useGlobalRegistry = useGlobalRegistry;
    }

    /// <inheritdoc />
    public string Now(CultureInfo culture, bool shortText) => this.Resolve(culture).Now(shortText);

    /// <inheritdoc />
    public string FormatUnit(RelativeTimeUnit unit, long value, CultureInfo culture, bool shortText)
        => this.Resolve(culture).FormatUnit(unit, value, shortText);

    /// <inheritdoc />
    public string FormatRelative(string durationText, RelativeTimeDirection direction, CultureInfo culture, bool shortText)
    {
        var language = this.Resolve(culture);
        return direction == RelativeTimeDirection.Past
            ? language.FormatPast(durationText, shortText)
            : language.FormatFuture(durationText, shortText);
    }

    private IRelativeTimeLanguage Resolve(CultureInfo culture)
    {
        if (this.useGlobalRegistry)
        {
            return RelativeTimeLanguages.Resolve(culture, this.fallbackLanguageCode);
        }

        if (this.languages.TryGetValue(culture.Name, out var exact))
        {
            return exact;
        }

        if (this.languages.TryGetValue(culture.TwoLetterISOLanguageName, out var neutral))
        {
            return neutral;
        }

        return this.languages.TryGetValue(this.fallbackLanguageCode, out var fallback)
            ? fallback
            : this.languages.Values.First();
    }
}
