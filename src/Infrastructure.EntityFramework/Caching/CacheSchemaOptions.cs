// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

/// <summary>Selects validated physical cache table names. Deploy matching host-owned migration artifacts before marking the database ready.</summary>
/// <example><code>var schema = new CacheSchemaOptions { TablePrefix = "__Caching_" };</code></example>
public sealed record CacheSchemaOptions
{
    /// <summary>The default table prefix following the DevKit feature-owned persistence convention.</summary>
    public const string DefaultTablePrefix = "__Caching_";

    /// <summary>Gets an optional relational schema. SQLite requires null.</summary>
    public string Schema { get; init; }

    /// <summary>Gets the bounded feature-owned table family prefix. Changing this requires separately deployed artifacts.</summary>
    public string TablePrefix { get; init; } = DefaultTablePrefix;

    /// <summary>Validates physical identifiers before model building or database access.</summary>
    /// <exception cref="ArgumentException">A schema or table prefix is not a bounded ASCII identifier.</exception>
    /// <example><code>new CacheSchemaOptions { TablePrefix = "CatalogCache" }.Validate();</code></example>
    public void Validate()
    {
        if (!Valid(this.TablePrefix, false, true) || !Valid(this.Schema, true, false))
        {
            throw new ArgumentException("Cache schemas and prefixes contain at most 32 ASCII letters/digits/underscores. Schemas start with a letter; table prefixes may also start with an underscore.");
        }
    }

    private static bool Valid(string value, bool optional, bool allowUnderscore) => value is null ? optional : value.Length is > 0 and <= 32 &&
        (char.IsAsciiLetter(value[0]) || (allowUnderscore && value[0] == '_')) && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
}