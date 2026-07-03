// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// English relative-time language.
/// </summary>
/// <remarks><example><code>var language = new EnglishRelativeTimeLanguage();</code></example></remarks>
public sealed class EnglishRelativeTimeLanguage : IRelativeTimeLanguage
{
    /// <inheritdoc />
    public string LanguageCode => "en";

    /// <inheritdoc />
    public string Now(bool shortText) => shortText ? "now" : "just now";

    /// <inheritdoc />
    public string FormatUnit(RelativeTimeUnit unit, long value, bool shortText)
    {
        if (shortText)
        {
            return $"{value}{ShortUnit(unit)}";
        }

        var name = unit switch
        {
            RelativeTimeUnit.Millisecond => value == 1 ? "millisecond" : "milliseconds",
            RelativeTimeUnit.Second => value == 1 ? "second" : "seconds",
            RelativeTimeUnit.Minute => value == 1 ? "minute" : "minutes",
            RelativeTimeUnit.Hour => value == 1 ? "hour" : "hours",
            RelativeTimeUnit.Day => value == 1 ? "day" : "days",
            RelativeTimeUnit.Week => value == 1 ? "week" : "weeks",
            RelativeTimeUnit.Month => value == 1 ? "month" : "months",
            RelativeTimeUnit.Year => value == 1 ? "year" : "years",
            _ => "seconds"
        };

        return $"{value} {name}";
    }

    /// <inheritdoc />
    public string FormatPast(string durationText, bool shortText) => $"{durationText} ago";

    /// <inheritdoc />
    public string FormatFuture(string durationText, bool shortText) => $"in {durationText}";

    private static string ShortUnit(RelativeTimeUnit unit) => unit switch
    {
        RelativeTimeUnit.Millisecond => "ms",
        RelativeTimeUnit.Second => "s",
        RelativeTimeUnit.Minute => "m",
        RelativeTimeUnit.Hour => "h",
        RelativeTimeUnit.Day => "d",
        RelativeTimeUnit.Week => "w",
        RelativeTimeUnit.Month => "mo",
        RelativeTimeUnit.Year => "y",
        _ => "s"
    };
}

/// <summary>
/// German relative-time language.
/// </summary>
/// <remarks><example><code>var language = new GermanRelativeTimeLanguage();</code></example></remarks>
public sealed class GermanRelativeTimeLanguage : IRelativeTimeLanguage
{
    /// <inheritdoc />
    public string LanguageCode => "de";

    /// <inheritdoc />
    public string Now(bool shortText) => shortText ? "jetzt" : "gerade eben";

    /// <inheritdoc />
    public string FormatUnit(RelativeTimeUnit unit, long value, bool shortText)
    {
        if (shortText)
        {
            return $"{value} {ShortUnit(unit)}";
        }

        var name = unit switch
        {
            RelativeTimeUnit.Millisecond => value == 1 ? "Millisekunde" : "Millisekunden",
            RelativeTimeUnit.Second => value == 1 ? "Sekunde" : "Sekunden",
            RelativeTimeUnit.Minute => value == 1 ? "Minute" : "Minuten",
            RelativeTimeUnit.Hour => value == 1 ? "Stunde" : "Stunden",
            RelativeTimeUnit.Day => value == 1 ? "Tag" : "Tagen",
            RelativeTimeUnit.Week => value == 1 ? "Woche" : "Wochen",
            RelativeTimeUnit.Month => value == 1 ? "Monat" : "Monaten",
            RelativeTimeUnit.Year => value == 1 ? "Jahr" : "Jahren",
            _ => "Sekunden"
        };

        return $"{value} {name}";
    }

    /// <inheritdoc />
    public string FormatPast(string durationText, bool shortText) => $"vor {durationText}";

    /// <inheritdoc />
    public string FormatFuture(string durationText, bool shortText) => $"in {durationText}";

    private static string ShortUnit(RelativeTimeUnit unit) => unit switch
    {
        RelativeTimeUnit.Millisecond => "ms",
        RelativeTimeUnit.Second => "Sek.",
        RelativeTimeUnit.Minute => "Min.",
        RelativeTimeUnit.Hour => "Std.",
        RelativeTimeUnit.Day => "Tg.",
        RelativeTimeUnit.Week => "Wo.",
        RelativeTimeUnit.Month => "Mon.",
        RelativeTimeUnit.Year => "J.",
        _ => "Sek."
    };
}

