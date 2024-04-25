// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using System;

public class EntityCommandRuleNotSatisfied : Exception
{
    public EntityCommandRuleNotSatisfied()
        : base()
    {
    }

    public EntityCommandRuleNotSatisfied(string message)
        : base(message)
    {
    }

    public EntityCommandRuleNotSatisfied(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}