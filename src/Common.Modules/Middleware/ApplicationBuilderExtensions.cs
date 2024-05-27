// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.AspNetCore.Builder;

using BridgingIT.DevKit.Common;
using EnsureThat;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds middleware for providing a module context for each HTTP request.
    /// </summary>
    /// <param name="app">The application builder.</param>
    public static IApplicationBuilder UseRequestModuleContext(this IApplicationBuilder app)
    {
        EnsureArg.IsNotNull(app, nameof(app));

        return app.UseMiddleware<RequestModuleMiddleware>();
    }
}