// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Notifications;

/// <summary>
/// Configures the fake SMTP client used for development and test scenarios.
/// </summary>
public class FakeSmtpClientOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether message bodies should be logged.
    /// </summary>
    public bool LogMessageBody { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of message body characters written to the logs.
    /// </summary>
    public int LogMessageBodyLength { get; set; } = 512;
}
