// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.DataPorter;

/// <summary>
/// Configures a property for data export/import operations.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class DataPorterColumnAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataPorterColumnAttribute"/> class.
    /// </summary>
    public DataPorterColumnAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataPorterColumnAttribute"/> class.
    /// </summary>
    /// <param name="name">The column header name.</param>
    public DataPorterColumnAttribute(string name)
    {
        this.Name = name;
    }

    /// <summary>
    /// Gets or sets the column header name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the column order (0-based). -1 means use natural order.
    /// </summary>
    public int Order { get; set; } = -1;

    /// <summary>
    /// Gets or sets the format string for the value.
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Gets or sets the column width (for Excel/PDF). -1 means auto-fit.
    /// </summary>
    public double Width { get; set; } = -1;

    /// <summary>
    /// Gets or sets the value to display when null.
    /// </summary>
    public string NullValue { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is required for import.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the custom error message when required validation fails.
    /// </summary>
    public string RequiredMessage { get; set; }

    /// <summary>
    /// Gets or sets the horizontal alignment.
    /// </summary>
    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;

    /// <summary>
    /// Gets or sets the vertical alignment.
    /// </summary>
    public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Middle;

    /// <summary>
    /// Gets or sets a value indicating whether to include in export. Default is true.
    /// </summary>
    public bool Export { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include in import. Default is true.
    /// </summary>
    public bool Import { get; set; } = true;
}
