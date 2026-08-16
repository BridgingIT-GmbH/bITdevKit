// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Represents one or more violations of domain policy.</summary>
public class DomainPolicyError : ResultErrorBase
{
    /// <summary>Initializes a domain-policy error without violation messages.</summary>
    public DomainPolicyError() { }

    /// <summary>Initializes a domain-policy error and joins its messages with the current environment's line separator.</summary>
    /// <param name="messages">The policy-violation messages, or <see langword="null"/> when no details are available.</param>
    public DomainPolicyError(IEnumerable<string> messages = null)
    {
        this.Messages = messages;

        if (messages is not null)
        {
            this.Message = string.Join(Environment.NewLine, messages);
        }
    }

    /// <summary>Gets the supplied policy-violation messages.</summary>
    public IEnumerable<string> Messages { get; }
}
