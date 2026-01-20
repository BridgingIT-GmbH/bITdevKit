// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
///     Exception handler for <see cref="ValidationException" />.
///     Returns a 400 Bad Request response with validation errors.
/// </summary>
public class ValidationExceptionHandler(
    ILogger<ValidationExceptionHandler> logger,
    GlobalExceptionHandlerOptions options)
    : ExceptionHandlerBase<ValidationException>(logger, options)
{
    /// <inheritdoc />
    protected override int StatusCode => StatusCodes.Status400BadRequest;

    /// <inheritdoc />
    protected override string Title => "Bad Request";

    /// <inheritdoc />
    protected override string GetDetail(ValidationException exception)
    {
        return this.Options.IncludeExceptionDetails
            ? $"[{nameof(ValidationException)}] A model validation error has occurred while executing the request"
            : null;
    }

    /// <inheritdoc />
    protected override ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        ValidationException exception)
    {
        var errors = exception.Errors?
            .OrderBy(v => v.PropertyName)
            .GroupBy(
                v => v.PropertyName.Replace(
                    "Entity.",
                    string.Empty,
                    StringComparison.OrdinalIgnoreCase),
                v => v.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray())
            ?? [];

        return new ValidationProblemDetails(errors)
        {
            Title = this.Title,
            Status = this.StatusCode,
            Detail = this.GetDetail(exception),
            Type = this.TypeUri,
            Instance = httpContext.Request.Path
        };
    }
}