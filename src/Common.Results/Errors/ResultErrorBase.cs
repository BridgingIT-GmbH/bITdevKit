// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

[DebuggerDisplay("Message={Message}")]
public abstract class ResultErrorBase(string message = null) : IResultError
{
    public virtual string Message { get; protected init; } = message;

    public virtual void Throw()
    {
        throw new ResultException(this.Message);
    }
}