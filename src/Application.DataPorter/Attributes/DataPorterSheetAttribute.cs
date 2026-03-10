// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Configures the sheet/section for a class during export/import operations.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class DataPorterSheetAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataPorterSheetAttribute"/> class.
    /// </summary>
    /// <param name="name">The sheet/section name.</param>
    public DataPorterSheetAttribute(string name)
    {
        this.Name = name;
    }

    /// <summary>
    /// Gets the sheet/section name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the sheet index (for import, 0-based). -1 means use Name.
    /// </summary>
    public int Index { get; set; } = -1;
}
