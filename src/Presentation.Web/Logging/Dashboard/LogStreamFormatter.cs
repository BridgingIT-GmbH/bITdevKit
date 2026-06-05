// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Logging.Dashboard;

using System.Globalization;
using System.Text;
using BridgingIT.DevKit.Application.Utilities;

internal static class LogStreamFormatter
{
    private const string Reset = "\x1b[0m";
    private const string Dim = "\x1b[90m";
    private const string Red = "\x1b[31m";

    public static string Format(LogEntryModel entry)
    {
        var level = DisplayLevel(entry.Level);
        var color = GetLevelColor(level);
        var shortLevel = GetShortLevel(level);
        var builder = new StringBuilder(256);

        builder
            .Append(color)
            .Append(entry.TimeStamp.LocalDateTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(shortLevel.PadRight(5))
            .Append(Reset);

        builder
            .Append(' ')
            .Append(SanitizeInline(entry.Message));

        AppendSegment(builder, "module", entry.ModuleName);

        builder.Append("\r\n");

        if (!string.IsNullOrWhiteSpace(entry.Exception))
        {
            foreach (var line in SanitizeMultiline(entry.Exception).Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                builder
                    .Append(Red)
                    .Append("  ")
                    .Append(line)
                    .Append(Reset)
                    .Append("\r\n");
            }
        }

        return builder.ToString();
    }

    private static void AppendSegment(StringBuilder builder, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        builder
            .Append(' ')
            .Append(Dim)
            .Append(name)
            .Append('=')
            .Append(SanitizeInline(value))
            .Append(Reset);
    }

    private static string DisplayLevel(string level)
    {
        return string.Equals(level, "Critical", StringComparison.OrdinalIgnoreCase) ? "Fatal" : level ?? "-";
    }

    private static string GetShortLevel(string level)
    {
        return level switch
        {
            "Verbose" or "Trace" => "TRC",
            "Debug" => "DBG",
            "Information" => "INF",
            "Warning" => "WRN",
            "Error" => "ERR",
            "Fatal" or "Critical" => "FTL",
            _ => "---"
        };
    }

    private static string GetLevelColor(string level)
    {
        return level switch
        {
            "Verbose" or "Trace" => "\x1b[90m",
            "Debug" => "\x1b[36m",
            "Information" => "\x1b[37m",
            "Warning" => "\x1b[33m",
            "Error" => "\x1b[31m",
            "Fatal" or "Critical" => "\x1b[1;31m",
            _ => "\x1b[37m"
        };
    }

    private static string SanitizeInline(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "-";
        }

        return SanitizeMultiline(value).Replace('\n', ' ');
    }

    private static string SanitizeMultiline(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Replace("\r\n", "\n").Replace('\r', '\n'))
        {
            if (character == '\n' || character == '\t' || !char.IsControl(character))
            {
                builder.Append(character == '\x1b' ? '?' : character);
            }
        }

        return builder.ToString();
    }
}
