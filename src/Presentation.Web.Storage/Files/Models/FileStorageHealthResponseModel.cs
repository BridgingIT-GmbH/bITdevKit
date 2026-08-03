// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

/// <summary>
/// Represents the health state for a registered file storage provider.
/// </summary>
public class FileStorageHealthResponseModel
{
    /// <summary>
    /// Gets or sets the registration name used by the file storage factory.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider is healthy.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Gets or sets the status message returned by the health probe.
    /// </summary>
    public string Message { get; set; }
}
