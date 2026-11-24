// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;

/// <summary>Dispatches authorized bounded management through the same Application requester boundary as other cache surfaces.</summary>
/// <param name="requester">The host requester, preserving its existing behaviors.</param>
/// <param name="catalog">The immutable trusted cache catalog.</param>
/// <param name="authorization">The current host identity/action authorization factory.</param>
/// <param name="configuration">The validated independent transport exposure policy.</param>
/// <example><code>var result = await dispatcher.ExecuteAsync("ConsoleCommands", "entries", arguments, cancellationToken);</code></example>
public sealed class CacheManagementDispatcher(IRequester requester, CacheCatalog catalog, ICacheManagementAccessContextFactory authorization, CacheManagementConfiguration configuration)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 32, Converters = { new JsonStringEnumConverter() } };

    /// <summary>Executes one metadata or operational request; no command invokes a source loader or returns a bulk payload.</summary>
    /// <param name="transport">The explicitly registered ConsoleCommands or Mcp transport.</param>
    /// <param name="operation">The finite management operation name.</param>
    /// <param name="arguments">The bounded selection and target arguments.</param>
    /// <param name="cancellationToken">The original transport cancellation token.</param>
    /// <returns>Bounded redacted structured data, or an explicit validation/authorization/operation failure.</returns>
    public async ValueTask<Result<object>> ExecuteAsync(string transport, string operation, CacheManagementArguments arguments,
        CancellationToken cancellationToken = default)
    {
        arguments ??= new();
        if (!CacheManagementOperations.IsSupported(operation) || !this.IsEnabled(transport) || arguments.Take is <= 0 or > 1000 ||
            arguments.Continuation?.Length > 100000 || arguments.SelectionJson?.Length > 65536 ||
            (arguments.SelectionJson is not null && Encoding.UTF8.GetByteCount(arguments.SelectionJson) > 65536))
        {
            return Result<object>.Failure(new CacheError(CacheErrorCode.Validation, "The management capability, operation, or argument bounds are invalid."));
        }

        try
        {
            operation = operation.ToLowerInvariant();
            if (arguments.Collection is not null && arguments.Collections is not null) { throw new ArgumentException("Select one explicit target form."); }

            var names = arguments.Collections is not null ? CacheBoundedSnapshot.Copy(arguments.Collections, 32, nameof(arguments.Collections)) :
                arguments.Collection is null ? Array.Empty<string>() : new[] { arguments.Collection };
            var targets = names.Select(CacheIdentityNormalizer.Name).ToArray();
            if (targets.Distinct(StringComparer.OrdinalIgnoreCase).Count() != targets.Length || targets.Any(name => !catalog.Collections.ContainsKey(name) || !this.IsEnabled(transport, name)))
            {
                return Result<object>.Failure(new CacheError(CacheErrorCode.Validation, "Every explicit target must be a declared collection exposed to this management transport."));
            }

            var required = CacheManagementOperations.RequiredAction(operation);
            var access = await authorization.CreateAsync(new(transport, operation, Array.AsReadOnly(targets), required), cancellationToken).ConfigureAwait(false);
            if (access.IsFailure) { return Result<object>.Failure(access); }

            if (access.Value is null) { return Result<object>.Failure(new UnauthorizedError("Trusted management authority is unavailable.")); }

            // Intersect host scope with this transport's collection opt-outs. Arguments never grant scope or actions.
            var allowed = catalog.Collections.Keys.Where(name => this.IsEnabled(transport, name) &&
                (access.Value.Collections is null || access.Value.Collections.Contains(name, StringComparer.OrdinalIgnoreCase))).ToArray();
            var context = access.Value with { Collections = Array.AsReadOnly(allowed), Origin = transport };
            if ((context.Actions & required) != required || !StringComparer.OrdinalIgnoreCase.Equals(context.Namespace, catalog.Options.Namespace) ||
                targets.Any(target => !allowed.Contains(target, StringComparer.OrdinalIgnoreCase)))
            {
                return Result<object>.Failure(new UnauthorizedError("The current host caller is not authorized for this management action."));
            }

            var selected = this.Selection(arguments);
            var collection = targets.Length == 1 ? targets[0] : null;
            if (operation is not ("collections" or "status" or "diagnostics" or "invalidate" or "clear") && collection is null)
            {
                return Result<object>.Failure(new CacheError(CacheErrorCode.Validation, "This management operation requires exactly one declared collection."));
            }

            switch (operation.ToLowerInvariant())
            {
                case "diagnostics": return await BoxAsync(requester.SendAsync(new CacheDiagnosticsQuery(context, collection), cancellationToken: cancellationToken)).ConfigureAwait(false);
                case "collections": return await BoxAsync(requester.SendAsync(new CacheCollectionsQuery(context), cancellationToken: cancellationToken)).ConfigureAwait(false);
                case "inspect":
                    var declarations = await requester.SendAsync(new CacheCollectionsQuery(context), cancellationToken: cancellationToken).ConfigureAwait(false);
                    return declarations.IsFailure ? Result<object>.Failure(declarations) : Result<object>.Success(declarations.Value.Where(c => c.Name == collection).ToArray());
                case "entries":
                    var page = await requester.SendAsync(new CacheEntriesQuery(context, collection, selected.IsEmpty ? null : selected, arguments.Take, arguments.Continuation, arguments.Cutoff), cancellationToken: cancellationToken).ConfigureAwait(false);
                    return page.IsFailure ? Result<object>.Failure(page) : Result<object>.Success(new
                    {
                        Scope = "Store", Entries = page.Value.Entries.Select(Metadata).ToArray(), page.Value.Count, page.Value.CountIsExact,
                        page.Value.Completed, page.Value.Continuation, page.Value.Cutoff, page.Value.ObservedAt
                    });
                case "preview":
                    var preview = await requester.SendAsync(new CacheInvalidationPreviewQuery(context, collection, selected), cancellationToken: cancellationToken).ConfigureAwait(false);
                    return preview.IsFailure ? Result<object>.Failure(preview) : Result<object>.Success(new
                    {
                        Scope = "Store", preview.Value.Count, preview.Value.CountIsExact, Sample = preview.Value.Sample.Select(Metadata).ToArray(), preview.Value.Continuation
                    });
                case "remove": return await BoxAsync(requester.SendAsync(new CacheEntryRemoveCommand(context, collection, arguments.Key), cancellationToken: cancellationToken)).ConfigureAwait(false);
                case "invalidate":
                case "clear":
                    if (targets.Length == 0) { return Result<object>.Failure(new CacheError(CacheErrorCode.Validation, "Invalidation requires explicit declared targets.")); }

                    var submitted = await requester.SendAsync(new CacheInvalidateCommand(context, targets, selected, operation.Equals("clear", StringComparison.OrdinalIgnoreCase), arguments.Background), cancellationToken: cancellationToken).ConfigureAwait(false);
                    return submitted.IsFailure ? Result<object>.Failure(submitted) : Result<object>.Success(new
                    {
                        Scope = "PerProviderCollection", Children = submitted.Value.Select((child, index) => new
                        {
                            Collection = targets[index], Provider = catalog.Collections[targets[index]].Provider.Name,
                            Submitted = child.IsSuccess, Completed = child.IsSuccess && child.Value.Status.State == CacheOperationState.Completed,
                            Handle = child.IsSuccess ? child.Value.Handle : null,
                            Accepted = child.IsSuccess && child.Value.Accepted,
                            Status = child.IsSuccess ? Operation(child.Value.Status) : null,
                            Errors = child.Errors.Select(error => new { Code = error is CacheError cacheError ? cacheError.Code.ToString() : error.GetType().Name, error.Message }).ToArray()
                        }).ToArray()
                    });
                case "status" when collection is null:
                    return await BoxAsync(requester.SendAsync(new CacheDiagnosticsQuery(context), cancellationToken: cancellationToken)).ConfigureAwait(false);
                case "status":
                case "pause":
                case "resume":
                    var background = await requester.SendAsync(new CacheBackgroundCommand(context, collection, operation switch
                    {
                        "pause" => CacheBackgroundAction.Pause, "resume" => CacheBackgroundAction.Resume, _ => CacheBackgroundAction.Status
                    }), cancellationToken: cancellationToken).ConfigureAwait(false);
                    return background.IsFailure ? Result<object>.Failure(background) : Result<object>.Success(new { Scope = "NodeLocal", State = background.Value });
                default:
                    if (!Guid.TryParseExact(arguments.OperationId, "N", out _)) { return Result<object>.Failure(new CacheError(CacheErrorCode.Validation, "A retained opaque operation identifier is required.")); }

                    var handle = new CacheOperationHandle(catalog.Collections[collection].Provider.Name, collection, arguments.OperationId);
                    var status = await requester.SendAsync(new CacheOperationCommand(context, handle, operation.ToLowerInvariant() switch
                    {
                        "operation-resume" => CacheOperationAction.Resume, "operation-cancel" => CacheOperationAction.Cancel, _ => CacheOperationAction.Status
                    }), cancellationToken: cancellationToken).ConfigureAwait(false);
                    return status.IsFailure ? Result<object>.Failure(status) : Result<object>.Success(Operation(status.Value));
            }
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or FormatException or OverflowException)
        {
            return Result<object>.Failure(new CacheError(CacheErrorCode.Validation, "The bounded management selection or scope is invalid."));
        }
    }

    private bool IsEnabled(string transport, string collection = null) => configuration.IsEnabled(transport, collection);

    private CacheSelection Selection(CacheManagementArguments arguments)
    {
        var selectors = new[] { arguments.Key, arguments.Prefix, arguments.Pattern, arguments.SelectionJson }.Count(value => value is not null);
        if (selectors > 1) { throw new ArgumentException("Select one structured, exact, prefix, or pattern selector."); }

        return arguments.SelectionJson is not null ? JsonSerializer.Deserialize<CacheSelection>(arguments.SelectionJson, Json) ?? CacheSelection.None :
            arguments.Key is not null ? CacheSelection.Key(arguments.Key) : arguments.Prefix is not null ? CacheSelection.Prefix(arguments.Prefix) :
            arguments.Pattern is not null ? CacheSelection.Pattern(arguments.Pattern) : CacheSelection.None;
    }

    private static async ValueTask<Result<object>> BoxAsync<T>(Task<Result<T>> operation)
    {
        var result = await operation.ConfigureAwait(false);
        return result.IsSuccess ? Result<object>.Success(result.Value) : Result<object>.Failure(result);
    }

    private static object Metadata(CacheEntryMetadata entry) => new
    {
        entry.DisplayKey, entry.ContractId, entry.IsNotFound, entry.PublishedAt, entry.ExpiresAt, entry.AbsoluteExpiresAt,
        entry.LastAccessAt, entry.HasForegroundAccess, entry.StoredSizeBytes, entry.LogicalSizeBytes, entry.EncodedSizeBytes,
        entry.DecodedSizeBytes, entry.ItemCount, entry.Priority, entry.Dimensions, entry.Compression, entry.ContentType
    };

    private static object Operation(CacheProviderOperation operation) => new
    {
        Scope = "Store", operation.Id, operation.Collection, Kind = operation.Request?.Clear == true ? "Clear" : "Invalidate",
        State = operation.State.ToString(), operation.Cutoff, operation.CancellationRequested, operation.Outcome, FailureCode = operation.FailureCode?.ToString(),
        operation.CreatedAt, operation.UpdatedAt, operation.Origin
    };
}