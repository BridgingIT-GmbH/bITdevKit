// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using IResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Server-side edit authorization for every write endpoint (and the editor fragment). The check runs before any body is read
/// or any data is touched and does not depend on what the client UI shows.
/// </summary>
internal static class EntitiesDashboardWriteGuard
{
    /// <summary>Checks whether the current user may edit.</summary>
    /// <param name="http">The HTTP context.</param>
    /// <returns>A 403 result when edit is not allowed; otherwise null.</returns>
    public static async Task<IResult> CheckAsync(HttpContext http)
    {
        var evaluator = http.RequestServices.GetService<EntitiesDashboardAccessEvaluator>();
        if (evaluator != null && await evaluator.CanEditAsync(http.User).ConfigureAwait(false))
        {
            return null;
        }

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
}
