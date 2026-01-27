// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.DataPorter;

/// <summary>
/// Specifies a custom type converter for a property during export/import operations.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class DataPorterConverterAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataPorterConverterAttribute"/> class.
    /// </summary>
    /// <param name="converterType">The converter type. Must implement <see cref="IValueConverter"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when converterType is null.</exception>
    /// <exception cref="ArgumentException">Thrown when converterType does not implement IValueConverter.</exception>
    public DataPorterConverterAttribute(Type converterType)
    {
        ArgumentNullException.ThrowIfNull(converterType);

        if (!typeof(IValueConverter).IsAssignableFrom(converterType))
        {
            throw new ArgumentException(
                $"Converter type must implement {nameof(IValueConverter)}",
                nameof(converterType));
        }

        this.ConverterType = converterType;
    }

    /// <summary>
    /// Gets the converter type.
    /// </summary>
    public Type ConverterType { get; }
}
