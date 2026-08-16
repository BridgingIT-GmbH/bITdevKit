// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error that occurs during notification sending operations.
/// </summary>
public class NotificationError(string message, string channel = null, Exception innerException = null)
    : ResultErrorBase(message ?? "Notification failed")
{
    /// <summary>Gets the notification channel on which delivery failed, when supplied.</summary>
    public string Channel { get; } = channel;

    /// <summary>Gets the exception that caused or describes the notification failure, when available.</summary>
    public Exception InnerException { get; } = innerException;
}
