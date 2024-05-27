// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Routing;

public interface IEndpoints
{
    bool Enabled { get; set; }

    bool IsRegistered { get; set; }

    void Map(IEndpointRouteBuilder app);
}