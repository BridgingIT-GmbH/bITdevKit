// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Messaging.Models;

/// <summary>
/// Represents the request body used to retry a single broker message handler entry.
/// </summary>
/// <example>
/// <code>
/// { "handlerType": "MyApp.Messages.OrderSubmittedHandler" }
/// </code>
/// </example>
public class RetryBrokerMessageHandlerModel
{
    /// <summary>
    /// Gets or sets the fully qualified handler type identifier to retry.
    /// </summary>
    public string HandlerType { get; set; }
}