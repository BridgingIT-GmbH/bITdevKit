// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

public static class ResultProblemExtensions
{
    public static RouteHandlerBuilder ProducesResultProblem(
        this RouteHandlerBuilder builder,
        int statusCode)
    {
        return builder.Produces<ResultProblemDetails>(statusCode, "application/problem+json");
    }
}