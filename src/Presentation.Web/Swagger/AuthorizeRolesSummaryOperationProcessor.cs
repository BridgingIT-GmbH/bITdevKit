// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

public class AuthorizeRolesSummaryOperationProcessor : IOperationProcessor
{
    public bool Process(OperationProcessorContext context)
    {
        var attribute = context.MethodInfo.GetCustomAttribute<AuthorizeAttribute>();
        if (attribute is not null)
        {
            context.OperationDescription.Operation.Summary += $"[Roles: {attribute.Roles}] ";
        }

        return true;
    }
}
