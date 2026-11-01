// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Client.Services;

using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>Consumes one authenticated stream with bounded history and explicit cancellation.</summary>
/// <param name="clients">The existing token-bearing backend client factory.</param>
/// <example><code>await client.StartAsync("live", 1000); await client.StopAsync();</code></example>
public sealed class StreamingDemoClient(IHttpClientFactory clients) : IAsyncDisposable
{
    private readonly SemaphoreSlim operations = new(1, 1);
    private readonly Queue<string> history = new();
    private CancellationTokenSource cancellation;
    private Task running;

    /// <summary>Signals changes for the component to render on its UI dispatcher.</summary>
    /// <example><code>client.Changed += Refresh;</code></example>
    public event Action Changed;

    /// <summary>Connection, completion, failure, or cancellation state.</summary>
    /// <example><code>var state = client.State;</code></example>
    public string State { get; private set; } = "idle";

    /// <summary>The number of data records received in this response.</summary>
    /// <example><code>var count = client.TotalCount;</code></example>
    public long TotalCount { get; private set; }

    /// <summary>The UTC time of the last received heartbeat.</summary>
    /// <example><code>var activity = client.LastHeartbeat;</code></example>
    public DateTimeOffset? LastHeartbeat { get; private set; }

    /// <summary>A stable local failure code without response or exception text.</summary>
    /// <example><code>var reason = client.ReasonCode;</code></example>
    public string ReasonCode { get; private set; }

    /// <summary>The last 200 data records, never the entire live feed.</summary>
    /// <example><code>foreach (var line in client.History) Show(line);</code></example>
    public IReadOnlyList<string> History => this.history.ToArray();

    /// <summary>Cancels and disposes any old response before opening the selected stream.</summary>
    /// <example><code>await client.StartAsync("finite-envelope", 1000);</code></example>
    public async Task StartAsync(string source, int intervalMilliseconds)
    {
        if (source is not ("finite-raw" or "finite-envelope" or "live")) { throw new ArgumentOutOfRangeException(nameof(source)); }

        if (source == "live" && intervalMilliseconds is < 1000 or > 60000) { throw new ArgumentOutOfRangeException(nameof(intervalMilliseconds)); }

        await this.operations.WaitAsync();
        try
        {
            await this.StopCoreAsync();
            this.TotalCount = 0;
            this.LastHeartbeat = null;
            this.ReasonCode = null;
            this.history.Clear();
            this.State = "connecting";
            this.cancellation = new CancellationTokenSource();
            this.Changed?.Invoke();
            this.running = this.ConsumeAsync(source, intervalMilliseconds, this.cancellation.Token);
        }
        finally
        {
            this.operations.Release();
        }
    }

    /// <summary>Waits for cancellation and response disposal before returning.</summary>
    /// <example><code>await client.StopAsync();</code></example>
    public async Task StopAsync()
    {
        await this.operations.WaitAsync();
        try { await this.StopCoreAsync(); }
        finally { this.operations.Release(); }
    }

    /// <summary>Releases the active stream at service-scope shutdown.</summary>
    /// <example><code>await client.DisposeAsync();</code></example>
    public async ValueTask DisposeAsync() => await this.StopAsync();

    private async Task StopCoreAsync()
    {
        if (this.running is not null)
        {
            this.cancellation.Cancel();
            await this.running;
            this.running = null;
            this.cancellation.Dispose();
            this.cancellation = null;
        }
    }

    private async Task ConsumeAsync(string source, int intervalMilliseconds, CancellationToken cancellationToken)
    {
        try
        {
            var uri = source switch
            {
                "finite-raw" => "api/core/todoitems/stream",
                "finite-envelope" => "api/core/todoitems/stream/enveloped",
                _ => $"api/core/streaming/ticks?intervalMilliseconds={intervalMilliseconds}"
            };
            using var http = clients.CreateClient("backend-api");
            var mode = source == "finite-raw" ? NdjsonMode.Raw : NdjsonMode.Envelope;
            var opened = await http.GetNdjsonAsync<JsonElement>(uri, new() { ExpectedMode = mode }, cancellationToken);
            if (opened.IsFailure)
            {
                this.ReasonCode = opened.Errors.OfType<NdjsonClientError>().FirstOrDefault()?.ReasonCode ?? "stream_http_error";
                this.State = cancellationToken.IsCancellationRequested ? "canceled" : "failed";
                return;
            }

            await using var response = opened.Value;
            this.State = "streaming";
            this.Changed?.Invoke();
            if (mode == NdjsonMode.Raw)
            {
                await foreach (var value in response.ReadAsync(cancellationToken)) { this.Add(value); }
            }
            else
            {
                await foreach (var frame in response.ReadEnvelopesAsync(cancellationToken))
                {
                    if (frame.Kind == NdjsonEnvelopeKind.Data) { this.Add(frame.Data); }
                    else if (frame.Kind == NdjsonEnvelopeKind.Heartbeat)
                    {
                        this.LastHeartbeat = DateTimeOffset.UtcNow;
                        this.Changed?.Invoke();
                    }
                }
            }

            this.State = "completed";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            this.State = "canceled";
        }
        catch (NdjsonStreamException error)
        {
            this.ReasonCode = error.ReasonCode;
            this.State = error.ReasonCode == "stream_incomplete" ? "incomplete" : "failed";
        }
        catch (Exception)
        {
            this.ReasonCode = "stream_client_failed";
            this.State = "failed";
        }
        finally
        {
            this.Changed?.Invoke();
        }
    }

    private void Add(JsonElement value)
    {
        this.TotalCount = checked(this.TotalCount + 1);
        if (this.history.Count == 200) { this.history.Dequeue(); }

        this.history.Enqueue(value.GetRawText());
        this.Changed?.Invoke();
    }
}
