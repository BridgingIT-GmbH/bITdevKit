// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

public abstract class WebModuleBase : ModuleBase, IWebModule
{
    protected WebModuleBase()
    {
    }

    protected WebModuleBase(string name = null, int priority = 99)
        : base(name, priority)
    {
    }

    public virtual IEndpointRouteBuilder Map(IEndpointRouteBuilder app, IConfiguration configuration = null, IWebHostEnvironment environment = null)
    {
        return app;
    }
}