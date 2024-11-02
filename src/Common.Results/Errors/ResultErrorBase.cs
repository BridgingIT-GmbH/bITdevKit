// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public abstract class ResultErrorBase(string message = null) : IResultError
{
    public string Message { get; protected init; } = message;

    public virtual void Throw()
    {
        throw new ResultException(this.Message);
    }
}