// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Represents the configuration for a single column in export/import operations.
/// </summary>
public interface IColumnConfiguration
{
    /// <summary>
    /// Gets the property name this column maps to.
    /// </summary>
    string PropertyName { get; }

    /// <summary>
    /// Gets the column header name.
    /// </summary>
    string HeaderName { get; }

    /// <summary>
    /// Gets the column order (0-based). -1 means use natural order.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Gets the format string for the column value.
    /// </summary>
    string Format { get; }

    /// <summary>
    /// Gets the column width (for Excel/PDF). -1 means auto-fit.
    /// </summary>
    double Width { get; }

    /// <summary>
    /// Gets the value to display when the property value is null.
    /// </summary>
    string NullValue { get; }

    /// <summary>
    /// Gets a value indicating whether this column should be ignored.
    /// </summary>
    bool Ignore { get; }

    /// <summary>
    /// Gets the horizontal alignment for the column.
    /// </summary>
    HorizontalAlignment HorizontalAlignment { get; }

    /// <summary>
    /// Gets the vertical alignment for the column.
    /// </summary>
    VerticalAlignment VerticalAlignment { get; }

    /// <summary>
    /// Gets the value converter for this column.
    /// </summary>
    IValueConverter Converter { get; }
}

/// <summary>
/// Specifies horizontal alignment options.
/// </summary>
public enum HorizontalAlignment
{
    /// <summary>
    /// Align content to the left.
    /// </summary>
    Left,

    /// <summary>
    /// Center the content.
    /// </summary>
    Center,

    /// <summary>
    /// Align content to the right.
    /// </summary>
    Right
}

/// <summary>
/// Specifies vertical alignment options.
/// </summary>
public enum VerticalAlignment
{
    /// <summary>
    /// Align content to the top.
    /// </summary>
    Top,

    /// <summary>
    /// Center the content vertically.
    /// </summary>
    Middle,

    /// <summary>
    /// Align content to the bottom.
    /// </summary>
    Bottom
}
