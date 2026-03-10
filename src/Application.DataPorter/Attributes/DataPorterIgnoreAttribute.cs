// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Marks a property to be ignored during export/import operations.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class DataPorterIgnoreAttribute : Attribute
{
    /// <summary>
    /// Gets or sets a value indicating whether to ignore only during export.
    /// </summary>
    public bool ExportOnly { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to ignore only during import.
    /// </summary>
    public bool ImportOnly { get; set; }
}
