// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Error raised when an import interceptor aborts processing.
/// </summary>
public sealed class ImportInterceptionAbortedError : DataPorterError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImportInterceptionAbortedError"/> class.
    /// </summary>
    /// <param name="message">The abort reason.</param>
    public ImportInterceptionAbortedError(string message)
        : base(message)
    {
    }
}
