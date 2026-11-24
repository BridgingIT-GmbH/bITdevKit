// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Records database ownership and entity mappings while the service collection is being configured.</summary>
/// <remarks>Registration is a single-threaded startup operation. Each database freezes its mappings when an owner is first resolved.</remarks>
public sealed class LiteDbDatabaseRegistry
{
    private readonly Dictionary<string, Registration> registrations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> paths = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    /// <summary>Returns the service collection's shared registration catalog or creates it.</summary>
    /// <param name="services">The service collection being configured.</param>
    /// <returns>The shared catalog instance.</returns>
    public static LiteDbDatabaseRegistry GetOrAdd(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var registry = services.FirstOrDefault(d => d.ServiceType == typeof(LiteDbDatabaseRegistry))?.ImplementationInstance as LiteDbDatabaseRegistry;
        if (registry is null)
        {
            registry = new LiteDbDatabaseRegistry();
            services.AddSingleton(registry);
        }

        return registry;
    }

    /// <summary>Records a named Direct-mode database and rejects competing owners of the same normalized path.</summary>
    /// <param name="name">The unique, case-sensitive registration name.</param>
    /// <param name="options">Connection and mapper configuration to snapshot.</param>
    /// <exception cref="ArgumentException">The name, path, or mode is invalid or already registered.</exception>
    public void Add(string name, LiteDbDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionString);
        var connection = new ConnectionString(options.ConnectionString);
        if (connection.Connection != ConnectionType.Direct)
        {
            throw new ArgumentException("LiteDB providers require Direct mode.", nameof(options));
        }

        var filename = connection.Filename;
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);
        var path = filename is ":memory:" or ":temp:" ? null : Path.GetFullPath(filename);
        if (this.registrations.ContainsKey(name) || path is not null && this.paths.ContainsKey(path))
        {
            throw new ArgumentException("A LiteDB database name or file already has an owner.", nameof(name));
        }

        if (path is not null)
        {
            connection.Filename = path;
            this.paths.Add(path, name);
        }

        this.registrations.Add(name, new Registration(connection, options.ConfigureMapper));
    }

    /// <summary>Finds a previously registered database.</summary>
    /// <param name="name">The exact registration name.</param>
    /// <returns>The database registration.</returns>
    /// <exception cref="InvalidOperationException">The name is not registered.</exception>
    public Registration Get(string name) => this.registrations.TryGetValue(name, out var registration)
        ? registration
        : throw new InvalidOperationException($"LiteDB database '{name}' is not registered.");

    /// <summary>Holds a connection snapshot and the mappings for one named database.</summary>
    /// <param name="connection">The parsed connection settings to snapshot.</param>
    /// <param name="configure">Optional application mappings applied after provider mappings.</param>
    public sealed class Registration(ConnectionString connection, Action<BsonMapper> configure)
    {
        private readonly string connectionString = connection?.ToStringWithPassword() ?? throw new ArgumentNullException(nameof(connection));
        private readonly Dictionary<(Type Type, string Feature), (string Collection, Action<BsonMapper> Configure)> entities = [];
        private bool frozen;

        /// <summary>Gets an independent copy of the connection settings, including its password. Its diagnostic ToString redacts the password.</summary>
        public ConnectionString Connection => new(this.connectionString);

        /// <summary>Registers one entity-to-collection mapping before the database owner is created.</summary>
        /// <param name="type">The stored entity type.</param>
        /// <param name="collection">Its unique, case-insensitive collection name.</param>
        /// <param name="mapping">Provider mappings applied to the private database mapper.</param>
        /// <exception cref="InvalidOperationException">The registration has already frozen its mappings.</exception>
        /// <exception cref="ArgumentException">The type or collection conflicts with another mapping.</exception>
        /// <param name="feature">The feature owning this entity collection.</param>
        public void AddEntity(Type type, string collection, Action<BsonMapper> mapping, string feature = "ActiveEntity")
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentException.ThrowIfNullOrWhiteSpace(collection);
            ArgumentNullException.ThrowIfNull(mapping);
            if (this.frozen)
            {
                throw new InvalidOperationException("LiteDB mappings cannot change after database initialization.");
            }

            var key = (type, feature);
            if (this.entities.Any(e => e.Key != key && string.Equals(e.Value.Collection, collection, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("The LiteDB collection is already assigned to another entity type.", nameof(collection));
            }

            if (this.entities.TryGetValue(key, out var existing) && existing.Collection != collection)
            {
                throw new ArgumentException("An entity type cannot have two LiteDB collection mappings in one database.", nameof(type));
            }

            this.entities[key] = (collection, mapping);
        }

        /// <summary>Finds the entity collection registered by a persistence feature.</summary>
        /// <param name="type">The stored entity type.</param>
        /// <param name="feature">The preferred feature, or null when the type has one collection.</param>
        /// <returns>The configured collection name.</returns>
        public string GetCollection(Type type, string feature = null)
        {
            if (feature is not null && this.entities.TryGetValue((type, feature), out var mapping)) { return mapping.Collection; }

            var matches = this.entities.Where(e => e.Key.Type == type).Select(e => e.Value.Collection).Distinct().ToArray();
            return matches.Length == 1 ? matches[0] : throw new InvalidOperationException("History requires an unambiguous entity collection in its selected database.");
        }

        /// <summary>Freezes mapping registration and creates a private mapper with provider and application mappings.</summary>
        /// <returns>A new mapper that excludes ActiveEntity pending events.</returns>
        /// <remarks>A configuration failure does not cache a partial mapper. A later owner construction can retry.</remarks>
        public BsonMapper CreateMapper()
        {
            this.frozen = true;
            var mapper = new BsonMapper();
            mapper.ResolveMember += (type, member, mapped) =>
            {
                if (member.Name == "DomainEvents" && (LiteDbActiveEntityMapping.IsActiveEntity(type) || typeof(BridgingIT.DevKit.Domain.Model.IAggregateRoot).IsAssignableFrom(type)))
                {
                    mapped.IsIgnore = true;
                }
            };
            foreach (var entity in this.entities.Values)
            {
                entity.Configure(mapper);
            }

            configure?.Invoke(mapper);
            return mapper;
        }
    }
}
