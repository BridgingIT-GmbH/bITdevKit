// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics.Metrics;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Captures bounded observations from the existing DevKit meter during local profiling collection.
/// </summary>
/// <remarks>
/// The listener introduces no application-facing metric API. Measurements with tags are rejected
/// to avoid high-cardinality persistence, and each session accepts a fixed maximum of stable
/// instrument identifiers.
/// </remarks>
/// <example><code>await listener.StartAsync(cancellationToken);</code></example>
public sealed class ProfilingCustomMetricListener : IHostedService, IDisposable
{
    private const int QueueCapacity = 1024;
    private const int MaximumMetricIdentifiersPerSession = 128;
    private const int MaximumMetricIdentifierLength = 128;
    private readonly object metricSync = new();
    private readonly IProfilingStore store;
    private readonly ProfilingActiveSessionContext activeSessionContext;
    private readonly ProfilingSegmentContext segmentContext;
    private readonly ProfilingOptions options;
    private readonly TimeProvider timeProvider;
    private readonly Channel<QueueItem> queue = Channel.CreateBounded<QueueItem>(
        new BoundedChannelOptions(QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        }
    );
    private readonly HashSet<string> metricIdentifiers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> gaugeValues = new(StringComparer.Ordinal);
    private MeterListener listener;
    private CancellationTokenSource stopping;
    private Task processing;
    private Task observing;
    private Guid trackedSessionId;
    private int started;
    private int disposed;

