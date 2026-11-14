// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

/// <summary>Streams a protected cache snapshot with backpressure and explicit terminal integrity/destination ownership.</summary>
/// <param name="handle">The acquired source whose ownership transfers to this result.</param>
/// <param name="binding">The collection's immutable transfer settings and representation identity.</param>
/// <param name="resources">The bounded application and collection transfer accounts.</param>
/// <param name="policy">The response cache-control policy.</param>
/// <param name="contentType">The code-declared public representation media type.</param>
/// <remarks>The destination retains its buffer reservation until its actual write or flush ends, including after deadline cancellation.</remarks>
/// <example><code>return new CacheStreamingResult(handle, binding, resources, policy);</code></example>
public sealed class CacheStreamingResult(ICacheReadHandle handle, CacheCollectionBinding binding, CacheRuntimeResources resources,
    CacheHttpCachePolicy policy, string contentType = "application/octet-stream") : Microsoft.AspNetCore.Http.IResult
{
    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext context)
    {
        var observations = context.RequestServices.GetService<CacheDiagnostics>()?.ForCollection(binding.Name);
        await using (handle)
        {
            var bufferSize = binding.Options.Streaming.BufferSize;
            if (!ResourceCharge.TryReserve(bufferSize, out var memory, resources.Transient, resources.ForCollection(binding.Name).Resident))
            {
                context.Response.Headers.CacheControl = "no-store";
                await Result.Failure(new CacheError(CacheErrorCode.Capacity, "The HTTP transfer buffer exceeded its finite budget."))
                    .MapHttpNoContent().ExecuteAsync(context).ConfigureAwait(false);
                return;
            }

            using (memory)
            {
                var started = false;
                var responding = 0;
                var terminal = 0;
                using var budget = new CacheTransferBudget(context.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System,
                    binding.Options.Streaming, Expired);
                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, budget.Token);
                var token = cancellation.Token;
                budget.Start();
                try
                {
                    if (handle.Metadata.LogicalSizeBytes == 0)
                    {
                        var empty = new byte[1];
                        if (await handle.Stream.ReadAsync(empty, token).ConfigureAwait(false) != 0)
                        {
                            throw new InvalidDataException("An empty cache representation returned payload bytes.");
                        }

                        await RequireCompletionAsync(handle, token).ConfigureAwait(false);
                    }

                    var range = handle.Range;
                    context.Response.StatusCode = range.HasValue ? StatusCodes.Status206PartialContent : StatusCodes.Status200OK;
                    context.Response.ContentType = contentType;
                    context.Response.ContentLength = range?.Length ?? handle.Metadata.LogicalSizeBytes;
                    context.Response.Headers.CacheControl = policy == CacheHttpCachePolicy.Revalidate ? "private, no-cache" : "no-store";
                    context.Response.Headers.ETag = CacheEntityTag.Create(handle.Metadata.Version, Representation(binding));
                    context.Response.Headers.LastModified = handle.Metadata.PublishedAt.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                    context.Response.Headers.ContentEncoding = "identity";
                    context.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
                    if (range.HasValue)
                    {
                        context.Response.Headers.ContentRange = $"bytes {range.Value.Offset}-{range.Value.Offset + range.Value.Length - 1}/{handle.Metadata.LogicalSizeBytes}";
                    }

                    // Mark destination ownership before checking cancellation so expiry cannot miss an ignored-cancellation start.
                    Volatile.Write(ref responding, 1);
                    CheckActive();
                    await context.Response.StartAsync(token).ConfigureAwait(false);
                    started = true;
                    if (handle.Metadata.LogicalSizeBytes != 0)
                    {
                        var buffer = new byte[bufferSize];
                        int count;
                        while ((count = await handle.Stream.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
                        {
                            await context.Response.Body.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                            CheckActive();
                            budget.Progress();
                        }

                        await RequireCompletionAsync(handle, token).ConfigureAwait(false);
                    }

                    await context.Response.Body.FlushAsync(token).ConfigureAwait(false);
                    CheckActive();
                    if (Interlocked.CompareExchange(ref terminal, 1, 0) == 0)
                    {
                        observations?.Record(CacheObservationKind.HttpDestinationCompleted);
                    }
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or OperationCanceledException)
                {
                    if (started || context.Response.HasStarted)
                    {
                        Failed();
                        context.RequestServices.GetService<ILogger<CacheStreamingResult>>()?.LogWarning(
                            "[{LogKey}] cache HTTP destination did not complete (Collection={Collection}, Failure={Failure})",
                            "ApplicationCaching", binding.Name, exception.GetType().Name);
                        context.Abort();
                        return;
                    }

                    Failed();
                    context.Response.Headers.CacheControl = "no-store";
                    var code = exception is OperationCanceledException ? CacheErrorCode.CoordinationTimeout : CacheErrorCode.Corruption;
                    await Result.Failure(new CacheError(code, "The cache transfer failed before response delivery."))
                        .MapHttpNoContent().ExecuteAsync(context).ConfigureAwait(false);
                }

                void Failed()
                {
                    if (Interlocked.CompareExchange(ref terminal, 2, 0) == 0)
                    {
                        observations?.Record(CacheObservationKind.HttpDestinationFailed);
                    }
                }

                void CheckActive()
                {
                    if (budget.HasExpired) { throw new OperationCanceledException(token); }

                    token.ThrowIfCancellationRequested();
                }

                void Expired()
                {
                    if (Interlocked.CompareExchange(ref terminal, 2, 0) != 0) { return; }

                    observations?.Record(CacheObservationKind.HttpDestinationFailed);
                    if (Volatile.Read(ref responding) != 0) { context.Abort(); }
                }
            }
        }
    }

    /// <summary>Gets the fixed public representation identity used by streaming validators and mutation conditions.</summary>
    public static string Representation(CacheCollectionBinding binding) => binding.Contract.Alias + "/" + binding.Options.ContractVersion + "/encoded";

    private static async ValueTask RequireCompletionAsync(ICacheReadHandle owned, CancellationToken cancellationToken)
    {
        var completion = await owned.WaitForCompletionAsync(cancellationToken).ConfigureAwait(false);
        if (completion.IsFailure || completion.Value is not (CacheReadCompletion.Validated or CacheReadCompletion.RangeCompleted))
        {
            throw new InvalidDataException("The cache source transfer did not establish its declared terminal integrity.");
        }
    }
}