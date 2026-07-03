// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Globalization;

/// <summary>
/// Globally registers relative-time languages for convenience formatting APIs.
/// </summary>
/// <remarks><example><code>RelativeTimeLanguages.Register(new DutchRelativeTimeLanguage());</code></example></remarks>
public static class RelativeTimeLanguages
{
    private static readonly Lock SyncRoot = new();
    private static Dictionary<string, IRelativeTimeLanguage> languages = CreateDefaults();
    private static string fallbackLanguageCode = "en";

    /// <summary>
    /// Registers a language globally.
    /// </summary>
    /// <param name="language">The language implementation.</param>
    /// <remarks><example><code>RelativeTimeLanguages.Register(new DutchRelativeTimeLanguage());</code></example></remarks>
    [DebuggerStepThrough]
    public static void Register(IRelativeTimeLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);

        lock (SyncRoot)
        {
            var updated = new Dictionary<string, IRelativeTimeLanguage>(languages, StringComparer.OrdinalIgnoreCase)
            {
                [language.LanguageCode] = language
            };
            languages = updated;
        }
    }

    /// <summary>
    /// Registers languages globally.
    /// </summary>
    /// <param name="languages">The language implementations.</param>
    /// <remarks><example><code>RelativeTimeLanguages.Register([new DutchRelativeTimeLanguage()]);</code></example></remarks>
    [DebuggerStepThrough]
    public static void Register(IEnumerable<IRelativeTimeLanguage> languages)
    {
        ArgumentNullException.ThrowIfNull(languages);

        foreach (var language in languages)
        {
            Register(language);
        }
    }

    /// <summary>
    /// Sets the fallback language code.
    /// </summary>
    /// <param name="languageCode">The fallback language code.</param>
    /// <remarks><example><code>RelativeTimeLanguages.SetFallback("en");</code></example></remarks>
    [DebuggerStepThrough]
    public static void SetFallback(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            throw new ArgumentException("Fallback language code is required.", nameof(languageCode));
        }

        lock (SyncRoot)
        {
            fallbackLanguageCode = languageCode;
        }
    }

    /// <summary>
    /// Resolves a globally registered language by culture.
    /// </summary>
    /// <param name="culture">The culture to resolve.</param>
    /// <returns>The resolved language.</returns>
    /// <remarks><example><code>var language = RelativeTimeLanguages.Resolve(CultureInfo.CurrentUICulture);</code></example></remarks>
    [DebuggerStepThrough]
    public static IRelativeTimeLanguage Resolve(CultureInfo culture)
    {
        return Resolve(culture, null);
    }

    internal static IRelativeTimeLanguage Resolve(CultureInfo culture, string fallbackLanguageCodeOverride)
    {
        culture ??= CultureInfo.CurrentUICulture;

        lock (SyncRoot)
        {
            if (languages.TryGetValue(culture.Name, out var exact))
            {
                return exact;
            }

            if (languages.TryGetValue(culture.TwoLetterISOLanguageName, out var neutral))
            {
                return neutral;
            }

            var fallback = fallbackLanguageCodeOverride ?? fallbackLanguageCode;
            return languages.TryGetValue(fallback, out var fallbackLanguage)
                ? fallbackLanguage
                : languages.Values.First();
        }
    }

    /// <summary>
    /// Resets global language registrations to the built-in defaults.
    /// </summary>
    /// <remarks><example><code>RelativeTimeLanguages.ResetDefaults();</code></example></remarks>
    [DebuggerStepThrough]
    public static void ResetDefaults()
    {
        lock (SyncRoot)
        {
            languages = CreateDefaults();
            fallbackLanguageCode = "en";
        }
    }

    private static Dictionary<string, IRelativeTimeLanguage> CreateDefaults()
    {
        return new Dictionary<string, IRelativeTimeLanguage>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new EnglishRelativeTimeLanguage(),
            ["de"] = new GermanRelativeTimeLanguage(),
            ["fr"] = new FrenchRelativeTimeLanguage(),
            ["nl"] = new DutchRelativeTimeLanguage(),
            ["es"] = new SpanishRelativeTimeLanguage(),
            ["it"] = new ItalianRelativeTimeLanguage()
        };
    }
}
