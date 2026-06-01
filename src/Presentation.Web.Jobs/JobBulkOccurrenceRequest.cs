// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Jobs;

/// <summary>
/// Represents a bulk occurrence operation request body.
/// </summary>
public sealed class JobBulkOccurrenceRequest
{
    /// <summary>
    /// Gets or sets the selected occurrence identifiers.
    /// </summary>
    public IReadOnlyCollection<Guid> OccurrenceIds { get; set; }

    /// <summary>
    /// Gets or sets the optional operator reason.
    /// </summary>
    public string Reason { get; set; }
}