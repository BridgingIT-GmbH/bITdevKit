// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Logs typed document operations without logging payloads.</summary>
/// <typeparam name="T">The document type.</typeparam>
/// <example><code>var behavior = new LoggingDocumentStoreClientBehavior&lt;Person&gt;(loggerFactory, inner);</code></example>
public class LoggingDocumentStoreClientBehavior<T>(ILoggerFactory loggerFactory, IDocumentStoreClient<T> inner, IKeyDisplayStrategy keyDisplayStrategy = null)
    : DocumentStoreClientBehaviorBase<T>(inner) where T : class, new()
{
    private readonly ILogger<LoggingDocumentStoreClientBehavior<T>> logger = loggerFactory?.CreateLogger<LoggingDocumentStoreClientBehavior<T>>() ?? NullLogger<LoggingDocumentStoreClientBehavior<T>>.Instance;
    private readonly IKeyDisplayStrategy keyDisplay = keyDisplayStrategy ?? new RawKeyDisplayStrategy();

    /// <inheritdoc />
    public override async Task<Result<DocumentEntry<T>>> GetAsync(DocumentKey key, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("{LogKey} documentclient: get (type={DocumentType}, key={DocumentKey})", Constants.LogKey, typeof(T).Name, this.Display(key));
        return await base.GetAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<Result<DocumentInfo>> UpsertAsync(DocumentKey key, T value, DocumentWriteOptions options = null, CancellationToken cancellationToken = default)
    {
        var result = await base.UpsertAsync(key, value, options, cancellationToken);
        this.logger.LogInformation("{LogKey} documentclient: upsert (type={DocumentType}, key={DocumentKey}, success={Success})", Constants.LogKey, typeof(T).Name, this.Display(key), result.IsSuccess);
        return result;
    }

    /// <inheritdoc />
    public override async Task<Result> DeleteAsync(DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default)
    {
        var result = await base.DeleteAsync(key, options, cancellationToken);
        this.logger.LogInformation("{LogKey} documentclient: delete (type={DocumentType}, key={DocumentKey}, success={Success})", Constants.LogKey, typeof(T).Name, this.Display(key), result.IsSuccess);
        return result;
    }

    private string Display(DocumentKey key) => this.keyDisplay.Display($"{key.PartitionKey}/{key.RowKey}");
}
