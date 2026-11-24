// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Executes bounded synchronous durable-record commands inside the named database's owned native transaction.</summary>
/// <param name="scopes">The root scope factory for independent feature operations.</param>
/// <param name="options">Frozen feature namespace and materialization settings.</param>
public sealed class LiteDbFeatureDatabase(IServiceScopeFactory scopes, LiteDbFeatureOptions options)
{
    private readonly AsyncLocal<CommandState> command = new();
    private readonly LiteDbFeatureOptions frozen = options is null ? throw new ArgumentNullException(nameof(options)) : new()
    {
        DatabaseName = options.DatabaseName, CollectionPrefix = options.CollectionPrefix,
        MaxMaterializedRows = options.MaxMaterializedRows, Serializer = options.Serializer
    };
    private readonly JsonSerializerOptions jsonOptions = CreateJsonOptions();

    /// <summary>Gets the frozen feature settings.</summary>
    public LiteDbFeatureOptions Options => new()
    {
        DatabaseName = this.frozen.DatabaseName, CollectionPrefix = this.frozen.CollectionPrefix,
        MaxMaterializedRows = this.frozen.MaxMaterializedRows, Serializer = this.frozen.Serializer
    };

    /// <summary>Gets the service provider of the current command scope for scoped actor accessors.</summary>
    public IServiceProvider Services => this.Current.Services;

    /// <summary>Creates a typed command-bound view over one feature collection.</summary>
    /// <typeparam name="TKey">The stable record key type.</typeparam>
    /// <typeparam name="TValue">The stored record type.</typeparam>
    /// <param name="suffix">The collection suffix declared during registration.</param>
    /// <param name="ignoreCase">Whether string keys use ordinal case-insensitive lookup.</param>
    /// <returns>A view that can only be accessed within ExecuteAsync.</returns>
    public LiteDbKeyedCollection<TKey, TValue> Collection<TKey, TValue>(string suffix, bool ignoreCase = false) => new(this, this.Options.CollectionPrefix + "_" + suffix, ignoreCase);

    /// <summary>Runs a synchronous result-producing command without allowing asynchronous work on the native thread.</summary>
    /// <typeparam name="T">The detached result type.</typeparam>
    /// <param name="action">A command returning an already completed task; external I/O is forbidden.</param>
    /// <param name="cancellationToken">Cancels admission and native completion.</param>
    /// <returns>The command result after atomic commit.</returns>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        if (this.command.Value is not null)
        {
            _ = this.Current;
            var nested = action();
            if (!nested.IsCompleted) { throw new InvalidOperationException("Native feature commands cannot await external work."); }

            return nested.GetAwaiter().GetResult();
        }

        await using var scope = scopes.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredKeyedService<LiteDbDatabaseSession>(this.Options.DatabaseName);
        var result = await session.ExecuteAsync(async () => Result<T>.Success(await session.NativeAsync(database =>
        {
            this.command.Value = new CommandState(database, cancellationToken, scope.ServiceProvider);
            try
            {
                var pending = action();
                if (!pending.IsCompleted) { throw new InvalidOperationException("Native feature commands cannot await external work."); }

                var value = pending.GetAwaiter().GetResult();
                cancellationToken.ThrowIfCancellationRequested();
                this.command.Value.Flush();
                return value;
            }
            finally { this.command.Value.Active = false; this.command.Value = null; }
        }, cancellationToken).ConfigureAwait(false)), true, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) { throw new LiteDbProviderException(result); }

