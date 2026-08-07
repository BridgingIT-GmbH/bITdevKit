// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory;

using BridgingIT.DevKit.Application.Entities;

/// <summary>
/// Represents the body of a ChangeHistory restore HTTP request.
/// </summary>
/// <example>
/// <code>
/// var request = new ChangeHistoryRestoreRequestModel
/// {
///     Reason = "Undo accidental edit",
///     ExpectedConcurrencyVersion = currentVersion
/// };
/// </code>
/// </example>
public sealed class ChangeHistoryRestoreRequestModel
{
    /// <summary>
    /// Gets or sets the business reason recorded on restore rows.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the expected concurrency version for protected restore operations.
    /// </summary>
    public Guid? ExpectedConcurrencyVersion { get; set; }

    /// <summary>
    /// Gets or sets the restore selection mode.
    /// </summary>
    public ChangeHistoryRestoreMode? RestoreMode { get; set; }
}

/// <summary>
/// Represents a successful ChangeHistory restore HTTP response.
/// </summary>
/// <example>
/// <code>
/// Console.WriteLine(response.RestoredChangeSetId);
/// </code>
/// </example>
public sealed record ChangeHistoryRestoreResponseModel(Guid RestoredChangeSetId, int RestoredPropertyCount);
