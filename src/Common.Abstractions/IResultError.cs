// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Represents an error result in an operation or computation.
/// </summary>
public interface IResultError
{
    /// <summary>
    ///     Gets the error message associated with the result error.
    /// </summary>
    string Message { get; }

    public void Throw();
}