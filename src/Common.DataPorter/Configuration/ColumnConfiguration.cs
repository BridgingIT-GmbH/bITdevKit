// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.DataPorter;

using System.Reflection;

/// <summary>
/// Represents the configuration for a single column in export operations.
/// </summary>
public sealed class ColumnConfiguration : IColumnConfiguration
{
    /// <summary>
    /// Gets or sets the property name this column maps to.
    /// </summary>
    public required string PropertyName { get; init; }

    /// <summary>
    /// Gets or sets the column header name.
    /// </summary>
    public string HeaderName { get; set; }

    /// <summary>
    /// Gets or sets the column order (0-based). -1 means use natural order.
    /// </summary>
    public int Order { get; set; } = -1;

    /// <summary>
    /// Gets or sets the format string for the column value.
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Gets or sets the column width (for Excel/PDF). -1 means auto-fit.
    /// </summary>
    public double Width { get; set; } = -1;

    /// <summary>
    /// Gets or sets the value to display when the property value is null.
    /// </summary>
    public string NullValue { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this column should be ignored.
    /// </summary>
    public bool Ignore { get; set; }

    /// <summary>
    /// Gets or sets the horizontal alignment for the column.
    /// </summary>
    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;

    /// <summary>
    /// Gets or sets the vertical alignment for the column.
    /// </summary>
    public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Middle;

    /// <summary>
    /// Gets or sets the value converter for this column.
    /// </summary>
    public IValueConverter Converter { get; set; }

    /// <summary>
    /// Gets or sets the property info for reflection-based value access.
    /// </summary>
    public PropertyInfo PropertyInfo { get; set; }

    /// <summary>
    /// Gets or sets the value getter function.
    /// </summary>
    internal Func<object, object> ValueGetter { get; set; }

    /// <summary>
    /// Gets or sets the conditional styles for this column.
    /// </summary>
    public List<ConditionalStyle> ConditionalStyles { get; set; } = [];

    /// <summary>
    /// Gets the value from the source object.
    /// </summary>
    /// <param name="source">The source object.</param>
    /// <returns>The property value.</returns>
    public object GetValue(object source)
    {
        if (source is null)
        {
            return null;
        }

        if (this.ValueGetter is not null)
        {
            return this.ValueGetter(source);
        }

        return this.PropertyInfo?.GetValue(source);
    }
}

/// <summary>
/// Represents a conditional style for a column.
/// </summary>
public sealed class ConditionalStyle
{
    /// <summary>
    /// Gets or sets the condition function.
    /// </summary>
    public required Func<object, bool> Condition { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the text should be bold.
    /// </summary>
    public bool IsBold { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the text should be italic.
    /// </summary>
    public bool IsItalic { get; set; }

    /// <summary>
    /// Gets or sets the foreground color (hex format, e.g., "#FF0000").
    /// </summary>
    public string ForegroundColor { get; set; }

    /// <summary>
    /// Gets or sets the background color (hex format, e.g., "#FFFF00").
    /// </summary>
    public string BackgroundColor { get; set; }
}
