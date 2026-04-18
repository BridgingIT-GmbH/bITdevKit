// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures timeout behavior for <see cref="TimeoutDocumentStoreClientBehavior{T}" />.
/// </summary>
public class TimeoutDocumentStoreClientBehaviorOptions
{
    /// <summary>
    /// Gets or sets the timeout applied to document-store operations.
    /// </summary>
    public TimeSpan Timeout { get; set; } = new(0, 0, 0, 30);
}
