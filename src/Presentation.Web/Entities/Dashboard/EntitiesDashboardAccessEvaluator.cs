// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Decides whether the current user may use edit mode on the Entities dashboard.</summary>
/// <param name="options">The Entities dashboard options.</param>
/// <param name="services">The service provider used to resolve the authorization service.</param>
internal sealed class EntitiesDashboardAccessEvaluator(EntitiesDashboardOptions options, IServiceProvider services)
{
    /// <summary>Returns true when edit is configured and the user satisfies the configured policy and roles.</summary>
    /// <param name="user">The current user.</param>
    /// <returns><c>true</c> when edit mode is allowed.</returns>
    public async Task<bool> CanEditAsync(ClaimsPrincipal user)
    {
        if (!options.IsEditConfigured || user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(options.EditPolicy))
        {
            var authorization = services.GetService<IAuthorizationService>();
            if (authorization == null)
            {
                return false;
            }

            var result = await authorization.AuthorizeAsync(user, options.EditPolicy);
            if (!result.Succeeded)
            {
                return false;
            }
        }

        if (options.EditRoles is { Count: > 0 })
        {
            return options.EditRoles.Any(user.IsInRole);
        }

        return true;
    }
}
