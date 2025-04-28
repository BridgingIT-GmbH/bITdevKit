// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Collections.Generic;

/// <summary>
///     Middleware to add the current user's ID to the response headers.
/// </summary>
public class CurrentUserLoggingMiddleware
{
    private const string UserIdKey = "UserId";
    private const string UserRolesKey = "UserRoles";
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

                if (currentUserAccessor.Roles != null)
                {
                    httpContext.Response.Headers.AddOrUpdate(
                        UserRolesKey,
                        string.Join(",", currentUserAccessor.Roles.Select(SanitizeHeaderValue)));
                }
            }
        }

        if (!string.IsNullOrEmpty(userId))
        {
            using (this.logger.BeginScope(new Dictionary<string, object>
            {
                [UserIdKey] = userId,
            }))
            {
                //this.logger.LogInformation("User {UserId} has roles {UserRoles}", userId, currentUserAccessor.Roles);
                await this.next(httpContext); // continue pipeline
            }
        }
        else
        {
            await this.next(httpContext); // continue pipeline
        }
    }

    /// <summary>
    /// Sanitizes a string value to be used in HTTP headers by removing characters that could cause issues.
    /// </summary>
    /// <param name="value">The string value to sanitize.</param>
    /// <returns>A sanitized string that is safe to use in HTTP headers.</returns>
    private static string SanitizeHeaderValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // Remove characters that could cause issues in HTTP headers
        return value.Replace("\"", string.Empty)
                    .Replace("'", string.Empty)
                    .Replace(":", string.Empty)
                    .Replace(";", string.Empty)
                    .Replace(",", string.Empty);
    }
}