        return result.Value;
    }

    /// <summary>Executes a durable command inside an explicitly supplied scoped entity transaction.</summary>
    /// <typeparam name="T">The detached result type.</typeparam>
    /// <param name="session">The caller's named database session.</param>
    /// <param name="services">Its scoped services for actor resolution.</param>
    /// <param name="action">Synchronous native record operations.</param>
    /// <param name="cancellationToken">Cancels admission and completion.</param>
    /// <returns>The command result without prematurely committing an existing transaction.</returns>
    public async Task<T> ExecuteInSessionAsync<T>(LiteDbDatabaseSession session, IServiceProvider services, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        var result = await session.ExecuteAsync(async () => Result<T>.Success(await session.NativeAsync(database =>
        {
            this.command.Value = new CommandState(database, cancellationToken, services);
            try
            {
                var pending = action();
                if (!pending.IsCompleted) { throw new InvalidOperationException("Native feature commands cannot await external work."); }

                var value = pending.GetAwaiter().GetResult();
                cancellationToken.ThrowIfCancellationRequested();
                this.command.Value.Flush();
                return value;
            }
            finally { this.command.Value.Active = false; this.command.Value = null; }
        }, cancellationToken).ConfigureAwait(false)), true, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) { throw new LiteDbProviderException(result); }

        return result.Value;
    }

    /// <summary>Runs a completed-task command and commits its record mutations atomically.</summary>
    /// <param name="action">Synchronous native record operations.</param>
    /// <param name="cancellationToken">Cancels admission and completion.</param>
    /// <returns>The commit task.</returns>
    public Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default) => this.ExecuteAsync(() =>
    {
        var pending = action();
        if (!pending.IsCompleted) { throw new InvalidOperationException("Native feature commands cannot await external work."); }

        pending.GetAwaiter().GetResult();
        return Task.FromResult(0);
    }, cancellationToken);

    /// <summary>Inspects native state without starting a write transaction, tracking models, or creating indexes.</summary>
    /// <typeparam name="T">The fully materialized inspection result.</typeparam>
    /// <param name="inspect">Synchronous inspection that does not mutate native state or return a cursor.</param>
    /// <param name="cancellationToken">Cancels admission and inspection.</param>
    /// <returns>The detached inspection result.</returns>
    public async Task<T> InspectAsync<T>(Func<LiteDatabase, T> inspect, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inspect);
        await using var scope = scopes.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredKeyedService<LiteDbDatabaseSession>(this.frozen.DatabaseName);
        var result = await session.ExecuteAsync(async () => Result<T>.Success(await session.NativeAsync(inspect, cancellationToken).ConfigureAwait(false)), false, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) { throw new LiteDbProviderException(result); }

        return result.Value;
    }

    /// <summary>Reads one detached value or returns a command-local tracked value already loaded by this operation.</summary>
    /// <typeparam name="T">The stored model.</typeparam>
    /// <param name="collection">The declared collection.</param>
    /// <param name="id">The normalized identity.</param>
    /// <param name="found">Whether the native record exists.</param>
    /// <returns>The detached model or its default value.</returns>
    public T Read<T>(string collection, string id, out bool found)
    {
        var state = this.Current;
        var row = state.Database.GetCollection<BsonDocument>(collection).FindById(id);
        found = row is not null;
        if (!found) { return default; }

        var token = collection + ":" + id;
        if (state.Tracked.TryGetValue(token, out var tracked)) { return (T)tracked.Value; }

        var payload = row["Payload"].AsString;
        var value = JsonSerializer.Deserialize<T>(payload, this.jsonOptions);
        state.Tracked[token] = new(value, () =>
        {
            var serialized = this.SerializeValue(value);
            if (serialized != payload) { row["Payload"] = serialized; (value as ILiteDbIndexedRecord)?.WriteIndexes(row); state.Database.GetCollection<BsonDocument>(collection).Update(row); }
        });
        this.CheckBound(state.Tracked.Count);
        return value;
    }

    /// <summary>Writes a logical record and tracks in-command mutations to its detached value.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="T">The stored value type.</typeparam>
    /// <param name="collection">The declared collection.</param>
    /// <param name="id">The normalized identity.</param>
    /// <param name="key">The original key retained for enumeration.</param>
    /// <param name="value">The model to persist.</param>
    public void Write<TKey, T>(string collection, string id, TKey key, T value)
    {
        var state = this.Current;
        var row = new BsonDocument { ["_id"] = id, ["Key"] = JsonSerializer.Serialize(key, this.jsonOptions), ["Payload"] = this.SerializeValue(value) };
        (value as ILiteDbIndexedRecord)?.WriteIndexes(row);
        state.Database.GetCollection<BsonDocument>(collection).Upsert(row);
        state.Tracked[collection + ":" + id] = new(value, () =>
        {
            row["Payload"] = this.SerializeValue(value);
            (value as ILiteDbIndexedRecord)?.WriteIndexes(row);
            state.Database.GetCollection<BsonDocument>(collection).Update(row);
        });
        this.CheckBound(state.Tracked.Count);
    }

    /// <summary>Deletes one logical record without allowing later tracked flushes to resurrect it.</summary>
    /// <param name="collection">The declared collection.</param>
    /// <param name="id">The normalized identity.</param>
    /// <returns>Whether a record was removed.</returns>
    public bool Remove(string collection, string id)
    {
        var state = this.Current;
        state.Tracked.Remove(collection + ":" + id);
        return state.Database.GetCollection<BsonDocument>(collection).Delete(id);
    }

    /// <summary>Materializes a bounded set of native keys for command-local enumeration.</summary>
    /// <typeparam name="TKey">The original key type.</typeparam>
    /// <param name="collection">The declared collection.</param>
    /// <returns>The fully materialized native identities and original keys.</returns>
    public IReadOnlyList<(string Id, TKey Key)> Keys<TKey>(string collection)
    {
        var state = this.Current;
        var rows = state.Database.GetCollection<BsonDocument>(collection).Query().Limit(this.Options.MaxMaterializedRows + 1).ToList();
        this.CheckBound(rows.Count);
        return rows.Select(row => (row["_id"].AsString, JsonSerializer.Deserialize<TKey>(row["Key"].AsString, this.jsonOptions))).ToArray();
    }

    /// <summary>Selects indexed records natively before bounded payload materialization.</summary>
    /// <typeparam name="T">The stored record type.</typeparam>
    /// <param name="suffix">The declared collection suffix.</param>
    /// <param name="predicate">The native scalar predicate.</param>
    /// <param name="take">The maximum selected records.</param>
    /// <param name="order">The native order expression.</param>
    /// <param name="descending">Whether native ordering is descending.</param>
    /// <param name="failOnOverflow">Whether one extra matching row must trigger an explicit selection-limit failure.</param>
    /// <returns>Command-local tracked models, bounded after native filtering.</returns>
    public IReadOnlyList<T> Query<T>(string suffix, BsonExpression predicate, int take, string order = "$.CreatedTicks", bool descending = false, bool failOnOverflow = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);
        this.CheckBound(take);
        var name = this.frozen.CollectionPrefix + "_" + suffix;
        var collection = this.Current.Database.GetCollection<BsonDocument>(name);
        collection.EnsureIndex("ready", "$.Ready");
        collection.EnsureIndex("created", "$.CreatedTicks");
        var query = collection.Query();
        if (predicate is not null) { query = query.Where(predicate); }

        var rows = query.OrderBy(BsonExpression.Create(order), descending ? -1 : 1).Limit(failOnOverflow ? checked(take + 1) : take).ToList();
        if (failOnOverflow && rows.Count > take) { throw new LiteDbProviderException(new InvalidOperationError("The native selection exceeds its materialization limit.")); }

        return rows.Select(row => this.Read<T>(name, row["_id"].AsString, out _)).ToArray();
    }

    private string SerializeValue<T>(T value)
    {
        if (value is System.Collections.ICollection collection && value is not byte[]) { this.CheckBound(collection.Count); }

        return JsonSerializer.Serialize(value, this.jsonOptions);
    }

    private CommandState Current
    {
        get
        {
            var state = this.command.Value ?? throw new InvalidOperationException("Durable record access requires a native feature command.");
            if (!state.Active || state.ThreadId != Environment.CurrentManagedThreadId)
            {
                throw new InvalidOperationException("Durable record access must remain on the owning native command thread.");
            }

            state.CancellationToken.ThrowIfCancellationRequested();
            return state;
        }
    }

    private void CheckBound(int count)
    {
        if (count > this.Options.MaxMaterializedRows) { throw new LiteDbProviderException(new InvalidOperationError("The durable feature command exceeds MaxMaterializedRows. Narrow its selection or raise the configured bound.")); }
    }

    private sealed class CommandState(LiteDatabase database, CancellationToken cancellationToken, IServiceProvider services)
    {
        public int ThreadId { get; } = Environment.CurrentManagedThreadId;
        public bool Active { get; set; } = true;
        public LiteDatabase Database { get; } = database;
        public IServiceProvider Services { get; } = services;
        public CancellationToken CancellationToken { get; } = cancellationToken;
        public Dictionary<string, TrackedValue> Tracked { get; } = new(StringComparer.Ordinal);
        public void Flush()
        {
            foreach (var value in this.Tracked.Values) { this.CancellationToken.ThrowIfCancellationRequested(); value.Flush(); }
        }
    }

    private sealed record TrackedValue(object Value, Action Flush);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = DefaultJsonSerializerOptions.Create(new StoredTypeConverter(), new StoredObjectConverter());
        options.Converters.Insert(0, new StoredPropertyBagConverter());
        return options;
    }

    private sealed class StoredPropertyBagConverter : JsonConverter<PropertyBag>
    {
        public override PropertyBag Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options);
            return values is null ? null : new PropertyBag(values);
        }

        public override void Write(Utf8JsonWriter writer, PropertyBag value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var entry in value)
            {
                writer.WritePropertyName(entry.Key);
                JsonSerializer.Serialize(writer, entry.Value, options);
            }

            writer.WriteEndObject();
        }
    }

    private sealed class StoredTypeConverter : JsonConverter<Type>
    {
        public override Type Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => Type.GetType(reader.GetString()!, true)!;
        public override void Write(Utf8JsonWriter writer, Type value, JsonSerializerOptions options) => writer.WriteStringValue(value.AssemblyQualifiedNameShort());
    }

    private sealed class StoredObjectConverter : JsonConverter<object>
    {
        public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("$type", out var type)) { return root.Clone(); }

            var actual = Type.GetType(type.GetString()!, true)!;
            return actual == typeof(object) ? new object() : JsonSerializer.Deserialize(root.GetProperty("$value").GetRawText(), actual, options);
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("$type", value.GetType().AssemblyQualifiedNameShort());
            writer.WritePropertyName("$value");
            if (value.GetType() == typeof(object)) { writer.WriteStartObject(); writer.WriteEndObject(); }
            else { JsonSerializer.Serialize(writer, value, value.GetType(), options); }

            writer.WriteEndObject();
        }
    }
}
