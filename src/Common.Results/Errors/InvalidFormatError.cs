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
    /// <summary>Gets the data that could not be parsed or validated, when supplied.</summary>
    public object ReceivedData { get; } = receivedData;

    /// <summary>Initializes an invalid-format error with the default message and no received data.</summary>
    public InvalidFormatError() : this(null, null)
    {
    }

    /// <summary>Initializes an invalid-format error for received data.</summary>
    /// <param name="receivedData">The data that could not be parsed or validated.</param>
    public InvalidFormatError(object receivedData) : this(null, receivedData)
    {
    }
}
