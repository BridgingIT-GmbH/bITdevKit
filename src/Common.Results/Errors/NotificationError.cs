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
    public string Channel { get; } = channel;

    public Exception InnerException { get; } = innerException;
}