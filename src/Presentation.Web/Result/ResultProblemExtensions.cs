// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Represents result problem extensions.
/// </summary>
public static class ResultProblemExtensions
{
    /// <summary>
    /// Executes the produces result problem operation.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="statusCode">The status code used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static RouteHandlerBuilder ProducesResultProblem(
        this RouteHandlerBuilder builder,
        int statusCode)
    {
        return builder.Produces<ResultProblemDetails>(statusCode, "application/problem+json");
    }
}
