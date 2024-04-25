// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Collections.Generic;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSwag.Annotations;

//[Authorize]
[ApiController]
[Route("api/_system/modules")]
public class SystemModuleController : ControllerBase
{
    private readonly ILogger<SystemModuleController> logger;
    private readonly IEnumerable<IModule> modules;

    public SystemModuleController(
        ILogger<SystemModuleController> logger,
        IEnumerable<IModule> modules = null)
    {
        this.logger = logger;
        this.modules = modules;
    }

    [HttpGet]
    [OpenApiTag("_system/modules")]
    public Task<IEnumerable<IModule>> Get()
    {
        return Task.FromResult(this.modules);
    }
}