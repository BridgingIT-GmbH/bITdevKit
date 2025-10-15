// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;
using System.Text.Json;
/// <summary>
/// Adds JSON-based parsing for <see cref="FilterModel"/> so Minimal APIs can bind it
/// from a single query value (e.g., ?filter={...json...}).
///
/// Usage:
/// - At app startup, inject your JSON options (with converters):
///   FilterModel.ConfigureJsonOptions(DefaultSystemTextJsonSerializerOptions.Create());
///
/// - GET endpoint (query):
///   group.MapGet("", (HttpContext ctx, [FromServices] IRequester req, [FromQuery] FilterModel filter)
///       => CustomerFindAll(ctx, req, filter));
///
/// Notes:
/// - Only JSON-in-query is supported (the value must start with '{' or '[').
/// - Clients must URL-encode the JSON payload in the query string.
/// </summary>
public partial class FilterModel
{
    // Pluggable JSON options (default to Web-like behavior if not set by host).
    private static JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Configures the JSON serializer options used by <see cref="TryParse(string, IFormatProvider, out FilterModel)"/>.
    /// Call this once at application startup from the hosting (web) project to inject your
    /// custom converters and settings.
    /// </summary>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> to use for deserialization.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public static void ConfigureJson(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        jsonOptions = options;
    }

    /// <summary>
    /// Attempts to parse a <see cref="FilterModel"/> from a JSON string.
    /// The value must be a JSON object/array (e.g., starts with '{' or '[').
    /// </summary>
    /// <param name="value">The JSON value (URL-decoded string from the query parameter).</param>
    /// <param name="provider">Unused.</param>
    /// <param name="result">The parsed model on success.</param>
    /// <returns>true if JSON deserialization succeeded; otherwise false.</returns>
    public static bool TryParse(string value, IFormatProvider provider, out FilterModel result)
    {
        result = new FilterModel();

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var span = value.AsSpan().TrimStart();
        var looksJson = span.Length > 0 && (span[0] == '{' || span[0] == '[');
        if (!looksJson)
        {
            return false;
        }

        try
        {
            var des = JsonSerializer.Deserialize<FilterModel>(value, jsonOptions);
            if (des == null)
            {
                result = new FilterModel();
                return true;
            }

            result = des;

            return true;
        }
        catch
        {
            return false;
        }
    }
}