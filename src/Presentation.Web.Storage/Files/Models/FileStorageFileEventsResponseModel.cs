// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

/// <summary>
/// Represents a file event query response for a provider-backed monitoring location.
/// </summary>
public class FileStorageFileEventsResponseModel
{
    /// <summary>
    /// Gets or sets the file storage provider registration name.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the monitored location name used for the query.
    /// </summary>
    public string LocationName { get; set; }

    /// <summary>
    /// Gets or sets the number of events returned in the response.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the file events returned by the query.
    /// </summary>
    public FileStorageFileEventModel[] Events { get; set; } = [];
}
