// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
///     Middleware to add the current user's ID to the response headers.
/// </summary>
public class CurrentUserLoggingMiddleware
{
    private const string UserIdKey = "UserId";
    private readonly ILogger logger;
    private readonly RequestDelegate next;

    public CurrentUserLoggingMiddleware(
        ILogger<CurrentUserLoggingMiddleware> logger,
        RequestDelegate next)
    {
        EnsureArg.IsNotNull(logger, nameof(logger));
        EnsureArg.IsNotNull(next, nameof(next));

        this.logger = logger;
        this.next = next;
    }

    public async Task Invoke(HttpContext httpContext)
    {
        EnsureArg.IsNotNull(httpContext, nameof(httpContext));

        var currentUserAccessor = httpContext.RequestServices.GetService<ICurrentUserAccessor>();
        var userId = string.Empty;
        if (currentUserAccessor != null)
        {
            userId = currentUserAccessor.UserId;

            if (!string.IsNullOrEmpty(userId))
            {
                httpContext.Response.Headers.AddOrUpdate(UserIdKey, userId);
            }
        }

        if (!string.IsNullOrEmpty(userId))
        {
            using (this.logger.BeginScope(new Dictionary<string, object>
            {
                [UserIdKey] = userId,
            }))
            {
                await this.next(httpContext); // continue pipeline
            }
        }
        else
        {
            await this.next(httpContext); // continue pipeline
        }
    }
}