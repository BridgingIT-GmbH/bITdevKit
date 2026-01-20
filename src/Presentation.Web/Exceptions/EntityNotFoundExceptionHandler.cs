// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
///     Exception handler for <see cref="EntityNotFoundException" />.
///     Returns a 404 Not Found response when an entity cannot be found.
/// </summary>
public class EntityNotFoundExceptionHandler(
    ILogger<EntityNotFoundExceptionHandler> logger,
    GlobalExceptionHandlerOptions options)
    : ExceptionHandlerBase<EntityNotFoundException>(logger, options)
{
    /// <inheritdoc />
    protected override int StatusCode => StatusCodes.Status404NotFound;

    /// <inheritdoc />
    protected override string Title => "Domain Entity Not Found";
}