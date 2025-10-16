// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Text.Json;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;

public static class HttpContextExtensions
{
    private static readonly ISerializer Serializer = new SystemTextJsonSerializer();

    /// <summary>
    /// usage:
    ///   app.MapGet("/data", (HttpContext context) =>
    ///   {
    ///       var filterModel = context.FromQueryFilter();
    ///       if (filterModel == null)
    ///       {
    ///           return Results.BadRequest("Invalid or missing filter.");
    ///       }
    ///
    ///       return Results.Ok(filterModel);
    ///   });
    /// </summary>
    public static async Task<FilterModel> FromQueryFilterAsync(this HttpContext context, string queryParameter = "filter")
    {
        if (context == null)
        {
            return new FilterModel();
        }

        // Default maximum query string length: 2,048 characters
        var query = context.Request.Query[queryParameter].FirstOrDefault();

        try
        {
            return string.IsNullOrEmpty(query) ? default : Serializer.Deserialize<FilterModel>(query);
        }
        catch (JsonException ex)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync($"Invalid JSON format for filter model: {ex.Message}");

            return default;
        }
    }

    /// <summary>
    /// usage:
    ///   app.MapPost("/data/body", async (HttpContext context) =>
    ///   {
    ///       var filterModel = await context.FromBodyFilterAsync();
    ///       if (filterModel == null)
    ///       {
    ///           return Results.BadRequest("Invalid or missing filter in the body.");
    ///       }
    ///
    ///       return Results.Ok(filterModel);
    ///   });
    /// </summary>
    public static async Task<FilterModel> FromBodyFilterAsync(this HttpContext context, bool enableBuffering = false)
    {
        if (context == null)
        {
            return new FilterModel();
        }

        try
        {
            if (enableBuffering) // Enable request body buffering to allow multiple reads
            {
                context.Request.EnableBuffering();
            }

            // Read request body as string
            using var reader = new StreamReader(context.Request.Body, leaveOpen: enableBuffering);
            var body = await reader.ReadToEndAsync();

            if (enableBuffering) // Reset the request body stream position so it can be read again
            {
                context.Request.Body.Position = 0;
            }

            if (string.IsNullOrEmpty(body))
            {
                return default;
            }

            return Serializer.Deserialize<FilterModel>(body);
        }
        catch (JsonException ex)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync($"Invalid JSON format for filter model: {ex.Message}");

            return default;
        }
    }
}
