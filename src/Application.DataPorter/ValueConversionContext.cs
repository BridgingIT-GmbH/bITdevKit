// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Provides context information for value conversion operations.
/// </summary>
public sealed record ValueConversionContext
{
    /// <summary>
    /// Gets the property name being converted.
    /// </summary>
    public required string PropertyName { get; init; }

    /// <summary>
    /// Gets the property type being converted.
    /// </summary>
    public required Type PropertyType { get; init; }

    /// <summary>
    /// Gets the source/target type containing the property.
    /// </summary>
    public required Type EntityType { get; init; }

    /// <summary>
    /// Gets the format string, if specified.
    /// </summary>
    public string Format { get; init; }

    /// <summary>
    /// Gets the culture to use for conversion.
    /// </summary>
    public System.Globalization.CultureInfo Culture { get; init; } = System.Globalization.CultureInfo.InvariantCulture;

    /// <summary>
    /// Gets additional parameters for the conversion.
    /// </summary>
    public IReadOnlyDictionary<string, object> Parameters { get; init; } = new Dictionary<string, object>();
}
