// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Text.Json;

/// <summary>
/// Configuration options for the JSON provider.
/// </summary>
public sealed class JsonConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether to format the JSON with indentation.
    /// </summary>
    public bool WriteIndented { get; set; } = true;

    /// <summary>
    /// Gets or sets the property naming policy.
    /// </summary>
    public JsonNamingPolicy PropertyNamingPolicy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to ignore null values.
    /// </summary>
    public bool IgnoreNullValues { get; set; } = false;

    /// <summary>
    /// Gets or sets the date format string.
    /// </summary>
    public string DateFormat { get; set; } = "yyyy-MM-ddTHH:mm:ssZ";

    /// <summary>
    /// Gets the JsonSerializerOptions based on this configuration.
    /// </summary>
    public JsonSerializerOptions GetSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = this.WriteIndented,
            PropertyNamingPolicy = this.PropertyNamingPolicy,
            DefaultIgnoreCondition = this.IgnoreNullValues
                ? System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                : System.Text.Json.Serialization.JsonIgnoreCondition.Never
        };

        return options;
    }
}
