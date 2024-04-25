// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Messaging;

using BridgingIT.DevKit.Application.Messaging;

public class StubMessage : MessageBase
{
    public StubMessage(string firstname, long ticks)
    {
        this.FirstName = firstname;
        this.Ticks = ticks;
    }

    public string FirstName { get; }

    public long Ticks { get; }
}