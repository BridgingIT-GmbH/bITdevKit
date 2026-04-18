// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

/// <summary>
/// Describes a registered file storage provider exposed through the REST API.
/// </summary>
public class FileStorageProviderInfoModel
{
    /// <summary>
    /// Gets or sets the registration name used by the file storage factory.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the logical storage location name reported by the provider.
    /// </summary>
    public string LocationName { get; set; }

    /// <summary>
    /// Gets or sets the human-readable provider description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider supports change notifications.
    /// </summary>
    public bool SupportsNotifications { get; set; }
}
