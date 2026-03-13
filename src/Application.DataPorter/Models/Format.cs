// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Specifies the supported data porter formats for import and export operations.
/// </summary>
public enum Format
{
    /// <summary>
    /// Microsoft Excel format (.xlsx).
    /// </summary>
    Excel,

    /// <summary>
    /// Comma-Separated Values format (.csv).
    /// </summary>
    Csv,

    /// <summary>
    /// Typed-row Comma-Separated Values format (.csv) for hierarchical object graphs.
    /// </summary>
    CsvTyped,

    /// <summary>
    /// JavaScript Object Notation format (.json).
    /// </summary>
    Json,

    /// <summary>
    /// Extensible Markup Language format (.xml).
    /// </summary>
    Xml,

    /// <summary>
    /// Portable Document Format (.pdf). Export only.
    /// </summary>
    Pdf
}