/// <summary>
/// French relative-time language.
/// </summary>
/// <remarks><example><code>var language = new FrenchRelativeTimeLanguage();</code></example></remarks>
public sealed class FrenchRelativeTimeLanguage : IRelativeTimeLanguage
{
    /// <inheritdoc />
    public string LanguageCode => "fr";

    /// <inheritdoc />
    public string Now(bool shortText) => shortText ? "maintenant" : "à l'instant";

    /// <inheritdoc />
    public string FormatUnit(RelativeTimeUnit unit, long value, bool shortText)
    {
        if (shortText)
        {
            return $"{value} {ShortUnit(unit)}";
        }

        var name = unit switch
        {
            RelativeTimeUnit.Millisecond => value == 1 ? "milliseconde" : "millisecondes",
            RelativeTimeUnit.Second => value == 1 ? "seconde" : "secondes",
            RelativeTimeUnit.Minute => value == 1 ? "minute" : "minutes",
            RelativeTimeUnit.Hour => value == 1 ? "heure" : "heures",
            RelativeTimeUnit.Day => value == 1 ? "jour" : "jours",
            RelativeTimeUnit.Week => value == 1 ? "semaine" : "semaines",
            RelativeTimeUnit.Month => "mois",
            RelativeTimeUnit.Year => value == 1 ? "an" : "ans",
            _ => "secondes"
        };

        return $"{value} {name}";
    }

    /// <inheritdoc />
    public string FormatPast(string durationText, bool shortText) => $"il y a {durationText}";

    /// <inheritdoc />
    public string FormatFuture(string durationText, bool shortText) => $"dans {durationText}";

    private static string ShortUnit(RelativeTimeUnit unit) => unit switch
    {
        RelativeTimeUnit.Millisecond => "ms",
        RelativeTimeUnit.Second => "s",
        RelativeTimeUnit.Minute => "min",
        RelativeTimeUnit.Hour => "h",
        RelativeTimeUnit.Day => "j",
        RelativeTimeUnit.Week => "sem.",
        RelativeTimeUnit.Month => "mois",
        RelativeTimeUnit.Year => "an",
        _ => "s"
    };
}

/// <summary>
/// Dutch relative-time language.
/// </summary>
/// <remarks><example><code>var language = new DutchRelativeTimeLanguage();</code></example></remarks>
public sealed class DutchRelativeTimeLanguage : IRelativeTimeLanguage
{
    /// <inheritdoc />
    public string LanguageCode => "nl";

    /// <inheritdoc />
    public string Now(bool shortText) => shortText ? "nu" : "zojuist";

    /// <inheritdoc />
    public string FormatUnit(RelativeTimeUnit unit, long value, bool shortText)
    {
        if (shortText)
        {
            return $"{value} {ShortUnit(unit)}";
        }

        var name = unit switch
        {
            RelativeTimeUnit.Millisecond => value == 1 ? "milliseconde" : "milliseconden",
            RelativeTimeUnit.Second => value == 1 ? "seconde" : "seconden",
            RelativeTimeUnit.Minute => value == 1 ? "minuut" : "minuten",
            RelativeTimeUnit.Hour => "uur",
            RelativeTimeUnit.Day => value == 1 ? "dag" : "dagen",
            RelativeTimeUnit.Week => value == 1 ? "week" : "weken",
            RelativeTimeUnit.Month => value == 1 ? "maand" : "maanden",
            RelativeTimeUnit.Year => "jaar",
            _ => "seconden"
        };

        return $"{value} {name}";
    }

    /// <inheritdoc />
    public string FormatPast(string durationText, bool shortText) => $"{durationText} geleden";

    /// <inheritdoc />
    public string FormatFuture(string durationText, bool shortText) => $"over {durationText}";

    private static string ShortUnit(RelativeTimeUnit unit) => unit switch
    {
        RelativeTimeUnit.Millisecond => "ms",
        RelativeTimeUnit.Second => "sec.",
        RelativeTimeUnit.Minute => "min.",
        RelativeTimeUnit.Hour => "u",
        RelativeTimeUnit.Day => "d",
        RelativeTimeUnit.Week => "wk.",
        RelativeTimeUnit.Month => "mnd.",
        RelativeTimeUnit.Year => "jr.",
        _ => "sec."
    };
}

