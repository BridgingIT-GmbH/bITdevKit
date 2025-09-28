//// MIT-License
//// Copyright BridgingIT GmbH - All Rights Reserved
//// Use of this source code is governed by an MIT-style license that can be
//// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

//namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core.Controllers;

//using System.Net;
//using Application.Modules.Core;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;

//[Authorize]
//[Route("api/core/enumerations")]
//[ApiController]
//public class EnumerationController() : ControllerBase
//{
//    [HttpGet]
//    [ProducesResponseType((int)HttpStatusCode.OK)]
//    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
//    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
//    public ActionResult<EnumerationModel> Get()
//    {
//        return this.Ok(new EnumerationModel());
//    }
//}