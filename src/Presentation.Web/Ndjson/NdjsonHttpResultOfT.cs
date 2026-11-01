// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Reflection;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

/// <summary>A single-use Minimal API NDJSON execution with per-response observation.</summary>
/// <typeparam name="T">The application record model for serialization and metadata.</typeparam>
/// <example><code>return NdjsonHttpResult.From(values).WithObserver(observer);</code></example>
public sealed class NdjsonHttpResult<T> : Microsoft.AspNetCore.Http.IResult, IEndpointMetadataProvider
{
    private readonly IAsyncEnumerable<T> records;
    private readonly IAsyncEnumerable<NdjsonEncodedRecord> encodedRecords;
    private readonly NdjsonEndpointOptions options;
    private INdjsonStreamObserver observer;
    private int started;
    private readonly object sync = new();

    internal NdjsonHttpResult(IAsyncEnumerable<T> records, IAsyncEnumerable<NdjsonEncodedRecord> encodedRecords, NdjsonEndpointOptions options)
    {
        if (records is null && encodedRecords is null) { throw new ArgumentNullException(nameof(records)); }

        this.records = records;
        this.encodedRecords = encodedRecords;
        this.options = options?.Capture();
        if (encodedRecords is not null && options?.Mode == NdjsonMode.Envelope) { throw new ArgumentException("Encoded output supports raw mode only.", nameof(options)); }
    }

    /// <summary>Attaches an observer before execution. Shared options never retain the observer.</summary>
    /// <example><code>result.WithObserver(observer);</code></example>
    public NdjsonHttpResult<T> WithObserver(INdjsonStreamObserver observer)
    {
        lock (this.sync)
        {
            if (this.started != 0) { throw new InvalidOperationException("Execution already started."); }

            this.observer = observer;
        }

        return this;
    }

    /// <summary>Executes one admitted stream with captured endpoint settings.</summary>
    /// <example><code>await result.ExecuteAsync(httpContext);</code></example>
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        lock (this.sync)
        {
            if (this.started != 0) { throw new InvalidOperationException("NDJSON results are single-use."); }

            this.started = 1;
        }

        var descriptor = httpContext.GetEndpoint()?.Metadata.GetMetadata<NdjsonEndpointDescriptor>();
        var defaults = httpContext.RequestServices.GetService<IOptions<NdjsonStreamingOptions>>()?.Value ?? new NdjsonStreamingOptions();
        var json = httpContext.RequestServices.GetService<IOptions<HttpJsonOptions>>()?.Value.SerializerOptions;
        var settings = (descriptor?.Settings ?? defaults.Capture(json)).Apply(this.options);
        if ((descriptor is not null && (descriptor.RecordType != typeof(T) || descriptor.Mode != settings.Mode || descriptor.EnableCompression != settings.EnableCompression)) || (this.encodedRecords is not null && settings.Mode != NdjsonMode.Raw))
        {
            throw new InvalidOperationException("NDJSON execution and endpoint metadata must declare the same mode, model, and compression policy.");
        }

        var clock = httpContext.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System;
        if (this.encodedRecords is not null)
        {
            return NdjsonHttpStreamExecutor.ExecuteAsync<NdjsonEncodedRecord, T>(httpContext, this.encodedRecords, settings, clock, this.observer, (record, _, _) =>
            {
                try
                {
                    if (record.JsonBytes.Length > settings.MaxRecordBytes) { throw new NdjsonStreamException("stream_record_too_large"); }

                    return Task.FromResult(record);
                }
                catch
                {
                    record?.Dispose();
                    throw;
                }
            }, record => record?.Dispose());
        }

        return NdjsonHttpStreamExecutor.ExecuteAsync<T, T>(httpContext, this.records, settings, clock, this.observer, (value, writer, token) => settings.Mode == NdjsonMode.Raw
            ? writer.EncodeRecordAsync(value, token)
            : writer.EncodeRecordAsync(new NdjsonEnvelope<T> { Kind = NdjsonEnvelopeKind.Data, Data = value }, token));
    }

    /// <summary>Describes NDJSON as a text sequence rather than a JSON array.</summary>
    /// <example><code>app.MapGet("/stream", () => NdjsonHttpResult.From(values)).ProducesNdjson&lt;Weather&gt;();</code></example>
    public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        builder.Metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, typeof(string), [NdjsonDefaults.CONTENT_TYPE]));
        if (!builder.Metadata.OfType<NdjsonEndpointDescriptor>().Any())
        {
            var defaults = builder.ApplicationServices?.GetService<IOptions<NdjsonStreamingOptions>>()?.Value ?? new NdjsonStreamingOptions();
            var json = builder.ApplicationServices?.GetService<IOptions<HttpJsonOptions>>()?.Value.SerializerOptions;
            builder.Metadata.Add(new NdjsonEndpointDescriptor(typeof(T), defaults.Capture(json), isExplicit: false));
        }
    }
}
