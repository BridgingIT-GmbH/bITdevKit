// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Presentation;

using Microsoft.AspNetCore.SignalR;
//using Microsoft.AspNetCore.Authorization;

//[Authorize]
public class SignalRHub : Hub
{
    public async Task OnCheckHealth()
    {
        await this.Clients.All.SendAsync("CheckHealth");
    }
}