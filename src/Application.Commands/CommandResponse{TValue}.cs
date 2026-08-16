// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

/// <summary>
/// Represents command response.
/// </summary>
/// <typeparam name="TResult">The result type.</typeparam>
/// <param name="cancelledReason">The cancelled reason used by the operation.</param>
public class CommandResponse<TResult>(string cancelledReason = null)
    : CommandResponse(cancelledReason)
{
    /// <summary>
    /// Gets or sets the result.
    /// </summary>
    public TResult Result { get; set; }
}
