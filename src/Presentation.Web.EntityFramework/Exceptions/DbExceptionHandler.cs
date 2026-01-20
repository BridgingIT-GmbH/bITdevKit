// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Host;

using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
///     Exception handler for <see cref="DbException" />.
///     Returns a 503 Service Unavailable response for general database connectivity
///     and execution errors not caught by more specific handlers.
/// </summary>
/// <remarks>
///     This is a catch-all handler for database exceptions and should be registered
///     after more specific handlers like <see cref="DbUpdateConcurrencyExceptionHandler" />
///     and <see cref="DbUpdateExceptionHandler" />.
/// </remarks>
public class DbExceptionHandler(
    ILogger<DbExceptionHandler> logger,
    GlobalExceptionHandlerOptions options)
    : ExceptionHandlerBase<DbException>(logger, options)
{
    /// <inheritdoc />
    protected override int StatusCode => StatusCodes.Status503ServiceUnavailable;

    /// <inheritdoc />
    protected override string Title => "Database Error";

    /// <inheritdoc />
    protected override string GetDetail(DbException exception)
    {
        if (!this.Options.IncludeExceptionDetails)
        {
            return "A database error occurred. Please try again later.";
        }

        return $"[{exception.GetType().Name}] {exception.Message}";
    }

    /// <inheritdoc />
    protected override void LogException(DbException exception)
    {
        this.Logger?.LogError(
            exception,
            "{ExceptionType} occurred (ErrorCode: {ErrorCode}): {ExceptionMessage}",
            exception.GetType().Name,
            exception.ErrorCode,
            exception.Message);
    }
}