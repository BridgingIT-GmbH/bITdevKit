﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

public class ValidationExceptionHandler(
    ILogger<ValidationExceptionHandler> logger,
    GlobalExceptionHandlerOptions options) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ValidationException ex)
        {
            return false;
        }

        options ??= new GlobalExceptionHandlerOptions();

        if (options.EnableLogging)
        {
            logger.LogError(ex, "{ExceptionType} occurred: {ExceptionMessage}", ex.GetType().Name, ex.Message);
        }

        var problemDetails = new ValidationProblemDetails
        {
            Title = "Bad Request",
            Status = StatusCodes.Status400BadRequest,
            Detail = options.IncludeExceptionDetails
                ? $"[{nameof(ValidationException)}] A model validation error has occurred while executing the request"
                : null,
            Type = "https://httpstatuses.com/400",
            Errors = ex.Errors?.OrderBy(v => v.PropertyName)
                    .GroupBy(v => v.PropertyName.Replace("Entity.", string.Empty, StringComparison.OrdinalIgnoreCase),
                        v => v.ErrorMessage)
#pragma warning disable SA1010 // Opening square brackets should be spaced correctly
                    .ToDictionary(g => g.Key, g => g.ToArray()) ??
                []
#pragma warning restore SA1010 // Opening square brackets should be spaced correctly
        };

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}