    /// <summary>Creates a listener over the shared DevKit meter.</summary>
    /// <param name="store">The profiling observation store.</param>
    /// <param name="activeSessionContext">The process-local active session and node.</param>
    /// <param name="segmentContext">The ambient measured segment context.</param>
    /// <param name="options">The fixed profiling timing options.</param>
    /// <param name="timeProvider">The UTC clock.</param>
    /// <example><code>var listener = new ProfilingCustomMetricListener(store, active, segments, options, TimeProvider.System);</code></example>
    public ProfilingCustomMetricListener(
        IProfilingStore store,
        ProfilingActiveSessionContext activeSessionContext,
        ProfilingSegmentContext segmentContext,
        ProfilingOptions options,
        TimeProvider timeProvider
    )
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.activeSessionContext =
            activeSessionContext ?? throw new ArgumentNullException(nameof(activeSessionContext));
        this.segmentContext =
            segmentContext ?? throw new ArgumentNullException(nameof(segmentContext));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed) != 0, this);
        if (Interlocked.Exchange(ref this.started, 1) != 0)
        {
            return Task.CompletedTask;
        }

        this.stopping = new CancellationTokenSource();
        this.listener = new MeterListener
        {
            InstrumentPublished = (instrument, candidate) =>
            {
                if (
                    string.Equals(
                        instrument.Meter.Name,
                        Metrics.MeterName,
                        StringComparison.Ordinal
                    )
                    && IsStableIdentifier(instrument.Name)
                    && IsSupported(instrument)
                )
                {
                    candidate.EnableMeasurementEvents(instrument);
                }
            },
        };
        this.listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, _) => this.OnMeasurement(instrument, measurement, tags)
        );
        this.listener.SetMeasurementEventCallback<double>(
            (instrument, measurement, tags, _) => this.OnMeasurement(instrument, measurement, tags)
        );
        this.listener.Start();
        this.processing = this.ProcessAsync();
        this.observing = this.ObserveGaugesAsync(this.stopping.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Collects observable gauges and waits until all previously admitted observations are stored.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes after the queue reaches the flush barrier.</returns>
    /// <example><code>await listener.FlushAsync(cancellationToken);</code></example>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref this.started) == 0 || Volatile.Read(ref this.disposed) != 0)
        {
            return;
        }

        this.listener.RecordObservableInstruments();
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        await this
            .queue.Writer.WriteAsync(new QueueItem(null, completion), cancellationToken)
            .ConfigureAwait(false);
        await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref this.started, 0) == 0)
        {
            return;
        }

        this.listener?.Dispose();
        this.stopping?.Cancel();
        this.queue.Writer.TryComplete();
        if (this.observing is not null)
        {
            try
            {
                await this.observing.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (this.stopping.IsCancellationRequested) { }
        }

        if (this.processing is not null)
        {
            await this.processing.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            return;
        }

        this.listener?.Dispose();
        this.stopping?.Cancel();
        this.stopping?.Dispose();
        this.queue.Writer.TryComplete();
    }

    private void OnMeasurement<T>(
        Instrument instrument,
        T measurement,
        ReadOnlySpan<KeyValuePair<string, object>> tags
    )
        where T : struct, IConvertible
    {
        try
        {
            if (!tags.IsEmpty)
            {
                return;
            }

            var active = this.activeSessionContext.Current;
            if (active is null)
            {
                return;
            }

            var now = this.timeProvider.GetUtcNow();
            if (
                active.Session.State != ProfilingSessionState.Running
                || now < active.Session.StartedUtc
                || now > active.Session.EndsUtc
            )
            {
                return;
            }

            var kind = GetKind(instrument);
            if (
                kind is null
                || !this.TryAcceptIdentifier(active.Session.Identity.Id, instrument.Name)
            )
            {
                return;
            }

            var value = Convert.ToDouble(
                measurement,
                System.Globalization.CultureInfo.InvariantCulture
            );
            if (instrument is UpDownCounter<T>)
            {
                lock (this.metricSync)
                {
                    value = this.gaugeValues.GetValueOrDefault(instrument.Name) + value;
                    this.gaugeValues[instrument.Name] = value;
                }
            }

            var ambient = this.segmentContext.Current;
            Guid? segmentId =
                ambient is not null
                && ambient.SessionId == active.Session.Identity.Id
                && ambient.NodeId == active.Node.Identity.Id
                    ? ambient.SegmentId
                    : null;
            this.queue.Writer.TryWrite(
                new(
                    new ProfilingMetricObservation
                    {
                        Id = Guid.NewGuid(),
                        SessionId = active.Session.Identity.Id,
                        SessionKey = active.Session.Identity.Key,
                        NodeId = active.Node.Identity.Id,
                        NodeKey = active.Node.Identity.Key,
                        SegmentId = segmentId,
                        MetricIdentifier = instrument.Name,
                        Kind = kind.Value,
                        Value = value,
                        Unit = instrument.Unit,
                        TimestampUtc = now,
                    },
                    null
                )
            );
        }
        catch (Exception exception) when (!IsFatal(exception))
        {
            // Metric emission must never fail application work.
        }
    }

    private async Task ProcessAsync()
    {
        await foreach (var item in this.queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            if (item.Completion is not null)
            {
                item.Completion.TrySetResult();
                continue;
            }

            try
            {
                _ = await this
                    .store.AddMetricObservationAsync(item.Observation, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (!IsFatal(exception))
            {
                // Metric persistence is observational and cannot fail application work.
            }
        }
    }

    private async Task ObserveGaugesAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(
                        this.options.SamplingInterval,
                        this.timeProvider,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (this.activeSessionContext.Current is not null)
                {
                    this.listener.RecordObservableInstruments();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private bool TryAcceptIdentifier(Guid sessionId, string identifier)
    {
        lock (this.metricSync)
        {
            if (this.trackedSessionId != sessionId)
            {
                this.trackedSessionId = sessionId;
                this.metricIdentifiers.Clear();
                this.gaugeValues.Clear();
            }

            return this.metricIdentifiers.Contains(identifier)
                || (
                    this.metricIdentifiers.Count < MaximumMetricIdentifiersPerSession
                    && this.metricIdentifiers.Add(identifier)
                );
        }
    }

    private static ProfilingMetricKind? GetKind(Instrument instrument) =>
        instrument switch
        {
            Counter<long> => ProfilingMetricKind.Counter,
            UpDownCounter<long> => ProfilingMetricKind.Gauge,
            ObservableGauge<long> => ProfilingMetricKind.Gauge,
            Histogram<long> when IsDuration(instrument) => ProfilingMetricKind.Duration,
            Histogram<double> when IsDuration(instrument) => ProfilingMetricKind.Duration,
            _ => null,
        };

    private static bool IsSupported(Instrument instrument) => GetKind(instrument) is not null;

    private static bool IsDuration(Instrument instrument) =>
        string.Equals(instrument.Unit, "ms", StringComparison.OrdinalIgnoreCase)
        || string.Equals(instrument.Unit, "s", StringComparison.OrdinalIgnoreCase)
        || instrument.Name.EndsWith("_duration", StringComparison.Ordinal);

    private static bool IsStableIdentifier(string value)
    {
        if (
            string.IsNullOrWhiteSpace(value)
            || value.Length > MaximumMetricIdentifierLength
            || !char.IsLetter(value[0])
        )
        {
            return false;
        }

        foreach (var character in value)
        {
            if (
                !char.IsAsciiLetterOrDigit(character)
                && character is not '_'
                && character is not '-'
                && character is not '.'
            )
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsFatal(Exception exception) =>
        exception is OutOfMemoryException or StackOverflowException or AccessViolationException;

    private sealed record QueueItem(
        ProfilingMetricObservation Observation,
        TaskCompletionSource Completion
    );
}

/// <summary>Hosts the singleton custom metric listener with the application lifecycle.</summary>
/// <param name="listener">The singleton listener.</param>
/// <example><code>services.AddProfiling(options => options.Enabled());</code></example>
public sealed class ProfilingCustomMetricHostedService(ProfilingCustomMetricListener listener)
    : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) =>
        listener.StartAsync(cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) =>
        listener.StopAsync(cancellationToken);
}
