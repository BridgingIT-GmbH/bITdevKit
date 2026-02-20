// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a format parsing or validation error.
/// </summary>
public class InvalidFormatError(string message = null, object receivedData = null)
    : ResultErrorBase(message ?? "Invalid format")
{
    public object ReceivedData { get; } = receivedData;

    public InvalidFormatError() : this(null, null)
    {
    }

    public InvalidFormatError(object receivedData) : this(null, receivedData)
    {
    }
}