// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Authorization;
using NSwag;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

/// <summary>
/// Represents authorization operation processor.
/// </summary>
/// <param name="name">The name of the value.</param>
public class AuthorizationOperationProcessor(string name) : IOperationProcessor
{
    private readonly string name = name;

    /// <summary>
    /// Executes the process operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public bool Process(OperationProcessorContext context)
    {
        if (this.name is not null &&
            context.MethodInfo.DeclaringType is not null &&
            (context.MethodInfo.DeclaringType.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any() ||
            context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any()))
        {
            context.OperationDescription.Operation.Responses.Add("401",
                new OpenApiResponse { Description = "Unauthorized" });
            context.OperationDescription.Operation.Responses.Add("403",
                new OpenApiResponse { Description = "Forbidden" });
            context.OperationDescription.Operation.Security =
                [new() { [this.name] = new List<string>() }];
        }

        return true;
    }
}
