// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Specifies the severity level of an error.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>
    /// A warning that doesn't prevent import.
    /// </summary>
    Warning,

    /// <summary>
    /// An error that prevents the row from being imported.
    /// </summary>
    Error,

    /// <summary>
    /// A critical error that may stop the entire import.
    /// </summary>
    Critical
}
