// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using BridgingIT.DevKit.Application.Messaging;

public class UserCreatedMessage : MessageBase
{
    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string Email { get; set; }
}