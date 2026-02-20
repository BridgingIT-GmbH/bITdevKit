// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

/// <summary>
///     Represents a mapping configuration for an exception type to a problem details response.
/// </summary>
public class ExceptionMapping
{
    /// <summary>
    ///     Gets or sets the exception type to map.
    /// </summary>
    public Type ExceptionType { get; set; }

    /// <summary>
    ///     Gets or sets the HTTP status code for the response.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    ///     Gets or sets the title for the problem details.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    ///     Gets or sets the type URI for the problem details.
    /// </summary>
    public string TypeUri { get; set; }

    /// <summary>
    ///     Gets or sets a custom factory function to create problem details.
    ///     When set, this takes precedence over <see cref="StatusCode" />,
    ///     <see cref="Title" />, and <see cref="TypeUri" />.
    /// </summary>
    public Func<Exception, HttpContext, ProblemDetails> ProblemDetailsFactory { get; set; }
}

/// <summary>
///     Represents a handler registration with priority and conditional execution support.
/// </summary>
public class ExceptionHandlerRegistration
{
    /// <summary>
    ///     Gets or sets the exception handler type.
    ///     Must implement <see cref="IExceptionHandler" />.
    /// </summary>
    public Type HandlerType { get; set; }

    /// <summary>
    ///     Gets or sets the priority of the handler.
    ///     Higher values are executed first. Default is 0.
    /// </summary>
    public int Priority { get; set; }
}