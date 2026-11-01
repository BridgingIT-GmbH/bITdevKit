// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Ndjson;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

internal sealed class StreamingProxyFixture(LoopbackStreamingFixture proxy, HttpClient upstream) : IAsyncDisposable
{
    internal Uri Address => proxy.Address;

    internal static async Task<StreamingProxyFixture> StartAsync(Uri upstreamAddress)
    {
        var client = new HttpClient { BaseAddress = upstreamAddress };
        var proxy = await LoopbackStreamingFixture.StartAsync(app =>
        {
            app.MapGet("/{**path}", async (HttpContext context) =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, context.Request.Path + context.Request.QueryString);
                if (context.Request.Headers.TryGetValue("Accept-Encoding", out var encodings)) { request.Headers.TryAddWithoutValidation("Accept-Encoding", encodings.ToArray()); }

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
                context.Response.StatusCode = (int)response.StatusCode;
                context.Response.ContentType = response.Content.Headers.ContentType?.ToString();
                foreach (var name in new[] { "X-Ndjson-Mode", "X-Ndjson-Envelope-Version" })
                {
                    if (response.Headers.TryGetValues(name, out var values)) { context.Response.Headers[name] = values.ToArray(); }
                }

                if (response.Content.Headers.ContentEncoding.Count > 0) { context.Response.Headers.ContentEncoding = response.Content.Headers.ContentEncoding.ToArray(); }

                await context.Response.StartAsync(context.RequestAborted);
                await using var body = await response.Content.ReadAsStreamAsync(context.RequestAborted);
                var buffer = new byte[4096];
                int count;
                while ((count = await body.ReadAsync(buffer, context.RequestAborted)) > 0)
                {
                    await context.Response.Body.WriteAsync(buffer.AsMemory(0, count), context.RequestAborted);
                    await context.Response.Body.FlushAsync(context.RequestAborted);
                }
            });
        }, compression: false);
        return new StreamingProxyFixture(proxy, client);
    }

    public async ValueTask DisposeAsync()
    {
        await proxy.DisposeAsync();
        upstream.Dispose();
    }
}
