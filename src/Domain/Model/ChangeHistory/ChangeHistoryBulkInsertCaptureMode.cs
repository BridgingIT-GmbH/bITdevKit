// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Defines the amount of ChangeHistory data captured for a native entity bulk insert.
/// </summary>
/// <example>
/// <code>
/// options.Track&lt;Customer&gt;()
///     .CaptureBulkInserts(ChangeHistoryBulkInsertCaptureMode.Summary);
/// </code>
/// </example>
public enum ChangeHistoryBulkInsertCaptureMode
{
    /// <summary>
    /// Native bulk inserts do not produce ChangeHistory rows.
    /// </summary>
    Disabled,

    /// <summary>
    /// One non-restoreable summary row is stored for the complete native bulk insert.
    /// </summary>
    Summary,

    /// <summary>
    /// Non-restoreable initial property values are stored for every inserted entity.
    /// </summary>
    Detailed
}