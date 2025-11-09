// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the policy configuration for a handler, including retry and timeout settings.
/// </summary>
public class PolicyConfig
{
    /// <summary>
    /// Gets or sets the authorization policy attribute for the handler.
    /// </summary>
    public HandlerAuthorizePolicyAttribute AuthorizePolicy { get; set; }

    /// <summary>
    /// Gets or sets the authorization roles attribute for the handler.
    /// </summary>
    public HandlerAuthorizeRolesAttribute AuthorizeRoles { get; set; }

    /// <summary>
    /// Gets or sets the retry policy attribute for the handler.
    /// </summary>
    public HandlerRetryAttribute Retry { get; set; }

    /// <summary>
    /// Gets or sets the timeout policy attribute for the handler.
    /// </summary>
    public HandlerTimeoutAttribute Timeout { get; set; }

    /// <summary>
    /// Gets or sets the chaos policy attribute for the handler.
    /// </summary>
    public HandlerChaosAttribute Chaos { get; set; }

    /// <summary>
    /// Gets or sets the circuit breaker policy attribute for the handler.
    /// </summary>
    public HandlerCircuitBreakerAttribute CircuitBreaker { get; set; }

    /// <summary>
    /// Gets or sets the cache invalidation policy attribute for the handler.
    /// </summary>
    public HandlerCacheInvalidateAttribute CacheInvalidate { get; set; }

    ///// <summary>
    ///// Gets or sets the transaction policy attribute for the handler.
    ///// </summary>
    //public HandlerDatabaseTransactionAttribute DatabaseTransaction { get; set; }
}
