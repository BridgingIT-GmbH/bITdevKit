// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Utilities;

/// <summary>
/// Enum representing the supported formats for exporting log entries.
/// </summary>
public enum LogEntryExportFormat
{
    /// <summary>
    /// Comma-separated values format.
    /// </summary>
    Csv,

    /// <summary>
    /// JSON format.
    /// </summary>
    Json,

    /// <summary>
    /// Plain text format.
    /// </summary>
    Txt
}