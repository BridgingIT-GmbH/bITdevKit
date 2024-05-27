// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Routing;

public abstract class EndpointsBase : IEndpoints
{
    public bool Enabled { get; set; } = true;

    public bool IsRegistered { get; set; }

    public abstract void Map(IEndpointRouteBuilder app);
}