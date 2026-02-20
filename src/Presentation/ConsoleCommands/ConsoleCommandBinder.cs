// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

/// <summary>Command binding utility (reflection + caching).</summary>
public static class ConsoleCommandBinder
{
    private static readonly Dictionary<Type, ConsoleCommandMeta> cache = [];
    private static readonly object sync = new();

    /// <summary>
    /// Attempts to bind tokens to the properties of a console command.
    /// </summary>
    /// <param name="cmd">The command instance to bind values to.</param>
    /// <param name="tokens">The string tokens representing option/argument values.</param>
    /// <returns>Tuple of success flag and list of error messages.</returns>
    public static (bool ok, List<string> errors) TryBind(IConsoleCommand cmd, string[] tokens)
    {
        var meta = GetMeta(cmd.GetType());
        var errors = new List<string>();
        var options = ParseOptions(tokens);
        var consumedIndices = new HashSet<int>(options.SelectMany(kvp => kvp.Value.Indices));
        var positionals = tokens.Where((t, i) => !consumedIndices.Contains(i)).ToList();

        // Bind options
        foreach (var o in meta.Options)
        {
            if (options.TryGetValue(o.Name, out var present) || (o.Alias != null && options.TryGetValue(o.Alias, out present)))
            {
                var valToken = present.Value;
                object value = null;
                if (o.Property.PropertyType == typeof(bool))
                {
                    // flag: presence sets true unless explicit false given
                    var raw = valToken;
                    value = raw is null || raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "1";
                    if (raw is not null && (raw.Equals("false", StringComparison.OrdinalIgnoreCase) || raw == "0"))
                    {
                        value = false;
                    }
                }
                else
                {
                    if (valToken is null)
                    {
                        errors.Add($"Option --{o.Name} requires a value.");
                        continue;
                    }
                    if (!TryConvert(valToken, o.Property.PropertyType, out value))
                    {
                        errors.Add($"Invalid value '{valToken}' for --{o.Name} (expected {FriendlyType(o.Property.PropertyType)}). ");
                        continue;
                    }
                }
                o.Property.SetValue(cmd, value);
            }
            else
            {
                if (o.Required)
                {
                    errors.Add($"Missing required option --{o.Name}.");
                }
                else if (o.Default is not null)
                {
                    o.Property.SetValue(cmd, o.Default);
                }
            }
        }

        // Bind positional
        foreach (var a in meta.Arguments.OrderBy(a => a.Order))
        {
            if (a.Order < positionals.Count)
            {
                var raw = positionals[a.Order];
                if (!TryConvert(raw, a.Property.PropertyType, out var value))
                {
                    errors.Add($"Invalid value '{raw}' for argument #{a.Order} ({a.Property.Name}) expected {FriendlyType(a.Property.PropertyType)}.");
                    continue;
                }
                a.Property.SetValue(cmd, value);
            }
            else if (a.Required)
            {
                errors.Add($"Missing required argument at position {a.Order} ({a.Property.Name}).");
            }
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// Writes command help including options and arguments.
    /// </summary>
    /// <param name="console">The console to write help to.</param>
    /// <param name="cmd">The command instance to generate help for.</param>
    /// <param name="detailed">Whether to include detailed option examples.</param>
    public static void WriteHelp(IAnsiConsole console, IConsoleCommand cmd, bool detailed = false)
    {
        try
        {
            var meta = GetMeta(cmd.GetType());
            var table = new Table().Border(TableBorder.Minimal).Title($"[bold cyan]{cmd.Name}[/]");
            table.AddColumn("Key"); table.AddColumn("Value");
            table.AddRow("Description", cmd.Description);
            if (meta.Options.Any())
            {
                var optText = string.Join("\n", meta.Options.Select(o => $"--{o.Name}{(o.Alias != null ? "/-" + o.Alias : "")}: {o.Description} {(o.Required ? "(required)" : "")} {(o.Default != null ? "(default=" + o.Default + ")" : "")} ({FriendlyType(o.Property.PropertyType)})")).Replace("[]", " array");
                table.AddRow("Options", optText);
            }
            if (meta.Arguments.Any())
            {
                var argText = string.Join("\n", meta.Arguments.OrderBy(a => a.Order).Select(a => $"{a.Order}: {a.Property.Name} {a.Description ?? ""} {(a.Required ? "(required)" : "")} ({FriendlyType(a.Property.PropertyType)})"));
                table.AddRow("Arguments", argText);
            }
            if (detailed && meta.Options.Any())
            {
                table.AddRow("Example", $"{cmd.Name} " + string.Join(' ', meta.Options.Take(2).Select(o => $"--{o.Name}{(o.Property.PropertyType != typeof(bool) ? "=value" : "")}")));
            }
            console.Write(table);
        }
        catch (Exception ex)
        {
            console.WriteException(ex, ExceptionFormats.ShortenEverything | ExceptionFormats.ShowLinks);
        }
    }

    /// <summary>
    /// Gets cached or newly built metadata for a command type.
    /// </summary>
    /// <param name="t">The command type.</param>
    /// <returns>The metadata for the command.</returns>
    private static ConsoleCommandMeta GetMeta(Type t)
    {
        lock (sync)
        {
            if (!cache.TryGetValue(t, out var meta))
            {
                var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public);
                var options = new List<OptionMeta>();
                var args = new List<ArgumentMeta>();
                foreach (var p in props)
                {
                    var opt = p.GetCustomAttribute<ConsoleCommandOptionAttribute>();
                    if (opt is not null)
                    {
                        options.Add(new OptionMeta
                        {
                            Name = opt.Name,
                            Alias = opt.Alias,
                            Description = opt.Description ?? p.Name,
                            Required = opt.Required,
                            Default = opt.Default,
                            Property = p
                        });
                        continue;
                    }
                    var arg = p.GetCustomAttribute<ConsoleCommandArgumentAttribute>();
                    if (arg is not null)
                    {
                        args.Add(new ArgumentMeta
                        {
                            Order = arg.Order,
                            Description = arg.Description,
                            Required = arg.Required,
                            Property = p
                        });
                    }
                }
                meta = new ConsoleCommandMeta(options, args);
                cache[t] = meta;
            }
            return meta;
        }
    }
    private static string FriendlyType(Type t) => t.Name switch
    {
        nameof(String) => "string",
        nameof(Int32) => "int",
        nameof(Int64) => "long",
        nameof(Boolean) => "bool",
        nameof(DateTime) => "datetime",
        nameof(Guid) => "guid",
        nameof(TimeSpan) => "timespan",
        _ => t.IsEnum ? "enum" : t.Name.ToLowerInvariant()
    };

    private static bool TryConvert(string raw, Type type, out object value)
    {
        value = null;
        if (type == typeof(string)) { value = raw; return true; }
        if (type == typeof(int) || type == typeof(int?)) { if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) { value = i; return true; } return false; }
        if (type == typeof(long) || type == typeof(long?)) { if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) { value = l; return true; } return false; }
        if (type == typeof(bool) || type == typeof(bool?)) { if (bool.TryParse(raw, out var b)) { value = b; return true; } if (raw == "1") { value = true; return true; } if (raw == "0") { value = false; return true; } return false; }
        if (type == typeof(Guid) || type == typeof(Guid?)) { if (Guid.TryParse(raw, out var g)) { value = g; return true; } return false; }
        if (type == typeof(DateTime) || type == typeof(DateTime?)) { if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt)) { value = dt; return true; } return false; }
        if (type == typeof(TimeSpan) || type == typeof(TimeSpan?)) { if (TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out var ts)) { value = ts; return true; } return false; }
        if (type.IsEnum) { if (Enum.TryParse(type, raw, true, out var ev)) { value = ev; return true; } return false; }
        // fallback
        try
        {
            value = Convert.ChangeType(raw, type, CultureInfo.InvariantCulture);
            return true;
        }
        catch { return false; }
    }

    private static Dictionary<string, ParsedOption> ParseOptions(string[] tokens)
    {
        var dict = new Dictionary<string, ParsedOption>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < tokens.Length; i++)
        {
            var t = tokens[i];
            if (t.StartsWith("--"))
            {
                var trimmed = t.Substring(2);
                string name; string val = null;
                var eqIdx = trimmed.IndexOf('=');
                if (eqIdx >= 0)
                {
                    name = trimmed[..eqIdx];
                    val = trimmed[(eqIdx + 1)..];
                }
                else
                {
                    name = trimmed;
                    // value maybe next token if not another option
                    if (i + 1 < tokens.Length && !tokens[i + 1].StartsWith("-"))
                    {
                        val = tokens[i + 1];
                        dict[name] = new ParsedOption(val, [i, i + 1]);
                        i++;
                        continue;
                    }
                }
                dict[name] = new ParsedOption(val, [i]);
            }
            else if (t.StartsWith('-') && t.Length > 1 && !t.StartsWith("--"))
            {
                var alias = t[1..];
                string val = null;
                if (i + 1 < tokens.Length && !tokens[i + 1].StartsWith("-"))
                {
                    val = tokens[i + 1];
                    dict[alias] = new ParsedOption(val, [i, i + 1]);
                    i++;
                }
                else
                {
                    dict[alias] = new ParsedOption(null, [i]);
                }
            }
        }
        return dict;
    }

    private record ParsedOption(string Value, int[] Indices);

    private record ConsoleCommandMeta(IReadOnlyList<OptionMeta> Options, IReadOnlyList<ArgumentMeta> Arguments);

    private record OptionMeta
    {
        public string Name { get; init; } = default!;

        public string Alias { get; init; }

        public string Description { get; init; } = string.Empty;

        public bool Required { get; init; }

        public object Default { get; init; }

        public PropertyInfo Property { get; init; } = default!;
    }

    private record ArgumentMeta
    {
        public int Order { get; init; }

        public string Description { get; init; }

        public bool Required { get; init; }

        public PropertyInfo Property { get; init; } = default!;
    }
}
