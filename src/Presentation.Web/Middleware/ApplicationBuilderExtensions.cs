// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.AspNetCore.Builder;

using System;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds middleware for providing a correlation id to each HTTP request.
    /// </summary>
    /// <param name="app">The application builder.</param>
    public static IApplicationBuilder UseRequestCorrelation(this IApplicationBuilder app)
    {
        EnsureArg.IsNotNull(app, nameof(app));

        app.UseMiddleware<CorrelationIdProviderMiddleware>();
        return app;
    }

    /// <summary>
    /// Adds middleware for request logging.
    /// <param name="app">The application builder.</param>
    /// <param name="messageTemplateStarted">The message template to use when logging the request start
    /// <param name="messageTemplateFinished">The message template to use when logging the request finish
    /// <returns></returns>
    public static IApplicationBuilder UseRequestLogging(
        this IApplicationBuilder app,
        string messageTemplateStarted,
        string messageTemplateFinished)
        => app.UseRequestLogging(
            options =>
            {
                options.MessageTemplateStarted = messageTemplateStarted;
                options.MessageTemplateFinished = messageTemplateFinished;
            });

    /// <summary>
    /// Adds middleware for request logging.
    /// <param name="app">The application builder.</param>
    /// <param name="configureOptions">A <see cref="Action{T}" /> to configure the provided <see cref="RequestLoggingOptions" />.</param>
    public static IApplicationBuilder UseRequestLogging(
        this IApplicationBuilder app,
        Action<RequestLoggingOptions> configureOptions = null)
    {
        EnsureArg.IsNotNull(app, nameof(app));

        var options = app.ApplicationServices.GetService<IOptions<RequestLoggingOptions>>()?.Value ?? new RequestLoggingOptions();
        configureOptions?.Invoke(options);

        if (options.MessageTemplateStarted is null)
        {
            throw new ArgumentException($"Request logging {nameof(options.MessageTemplateStarted)} cannot be null.");
        }

        if (options.MessageTemplateFinished is null)
        {
            throw new ArgumentException($"Request logging {nameof(options.MessageTemplateFinished)} cannot be null.");
        }

        if (options.GetLevel is null)
        {
            throw new ArgumentException($"Request logging {nameof(options.GetLevel)} cannot be null.");
        }

        return app.UseMiddleware<RequestLoggingMiddleware>(options);
    }
}