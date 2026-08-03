// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Configures named Document Storage providers, clients, behaviors, and payload transforms.</summary>
/// <example><code>services.AddDocumentStorage().WithProvider&lt;Person&gt;(sp => new InMemoryDocumentStoreProvider());</code></example>
public sealed class DocumentStorageBuilderContext(
    IServiceCollection services,
    DocumentStorageOptions options,
    IConfiguration configuration = null)
{
    private readonly List<BehaviorRegistration> behaviors = [];
    private readonly List<TransformRegistration> transforms = [];
    private readonly HashSet<DocumentStoreServiceKey> registrations = [];
    private readonly HashSet<Type> defaults = [];

    /// <summary>Gets the configured service collection.</summary>
    public IServiceCollection Services { get; } = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>Gets top-level Document Storage options.</summary>
    public DocumentStorageOptions Options { get; } = options ?? new();

    /// <summary>Gets optional application configuration.</summary>
    public IConfiguration Configuration { get; } = configuration;

    /// <summary>Gets the default client lifetime.</summary>
    public ServiceLifetime Lifetime => this.Options.Lifetime;

    /// <summary>Registers an activator-created behavior for its implemented document type.</summary>
    /// <typeparam name="TBehavior">The closed client decorator type.</typeparam>
    /// <returns>The current builder.</returns>
    /// <example><code>builder.WithBehavior&lt;LoggingDocumentStoreClientBehavior&lt;Person&gt;&gt;();</code></example>
    public DocumentStorageBuilderContext WithBehavior<TBehavior>() where TBehavior : class
    {
        var documentType = GetDocumentType(typeof(TBehavior));
        this.behaviors.Add(new(documentType, (inner, provider) =>
            ActivatorUtilities.CreateInstance(provider, typeof(TBehavior), inner)));
        return this;
    }

    /// <summary>Registers a behavior factory for one document type.</summary>
    public DocumentStorageBuilderContext WithBehavior<T, TBehavior>(Func<IDocumentStoreClient<T>, TBehavior> behavior)
        where T : class, new() where TBehavior : notnull, IDocumentStoreClient<T>
    {
        ArgumentNullException.ThrowIfNull(behavior);
        this.behaviors.Add(new(typeof(T), (inner, _) => behavior((IDocumentStoreClient<T>)inner)));
        return this;
    }

    /// <summary>Registers a service-provider-aware behavior factory for one document type.</summary>
    public DocumentStorageBuilderContext WithBehavior<T, TBehavior>(Func<IDocumentStoreClient<T>, IServiceProvider, TBehavior> behavior)
        where T : class, new() where TBehavior : notnull, IDocumentStoreClient<T>
    {
        ArgumentNullException.ThrowIfNull(behavior);
        this.behaviors.Add(new(typeof(T), (inner, provider) => behavior((IDocumentStoreClient<T>)inner, provider)));
        return this;
    }

    /// <summary>Registers a payload transform factory for one document type.</summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="factory">The transform factory.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>builder.WithTransform&lt;Person&gt;(sp => new CompressionDocumentPayloadTransform());</code></example>
    public DocumentStorageBuilderContext WithTransform<T>(Func<IServiceProvider, IDocumentPayloadTransform> factory, string identifier = null)
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(factory);
        this.transforms.Add(new(typeof(T), factory, identifier));
        return this;
    }

    /// <summary>Registers a named provider and its validating typed client at one DI lifetime.</summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="providerFactory">The container-owned provider factory.</param>
    /// <param name="providerName">The provider display and continuation-token identity.</param>
    /// <param name="displayName">The document display name.</param>
    /// <param name="capabilities">The provider capabilities.</param>
    /// <param name="documentStoreOptions">The per-client validation and size options.</param>
    /// <param name="name">The normalized named-client identity.</param>
    /// <param name="isDefault">Whether unkeyed injection aliases this client.</param>
    /// <param name="lifetime">The optional lifetime override.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>builder.RegisterProvider&lt;Person&gt;(sp => new InMemoryDocumentStoreProvider(), "In-memory");</code></example>
    public DocumentStorageBuilderContext RegisterProvider<T>(
        Func<IServiceProvider, IDocumentStoreProvider> providerFactory,
        string providerName = null,
        string displayName = null,
        DocumentStoreProviderCapabilities capabilities = null,
        DocumentStoreOptions documentStoreOptions = null,
        string name = "default",
        bool isDefault = true,
        ServiceLifetime? lifetime = null)
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(providerFactory);
        if (!this.Options.IsEnabled) return this;

        var normalizedName = NormalizeName(name);
        var key = new DocumentStoreServiceKey(typeof(T), normalizedName);
        if (!this.registrations.Add(key))
        {
            throw new InvalidOperationException($"Document client '{normalizedName}' is already registered for '{typeof(T).PrettyName()}'.");
        }
        if (isDefault && !this.defaults.Add(typeof(T)))
        {
            throw new InvalidOperationException($"A default document client is already registered for '{typeof(T).PrettyName()}'.");
        }

        var clientOptions = documentStoreOptions ?? new DocumentStoreOptions();
        var validation = clientOptions.Validate();
        if (validation.IsFailure)
        {
            throw new InvalidOperationException(validation.Errors.FirstOrDefault()?.Message ?? "Document store options are invalid.");
        }

        var resolvedLifetime = lifetime ?? this.Lifetime;
        var resolvedProviderName = string.IsNullOrWhiteSpace(providerName) ? "Custom" : providerName.Trim();
        this.AddKeyed(key, resolvedLifetime, providerFactory);
        this.AddKeyed<IDocumentStoreClient<T>>(key, resolvedLifetime, serviceProvider =>
        {
            var provider = serviceProvider.GetRequiredKeyedService<IDocumentStoreProvider>(key);
            var transforms = this.transforms
                .Where(x => x.DocumentType == typeof(T))
                .Select(x => x.Create(serviceProvider))
                .ToArray();
            var core = new DocumentStoreClient<T>(
                provider,
                serviceProvider.GetService<ISerializer>(),
                clientOptions,
                serviceProvider.GetService<TimeProvider>(),
                transforms,
                normalizedName);
            IDocumentStoreClient<T> decorated = core;
            foreach (var behavior in this.behaviors.Where(x => x.DocumentType == typeof(T)).AsEnumerable().Reverse())
            {
                decorated = (IDocumentStoreClient<T>)behavior.Create(decorated, serviceProvider);
            }
            return new ClientBoundary<T>(
                normalizedName,
                resolvedProviderName,
                provider,
                decorated,
                clientOptions,
                serviceProvider.GetService<IContinuationTokenProtector>());
        });

        var descriptor = new DocumentStoreClientDescriptor(
            $"{typeof(T).FullName?.ToLowerInvariant() ?? typeof(T).Name.ToLowerInvariant()}:{normalizedName}",
            typeof(T),
            displayName ?? typeof(T).PrettyName(),
            resolvedProviderName,
            capabilities ?? CreateDefaultCapabilities(),
            normalizedName,
            isDefault,
            resolvedLifetime,
            DocumentTypeIdentity.For<T>(),
            this.transforms.Where(x => x.DocumentType == typeof(T)).Select(x => x.Identifier).Where(x => x is not null).ToArray());
        this.Services.AddSingleton(descriptor);
        this.AddKeyed<IDocumentStoreClientAccessor>(key, resolvedLifetime, serviceProvider =>
            new DocumentStoreClientAccessor<T>(descriptor, serviceProvider.GetRequiredKeyedService<IDocumentStoreClient<T>>(key), serviceProvider.GetService<ISerializer>()));
        if (isDefault)
        {
            this.Services.Add(new ServiceDescriptor(typeof(IDocumentStoreClient<T>), serviceProvider =>
                serviceProvider.GetRequiredKeyedService<IDocumentStoreClient<T>>(key), resolvedLifetime));
        }

        this.Services.TryAddScoped<IDocumentStoreClientFactory, DocumentStoreClientFactory>();
        this.Services.TryAddDocumentStorageHealthCheck(tags: ["ready", "storage", "documents"]);
        return this;
    }

    /// <summary>Normalizes a Document Storage client name.</summary>
    public static string NormalizeName(string name) => string.IsNullOrWhiteSpace(name) ? "default" : name.Trim().ToLowerInvariant();

    private void AddKeyed<TService>(DocumentStoreServiceKey key, ServiceLifetime lifetime, Func<IServiceProvider, TService> factory)
        where TService : class
    {
        this.Services.Add(new ServiceDescriptor(typeof(TService), key, (serviceProvider, _) => factory(serviceProvider), lifetime));
    }

    private static Type GetDocumentType(Type behaviorType)
    {
        var types = behaviorType.GetInterfaces()
            .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IDocumentStoreClient<>))
            .Select(x => x.GetGenericArguments()[0]).Distinct().ToArray();
        return types.Length == 1
            ? types[0]
            : throw new ArgumentException($"Behavior '{behaviorType.PrettyName()}' must implement exactly one closed IDocumentStoreClient<T>.", nameof(behaviorType));
    }

    private static DocumentStoreProviderCapabilities CreateDefaultCapabilities() => new()
    {
        FullMatch = DocumentQuerySupport.SupportedServerSide,
        RowKeyPrefixMatch = DocumentQuerySupport.SupportedServerSide,
        RowKeySuffixMatch = DocumentQuerySupport.Unsupported,
        FullScan = DocumentQuerySupport.SupportedServerSide,
        KeyListing = DocumentQuerySupport.SupportedServerSide,
        SupportsContinuationPaging = true
    };

    private sealed record BehaviorRegistration(Type DocumentType, Func<object, IServiceProvider, object> Create);
    private sealed record TransformRegistration(Type DocumentType, Func<IServiceProvider, IDocumentPayloadTransform> Create, string Identifier = null);

    private sealed class ClientBoundary<T>(
        string clientName,
        string providerName,
        IDocumentStoreProvider provider,
        IDocumentStoreClient<T> inner,
        DocumentStoreOptions options,
        IContinuationTokenProtector protector) : IDocumentStoreClient<T>, IDocumentStoreProviderAccessor, IDocumentStoreClientIdentity, IDocumentStoreClientDecorator<T>
        where T : class, new()
    {
        private readonly DocumentTypeIdentity type = DocumentTypeIdentity.For<T>();
        public string ClientName { get; } = clientName;
        public IDocumentStoreClient<T> InnerClient => inner;
        IDocumentStoreProvider IDocumentStoreProviderAccessor.Provider => provider;

        public Task<Result<DocumentEntry<T>>> GetAsync(DocumentKey key, CancellationToken cancellationToken = default) =>
            IsValid(key) ? inner.GetAsync(key, cancellationToken) : Invalid<DocumentEntry<T>>();

        public async Task<Result<DocumentPage<T>>> FindPageAsync(DocumentQuery query, CancellationToken cancellationToken = default)
        {
            var prepared = this.PreparePage("find", query);
            if (prepared.IsFailure) return prepared.Wrap<DocumentPage<T>>();
            var result = await inner.FindPageAsync(prepared.Value.Query, cancellationToken);
            if (result.IsFailure || string.IsNullOrWhiteSpace(result.Value.ContinuationToken)) return result;
            var token = this.WrapToken("find", prepared.Value.QueryHash, result.Value.ContinuationToken);
            return token.IsFailure ? token.Wrap<DocumentPage<T>>() : Result<DocumentPage<T>>.Success(new() { Items = result.Value.Items, ContinuationToken = token.Value });
        }

        public async Task<Result<DocumentKeyPage>> ListPageAsync(DocumentQuery query, CancellationToken cancellationToken = default)
        {
            var prepared = this.PreparePage("list", query);
            if (prepared.IsFailure) return prepared.Wrap<DocumentKeyPage>();
            var result = await inner.ListPageAsync(prepared.Value.Query, cancellationToken);
            if (result.IsFailure || string.IsNullOrWhiteSpace(result.Value.ContinuationToken)) return result;
            var token = this.WrapToken("list", prepared.Value.QueryHash, result.Value.ContinuationToken);
            return token.IsFailure ? token.Wrap<DocumentKeyPage>() : Result<DocumentKeyPage>.Success(new() { Items = result.Value.Items, ContinuationToken = token.Value });
        }

        public Task<Result<long>> CountAsync(DocumentCountQuery query, CancellationToken cancellationToken = default)
        {
            var validation = DocumentQueryValidator.ValidateCount<T>("count", query, provider.Capabilities, options);
            return validation.IsFailure ? Task.FromResult(validation.Wrap<long>()) : inner.CountAsync(query, cancellationToken);
        }

        public Task<Result<bool>> ExistsAsync(DocumentKey key, CancellationToken cancellationToken = default) =>
            IsValid(key) ? inner.ExistsAsync(key, cancellationToken) : Invalid<bool>();

        public Task<Result<DocumentInfo>> UpsertAsync(DocumentKey key, T value, DocumentWriteOptions writeOptions = null, CancellationToken cancellationToken = default) =>
            IsValid(key) && value is not null ? inner.UpsertAsync(key, value, writeOptions, cancellationToken) : Invalid<DocumentInfo>();

        public Task<Result<DocumentBatchResult<DocumentInfo>>> UpsertManyAsync(IReadOnlyCollection<DocumentWrite<T>> writes, CancellationToken cancellationToken = default) =>
            writes is not null && writes.All(x => x is not null && IsValid(x.Key) && x.Value is not null)
                ? inner.UpsertManyAsync(writes, cancellationToken)
                : Invalid<DocumentBatchResult<DocumentInfo>>();

        public Task<Result<DocumentInfo>> UpdatePropertiesAsync(DocumentPropertiesUpdate update, CancellationToken cancellationToken = default) =>
            update is not null && IsValid(update.Key) ? inner.UpdatePropertiesAsync(update, cancellationToken) : Invalid<DocumentInfo>();

        public Task<Result> DeleteAsync(DocumentKey key, DocumentDeleteOptions deleteOptions = null, CancellationToken cancellationToken = default) =>
            IsValid(key) ? inner.DeleteAsync(key, deleteOptions, cancellationToken) : Task.FromResult(Result.Failure(new DocumentStoreInvalidQueryError("PartitionKey and RowKey must not be null or whitespace.")));

        public Task<Result<DocumentBatchResult<DocumentKey>>> DeleteManyAsync(IReadOnlyCollection<DocumentDelete> deletes, CancellationToken cancellationToken = default) =>
            deletes is not null && deletes.All(x => x is not null && IsValid(x.Key))
                ? inner.DeleteManyAsync(deletes, cancellationToken)
                : Invalid<DocumentBatchResult<DocumentKey>>();

        private Result<PreparedPage> PreparePage(string operation, DocumentQuery query)
        {
            var publicToken = query?.ContinuationToken;
            var queryWithoutToken = Copy(query, null);
            var validation = DocumentQueryValidator.ValidatePage<T>(operation, this.type.Value, queryWithoutToken, provider.Capabilities, options);
            if (validation.IsFailure) return validation.Wrap<PreparedPage>();
            if (string.IsNullOrWhiteSpace(publicToken)) return Result<PreparedPage>.Success(new(queryWithoutToken, validation.Value.QueryHash));

            var parsed = DocumentContinuationTokenSerializer.Deserialize(publicToken, protector);
            if (parsed.IsFailure) return parsed.Wrap<PreparedPage>();
            var token = parsed.Value;
            if (!string.Equals(token.Provider, providerName, StringComparison.Ordinal) ||
                !string.Equals(token.ClientName, this.ClientName, StringComparison.Ordinal) ||
                !string.Equals(token.DocumentType, this.type.Value, StringComparison.Ordinal) ||
                !string.Equals(token.Operation, operation, StringComparison.Ordinal) ||
                !string.Equals(token.QueryHash, validation.Value.QueryHash, StringComparison.Ordinal))
            {
                return Result<PreparedPage>.Failure(new DocumentStoreInvalidContinuationTokenError("Continuation token does not match this client, type, operation, or query."));
            }
            return Result<PreparedPage>.Success(new(Copy(queryWithoutToken, token.NativeToken), validation.Value.QueryHash));
        }

        private Result<string> WrapToken(string operation, string queryHash, string nativeToken)
        {
            var innerToken = DocumentContinuationTokenSerializer.Deserialize(nativeToken);
            if (innerToken.IsFailure) return innerToken.Wrap<string>();
            return DocumentContinuationTokenSerializer.Serialize(new()
            {
                Provider = providerName,
                ClientName = this.ClientName,
                DocumentType = this.type.Value,
                Operation = operation,
                QueryHash = queryHash,
                VisibilityCutoff = innerToken.Value.VisibilityCutoff,
                NativeToken = nativeToken
            }, protector);
        }

        private static bool IsValid(DocumentKey key) => !string.IsNullOrWhiteSpace(key.PartitionKey) && !string.IsNullOrWhiteSpace(key.RowKey);
        private static Task<Result<TResult>> Invalid<TResult>() => Task.FromResult(Result<TResult>.Failure(new DocumentStoreInvalidQueryError("A valid document request is required.")));
        private static DocumentQuery Copy(DocumentQuery query, string token) => new()
        {
            DocumentKey = query?.DocumentKey,
            Filter = query?.Filter ?? DocumentKeyFilter.FullMatch,
            Take = query?.Take,
            AllowFullScan = query?.AllowFullScan ?? false,
            ContinuationToken = token
        };
        private sealed record PreparedPage(DocumentQuery Query, string QueryHash);
    }
}
