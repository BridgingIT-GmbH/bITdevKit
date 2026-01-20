// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
///     Exception handler for <see cref="DomainPolicyException" />.
///     Returns a 400 Bad Request response when a domain policy is violated.
/// </summary>
public class DomainPolicyExceptionHandler(
    ILogger<DomainPolicyExceptionHandler> logger,
    GlobalExceptionHandlerOptions options)
    : ExceptionHandlerBase<DomainPolicyException>(logger, options)
{
    /// <inheritdoc />
    protected override int StatusCode => StatusCodes.Status400BadRequest;

    /// <inheritdoc />
    protected override string Title => "Bad Request";

    /// <inheritdoc />
    protected override string GetDetail(DomainPolicyException exception)
    {
        // Always include details for domain policy violations (uses ToString())
        return $"[{exception.GetType().Name}] {exception}";
    }

    /// <inheritdoc />
    protected override void LogException(DomainPolicyException exception)
    {
        this.Logger?.LogError(
            exception,
            "{ExceptionType} occurred: {ExceptionMessage}",
            exception.GetType().Name,
            exception.ToString());
    }
}