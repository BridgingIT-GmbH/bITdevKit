// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Claims;

/// <summary>
/// Pipeline behavior that enforces one or more authorization policies
/// for a specific handler, based on <see cref="HandlerAuthorizePolicyAttribute"/>
/// discovered in the <see cref="PolicyConfig"/> for the handler type.
/// </summary>
/// <typeparam name="TRequest">The request type handled.</typeparam>
/// <typeparam name="TResponse">
/// The response type which must implement <see cref="IResult"/>.
/// </typeparam>
/// <remarks>
/// Behavior semantics:
/// - If no policies are configured on the handler, the behavior is a no-op.
/// - Requires the current principal to be authenticated; otherwise returns a failed
///   result with <see cref="UnauthorizedError"/>.
/// - AND semantics across policies: all configured policies must succeed via
///   <see cref="IAuthorizationService.AuthorizeAsync(ClaimsPrincipal, object, string)"/>.
/// - On any policy failure, returns a failed result with <see cref="ForbiddenError"/>.
/// - This behavior is handler-specific (see <see cref="IsHandlerSpecific"/>).
/// Place this behavior early in the pipeline to avoid doing work for unauthorized requests.
/// </remarks>
public sealed class AuthorizationPolicyPipelineBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    ConcurrentDictionary<Type, PolicyConfig> policyCache,
    IAuthorizationService authorizationService,
    ICurrentUserAccessor currentUserAccessor)
    : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly ConcurrentDictionary<Type, PolicyConfig> policyCache = policyCache ?? throw new ArgumentNullException(nameof(policyCache));
    private readonly IAuthorizationService authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    private readonly ICurrentUserAccessor currentUserAccessor = currentUserAccessor;

    /// <summary>
    /// Always returns true; actual applicability is determined during
    /// <see cref="Process"/> by inspecting the cached policy configuration.
    /// </summary>
    protected override bool CanProcess(TRequest request, Type handlerType) => true;

    /// <summary>
    /// Executes the authorization policy checks for the given handler.
    /// Returns a failed <typeparamref name="TResponse"/> with
    /// <see cref="UnauthorizedError"/> if not authenticated, or
    /// <see cref="ForbiddenError"/> if any policy fails.
    /// If no policies are configured, invokes the next behavior/handler.
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
            cfg.AuthorizePolicy is null ||
            cfg.AuthorizePolicy.Policies is null ||
            cfg.AuthorizePolicy.Policies.Length == 0)
        {
            return await next();
        }

        var principal = this.currentUserAccessor?.Principal ?? new ClaimsPrincipal();
        if (principal.Identity?.IsAuthenticated != true)
        {
            return (TResponse)(object)Result.Failure(new UnauthorizedError());
        }

        // all policies must pass
        foreach (var policy in cfg.AuthorizePolicy.Policies)
        {
            var result = await this.authorizationService.AuthorizeAsync(principal, resource: null, policy);
            if (!result.Succeeded)
            {
                return (TResponse)(object)Result.Failure(new ForbiddenError($"Policy '{policy}' not satisfied."));
            }
        }

        return await next();
    }

    /// <summary>
    /// Indicates that the behavior is applied per handler instance,
    /// leveraging handler-specific <see cref="PolicyConfig"/> entries.
    /// </summary>
    public override bool IsHandlerSpecific() => true;
}