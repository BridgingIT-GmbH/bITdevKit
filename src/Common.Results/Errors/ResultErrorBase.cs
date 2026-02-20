// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

[DebuggerDisplay("Message={Message}")]
public abstract class ResultErrorBase(string message = null) : IResultError
{
    /// <summary>
    /// Gets the collection of custom properties associated with this instance.
    /// </summary>
    public PropertyBag Properties { get; protected set; } = [];

    /// <summary>
    /// Gets the descriptive message associated with the current object.
    /// </summary>
    public virtual string Message { get; protected init; } = message;

    /// <summary>
    /// Throws a ResultException containing the current error message.
    /// </summary>
    public virtual void Throw()
    {
        throw new ResultException(this.Message);
    }
}