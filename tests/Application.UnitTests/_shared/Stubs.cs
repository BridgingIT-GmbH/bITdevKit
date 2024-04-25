// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests;

using BridgingIT.DevKit.Application.Messaging;

public class MessageStub : MessageBase
{
    public string FirstName { get; set; }

    public string LastName { get; set; }
}

public class PersonStub
{
    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string Nationality { get; set; }

    public int Age { get; set; }
}