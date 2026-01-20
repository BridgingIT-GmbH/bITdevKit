// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
///     Exception handler for <see cref="RuleException" />.
///     Returns a 400 Bad Request response when a domain rule is violated.
/// </summary>
public class DomainRuleExceptionHandler(
    ILogger<DomainRuleExceptionHandler> logger,
    GlobalExceptionHandlerOptions options)
    : ExceptionHandlerBase<RuleException>(logger, options)
{
    /// <inheritdoc />
    protected override int StatusCode => StatusCodes.Status400BadRequest;

    /// <inheritdoc />
    protected override string Title => "Bad Request";

    /// <inheritdoc />
    protected override string GetDetail(RuleException exception)
    {
        // Always include details for rule violations
        return $"[{exception.GetType().Name}] {exception.Message}";
    }
}