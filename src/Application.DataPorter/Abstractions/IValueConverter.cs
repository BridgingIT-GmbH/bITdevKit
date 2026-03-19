// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Converts values during export/import operations.
/// </summary>
public interface IValueConverter
{
    /// <summary>
    /// Converts a value for export.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="context">The conversion context.</param>
    /// <returns>The converted value for export.</returns>
    object ConvertToExport(object value, ValueConversionContext context);

    /// <summary>
    /// Converts a value from import.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="context">The conversion context.</param>
    /// <returns>The converted value for import.</returns>
    object ConvertFromImport(object value, ValueConversionContext context);
}

/// <summary>
/// Generic version for type-safe converters.
/// </summary>
/// <typeparam name="TProperty">The property type to convert.</typeparam>
public interface IValueConverter<TProperty> : IValueConverter
{
    /// <summary>
    /// Converts a typed value for export.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="context">The conversion context.</param>
    /// <returns>The converted value for export.</returns>
    object ConvertToExport(TProperty value, ValueConversionContext context);

    /// <summary>
    /// Converts a value from import to the property type.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="context">The conversion context.</param>
    /// <returns>The converted typed value for import.</returns>
    new TProperty ConvertFromImport(object value, ValueConversionContext context);
}
