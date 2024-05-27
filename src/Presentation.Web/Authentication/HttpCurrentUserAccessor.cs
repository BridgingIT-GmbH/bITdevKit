// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Security.Claims;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;

public class HttpCurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public string UserId =>
        httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string UserName =>
        httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);

    public string Email =>
        httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email);

    public string[] Roles =>
        httpContextAccessor.HttpContext.User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
}