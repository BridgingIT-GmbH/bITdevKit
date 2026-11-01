// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Opens one NDJSON response without buffering or reconnecting.</summary>
/// <example><code>var result = await client.GetNdjsonAsync&lt;Weather&gt;("weather/stream", cancellationToken: ct);</code></example>
public static class NdjsonHttpClientExtensions
{
    /// <summary>Opens a caller-owned authenticated GET or POST request using response headers completion.</summary>
    /// <example><code>var result = await client.OpenNdjsonAsync&lt;Weather&gt;(request, cancellationToken: ct);</code></example>
    public static Task<Result<NdjsonHttpResponse<T>>> OpenNdjsonAsync<T>(this HttpClient client, HttpRequestMessage request, NdjsonClientOptions options = null, CancellationToken cancellationToken = default) =>
        OpenAsync<T>(client, request, false, options, cancellationToken);

    /// <summary>Opens a GET response and owns the request it creates, while leaving the client open.</summary>
    /// <example><code>var result = await client.GetNdjsonAsync&lt;Weather&gt;("weather/stream", cancellationToken: ct);</code></example>
    public static Task<Result<NdjsonHttpResponse<T>>> GetNdjsonAsync<T>(this HttpClient client, string requestUri, NdjsonClientOptions options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.ParseAdd(NdjsonDefaults.CONTENT_TYPE);
        return OpenAsync<T>(client, request, true, options, cancellationToken);
    }

    private static async Task<Result<NdjsonHttpResponse<T>>> OpenAsync<T>(HttpClient client, HttpRequestMessage request, bool ownsRequest, NdjsonClientOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);
        HttpResponseMessage response = null;
        var transferred = false;
        try
        {
            var captured = (options ?? new NdjsonClientOptions()).Capture();
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // Error-body bytes are bounded and discarded, never reflected in diagnostics.
                await using var errorBody = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var scratch = new byte[4096];
                var remaining = scratch.Length;
                while (remaining > 0)
                {
                    var count = await errorBody.ReadAsync(scratch.AsMemory(0, remaining), cancellationToken).ConfigureAwait(false);
                    if (count == 0) { break; }

                    remaining -= count;
                }

                Array.Clear(scratch);
                return Failed("stream_http_error", (int)response.StatusCode);
            }

            var type = response.Content.Headers.ContentType;
            if (!string.Equals(type?.MediaType, NdjsonDefaults.CONTENT_TYPE, StringComparison.OrdinalIgnoreCase) ||
                (type.CharSet is { } charset && !string.Equals(charset.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase)))
            {
                return Failed("stream_invalid_content_type", (int)response.StatusCode);
            }

            var declared = Header(response, "X-Ndjson-Mode");
            var version = Header(response, "X-Ndjson-Envelope-Version");
            if (captured.ExpectedMode == NdjsonMode.Envelope)
            {
                if (declared != "envelope" || version != "1")
                {
                    return Failed("stream_unsupported_envelope", (int)response.StatusCode);
                }
            }
            else if ((declared is not null && declared != "raw") || version is not null)
            {
                return Failed("stream_unsupported_envelope", (int)response.StatusCode);
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var handle = new NdjsonHttpResponse<T>(response, stream, ownsRequest ? request : null, captured);
            transferred = true;
            return Result<NdjsonHttpResponse<T>>.Success(handle);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failed("stream_canceled");
        }
        catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException)
        {
            return Failed(error is OperationCanceledException ? "stream_input_timeout" : "stream_http_error");
        }
        finally
        {
            if (!transferred)
            {
                response?.Dispose();
                if (ownsRequest) { request.Dispose(); }
            }
        }

        static Result<NdjsonHttpResponse<T>> Failed(string code, int? status = null) => Result<NdjsonHttpResponse<T>>.Failure().WithError(new NdjsonClientError(code, status));
    }

    private static string Header(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues(name, out var values)) { return null; }

        var headers = values.ToArray();
        return headers.Length == 1 ? headers[0] : "invalid";
    }
}
