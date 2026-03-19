// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Configures the sheet/section for a class during export/import operations.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DataPorterSheetAttribute"/> class.
/// </remarks>
/// <param name="name">The sheet/section name.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class DataPorterSheetAttribute(string name) : Attribute
{

    /// <summary>
    /// Gets the sheet/section name.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets or sets the sheet index (for import, 0-based). -1 means use Name.
    /// </summary>
    public int Index { get; set; } = -1;
}
