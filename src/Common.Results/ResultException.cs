// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Represents a failed result as an exception.</summary>
/// <param name="message">The exception message.</param>
/// <param name="innerException">The exception that caused this result exception, when available.</param>
public class ResultException(string message, Exception innerException = null)
    : Exception(message, innerException)
{
    /// <summary>Initializes an exception using the result's string representation as its message.</summary>
    /// <param name="result">The result whose state, messages, and errors describe the exception.</param>
    public ResultException(Result result) : this(result.ToString())
    {
    }
}
