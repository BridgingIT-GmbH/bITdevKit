﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public class DomainPolicyError : ResultErrorBase
{
    public DomainPolicyError() { }

    public DomainPolicyError(IEnumerable<string> messages = null)
    {
        this.Messages = messages;

        if (messages is not null)
        {
            this.Message = string.Join(Environment.NewLine, messages);
        }
    }

    public IEnumerable<string> Messages { get; }
}