// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Claims;

/// <summary>
/// Pipeline behavior that enforces role-based authorization for a specific handler,
/// based on <see cref="HandlerAuthorizeRolesAttribute"/> discovered in the
/// <see cref="PolicyConfig"/> for the handler type.
/// </summary>
/// <typeparam name="TRequest">The request type handled.</typeparam>
/// <typeparam name="TResponse">
/// The response type which must implement <see cref="IResult"/>.
/// </typeparam>
/// <remarks>
/// Behavior semantics:
/// - If no roles are configured on the handler, the behavior is a no-op.
/// - Requires the current principal to be authenticated; otherwise returns a failed
///   result with <see cref="UnauthorizedError"/>.
/// - OR semantics across roles: the user must be in at least one of the configured roles.
/// - On role check failure, returns a failed result with <see cref="ForbiddenError"/>.
/// - This behavior is handler-specific (see <see cref="IsHandlerSpecific"/>).
/// Place this behavior early in the pipeline to avoid doing work for unauthorized requests.
/// </remarks>
public sealed class AuthorizationRolesPipelineBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    ConcurrentDictionary<Type, PolicyConfig> policyCache,
    ICurrentUserAccessor currentUserAccessor)
    : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly ConcurrentDictionary<Type, PolicyConfig> policyCache =
        policyCache ?? throw new ArgumentNullException(nameof(policyCache));
    private readonly ICurrentUserAccessor currentUser =
        currentUserAccessor ?? throw new ArgumentNullException(nameof(currentUserAccessor));

    /// <summary>
    /// Always returns true; the presence of roles is evaluated during
    /// <see cref="Process"/> by inspecting the cached policy configuration.
    /// </summary>
    protected override bool CanProcess(TRequest request, Type handlerType) => true;

    /// <summary>
    /// Executes the role-based authorization check for the given handler.
    /// Returns a failed <typeparamref name="TResponse"/> with
    /// <see cref="UnauthorizedError"/> if not authenticated, or
    /// <see cref="ForbiddenError"/> if the user is not a member of any
    /// required role. If no roles are configured, invokes the next stage.
    /// </summary>
    /// <param name="request">The current request instance.</param>
    /// <param name="handlerType">The concrete handler type being executed.</param>
    /// <param name="next">Continuation delegate for the pipeline.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The next pipeline result on success; otherwise a failed <typeparamref name="TResponse"/>.
    /// </returns>
    protected override async Task<TResponse> Process(
        TRequest request,
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken)
    {
        if (!this.policyCache.TryGetValue(handlerType, out var cfg) ||
            cfg.AuthorizeRoles is null ||
            cfg.AuthorizeRoles.Roles is null ||
            cfg.AuthorizeRoles.Roles.Length == 0)
        {
            return await next();
        }

        var principal = this.currentUser.Principal ?? new ClaimsPrincipal();
        if (principal.Identity?.IsAuthenticated != true)
        {
            return (TResponse)(object)Result.Failure(new UnauthorizedError());
        }

        // OR semantics: any role is sufficient
        var hasAnyRole = cfg.AuthorizeRoles.Roles.Any(principal.IsInRole);
        if (!hasAnyRole)
        {
            return (TResponse)(object)Result.Failure(new ForbiddenError("Required role not present."));
        }

        return await next();
    }

    /// <summary>
    /// Indicates that the behavior is applied per handler instance,
    /// leveraging handler-specific <see cref="PolicyConfig"/> entries.
    /// </summary>
    public override bool IsHandlerSpecific() => true;
}