/// <summary>
/// Spanish relative-time language.
/// </summary>
/// <remarks><example><code>var language = new SpanishRelativeTimeLanguage();</code></example></remarks>
public sealed class SpanishRelativeTimeLanguage : IRelativeTimeLanguage
{
    /// <inheritdoc />
    public string LanguageCode => "es";

    /// <inheritdoc />
    public string Now(bool shortText) => shortText ? "ahora" : "ahora mismo";

    /// <inheritdoc />
    public string FormatUnit(RelativeTimeUnit unit, long value, bool shortText)
    {
        if (shortText)
        {
            return $"{value} {ShortUnit(unit)}";
        }

        var name = unit switch
        {
            RelativeTimeUnit.Millisecond => value == 1 ? "milisegundo" : "milisegundos",
            RelativeTimeUnit.Second => value == 1 ? "segundo" : "segundos",
            RelativeTimeUnit.Minute => value == 1 ? "minuto" : "minutos",
            RelativeTimeUnit.Hour => value == 1 ? "hora" : "horas",
            RelativeTimeUnit.Day => value == 1 ? "día" : "días",
            RelativeTimeUnit.Week => value == 1 ? "semana" : "semanas",
            RelativeTimeUnit.Month => value == 1 ? "mes" : "meses",
            RelativeTimeUnit.Year => value == 1 ? "año" : "años",
            _ => "segundos"
        };

        return $"{value} {name}";
    }

    /// <inheritdoc />
    public string FormatPast(string durationText, bool shortText) => $"hace {durationText}";

    /// <inheritdoc />
    public string FormatFuture(string durationText, bool shortText) => $"en {durationText}";

    private static string ShortUnit(RelativeTimeUnit unit) => unit switch
    {
        RelativeTimeUnit.Millisecond => "ms",
        RelativeTimeUnit.Second => "s",
        RelativeTimeUnit.Minute => "min",
        RelativeTimeUnit.Hour => "h",
        RelativeTimeUnit.Day => "d",
        RelativeTimeUnit.Week => "sem.",
        RelativeTimeUnit.Month => "mes",
        RelativeTimeUnit.Year => "a",
        _ => "s"
    };
}

/// <summary>
/// Italian relative-time language.
/// </summary>
/// <remarks><example><code>var language = new ItalianRelativeTimeLanguage();</code></example></remarks>
public sealed class ItalianRelativeTimeLanguage : IRelativeTimeLanguage
{
    /// <inheritdoc />
    public string LanguageCode => "it";

    /// <inheritdoc />
    public string Now(bool shortText) => shortText ? "ora" : "proprio ora";

    /// <inheritdoc />
    public string FormatUnit(RelativeTimeUnit unit, long value, bool shortText)
    {
        if (shortText)
        {
            return $"{value} {ShortUnit(unit)}";
        }

        var name = unit switch
        {
            RelativeTimeUnit.Millisecond => value == 1 ? "millisecondo" : "millisecondi",
            RelativeTimeUnit.Second => value == 1 ? "secondo" : "secondi",
            RelativeTimeUnit.Minute => value == 1 ? "minuto" : "minuti",
            RelativeTimeUnit.Hour => value == 1 ? "ora" : "ore",
            RelativeTimeUnit.Day => value == 1 ? "giorno" : "giorni",
            RelativeTimeUnit.Week => value == 1 ? "settimana" : "settimane",
            RelativeTimeUnit.Month => value == 1 ? "mese" : "mesi",
            RelativeTimeUnit.Year => value == 1 ? "anno" : "anni",
            _ => "secondi"
        };

        return $"{value} {name}";
    }

    /// <inheritdoc />
    public string FormatPast(string durationText, bool shortText) => $"{durationText} fa";

    /// <inheritdoc />
    public string FormatFuture(string durationText, bool shortText) => $"tra {durationText}";

    private static string ShortUnit(RelativeTimeUnit unit) => unit switch
    {
        RelativeTimeUnit.Millisecond => "ms",
        RelativeTimeUnit.Second => "s",
        RelativeTimeUnit.Minute => "min",
        RelativeTimeUnit.Hour => "h",
        RelativeTimeUnit.Day => "g",
        RelativeTimeUnit.Week => "sett.",
        RelativeTimeUnit.Month => "mesi",
        RelativeTimeUnit.Year => "a",
        _ => "s"
    };
}
