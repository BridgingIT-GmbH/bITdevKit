// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Defines the outcome of a change-history capture attempt.
/// </summary>
/// <example>
/// <code>
/// var status = ChangeHistoryCaptureStatus.Captured;
/// </code>
/// </example>
public enum ChangeHistoryCaptureStatus
{
    /// <summary>
    /// Property-level change data was captured.
    /// </summary>
    Captured,

    /// <summary>
    /// Capture was skipped for the configured source.
    /// </summary>
    Skipped,

    /// <summary>
    /// Capture failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Only a summary row was captured.
    /// </summary>
    Summary
}
