// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.AspNetCore.Http;

using System.Reflection;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RazorSlices;

/// <summary>
/// Provides dashboard helpers for rendering RazorSlice pages by compiled Razor identifier.
/// </summary>
/// <example>
/// <code>
/// group.MapGet("/cities", () =>
///     Results.DashboardRazorSlice("/Modules/Core/Dashboard/Pages/Index.cshtml", typeof(DashboardEndpoints).Assembly));
/// </code>
/// </example>
public static class DashboardRazorSliceResultExtensions
{
    /// <summary>
    /// Renders a compiled RazorSlice by its Razor item identifier from the current application assemblies.
    /// </summary>
    /// <param name="resultExtensions">The result extensions marker.</param>
    /// <param name="identifier">The Razor compiled item identifier, for example <c>/Modules/Core/Dashboard/Pages/Index.cshtml</c>.</param>
    /// <param name="statusCode">The response status code.</param>
    /// <returns>An <see cref="IResult" /> that renders the RazorSlice.</returns>
    public static IResult DashboardRazorSlice(
        this IResultExtensions resultExtensions,
        string identifier,
        int statusCode = StatusCodes.Status200OK)
    {
        ArgumentNullException.ThrowIfNull(resultExtensions);

        return new DashboardRazorSliceHttpResult(
            identifier,
            AppDomain.CurrentDomain.GetAssemblies(),
            statusCode);
    }

    /// <summary>
    /// Renders a compiled RazorSlice by its Razor item identifier from the specified assembly.
    /// </summary>
    /// <param name="resultExtensions">The result extensions marker.</param>
    /// <param name="identifier">The Razor compiled item identifier, for example <c>/Modules/Core/Dashboard/Pages/Index.cshtml</c>.</param>
    /// <param name="assembly">The assembly that contains the compiled Razor item.</param>
    /// <param name="statusCode">The response status code.</param>
    /// <returns>An <see cref="IResult" /> that renders the RazorSlice.</returns>
    public static IResult DashboardRazorSlice(
        this IResultExtensions resultExtensions,
        string identifier,
        Assembly assembly,
        int statusCode = StatusCodes.Status200OK)
    {
        ArgumentNullException.ThrowIfNull(resultExtensions);
        ArgumentNullException.ThrowIfNull(assembly);

        return new DashboardRazorSliceHttpResult(identifier, [assembly], statusCode);
    }

    private sealed class DashboardRazorSliceHttpResult(
        string identifier,
        IEnumerable<Assembly> assemblies,
        int statusCode) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var item = assemblies
                .Where(assembly => assembly is not null)
                .SelectMany(assembly => assembly.GetCustomAttributes<RazorCompiledItemAttribute>())
                .FirstOrDefault(item => string.Equals(item.Identifier, identifier, StringComparison.Ordinal));

            if (item is null || !typeof(RazorSlice).IsAssignableFrom(item.Type))
            {
                httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var slice = (RazorSlice)ActivatorUtilities.CreateInstance(httpContext.RequestServices, item.Type);
            slice.HttpContext = httpContext;
            slice.ServiceProvider = httpContext.RequestServices;

            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "text/html; charset=utf-8";

            await slice.RenderAsync(httpContext.Response.BodyWriter, HtmlEncoder.Default, httpContext.RequestAborted);
        }
    }
}
