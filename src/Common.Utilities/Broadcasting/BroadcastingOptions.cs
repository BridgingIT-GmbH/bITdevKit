// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Configures the host-wide Broadcasting runtime.
/// </summary>
/// <example>
/// <code>
/// services.AddBroadcasting(options => options
///     .Enabled(environment.IsDevelopment())
///     .Scopes("MyApp.Development"));
/// </code>
/// </example>
public sealed class BroadcastingOptions
{
    private bool hasImplicitDefaultScope;

    /// <summary>Gets the scope used when registration or publication omits explicit scopes.</summary>
    /// <example><code>var scope = BroadcastingOptions.DefaultScope;</code></example>
    public const string DefaultScope = "default";

    /// <summary>Gets or sets whether the composed runtime is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the delay before the initial node registration begins.</summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.Zero;

    /// <summary>Gets or sets whether initial registration waits for optional database readiness.</summary>
    public bool WaitForDatabaseReady { get; set; }

    /// <summary>Gets or sets the database-readiness name used by initial registration.</summary>
    public string DatabaseReadyName { get; set; }

    /// <summary>Gets or sets the maximum time initial registration waits for database readiness.</summary>
    public TimeSpan DatabaseReadyTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Gets the scopes contributed by all registration calls.</summary>
    public ICollection<string> Scopes { get; } = new List<string>();

    /// <summary>Gets or sets an optional explicit node identity.</summary>
    public string NodeIdentity { get; set; }

    /// <summary>Gets or sets the maximum raw serialized payload size.</summary>
    public long MaximumPayloadBytes { get; set; } = ByteSize.Kilobytes(64);

    /// <summary>Gets or sets the per-node delivery timeout.</summary>
    public TimeSpan DeliveryTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Gets or sets the maximum concurrent remote deliveries.</summary>
    public int MaximumConcurrentDeliveries { get; set; } = 16;

    /// <summary>Gets or sets the default broadcast lifetime.</summary>
    public TimeSpan DefaultLifetime { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the maximum recent broadcast identifiers retained locally.</summary>
    public int DuplicateCapacity { get; set; } = 1024;

    /// <summary>Gets or sets how long accepted identifiers remain in duplicate protection.</summary>
    public TimeSpan DuplicateRetention { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Gets or sets the bounded queue capacity for each handler type.</summary>
    public int HandlerQueueCapacity { get; set; } = 32;

    /// <summary>Gets or sets the failed-delivery count that deactivates a node.</summary>
    public int UnreachableFailureThreshold { get; set; } = 3;

    /// <summary>Gets or sets whether low-frequency registration leasing is enabled.</summary>
    public bool RegistrationLeaseEnabled { get; set; }

    /// <summary>Gets or sets the registration lease-renewal interval.</summary>
    public TimeSpan RegistrationLeaseRenewalInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets the registration lease duration.</summary>
    public TimeSpan RegistrationLeaseDuration { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>Validates that the current options form a usable Broadcasting configuration.</summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when Broadcasting is enabled and one or more option values are invalid.
    /// </exception>
    /// <example><code>options.Validate();</code></example>
    public void Validate()
    {
        if (!this.Enabled)
        {
            return;
        }

        this.EnsureDefaultScope();

        if (
            this.Scopes.Any(scope =>
                string.IsNullOrWhiteSpace(scope) || scope.Trim().Length > 256
            )
        )
        {
            throw new InvalidOperationException(
                "Broadcast scopes must contain 1 to 256 non-whitespace characters."
            );
        }

        if (
            this.NodeIdentity is not null
            && (
                string.IsNullOrWhiteSpace(this.NodeIdentity)
                || this.NodeIdentity.Trim().Length > 256
            )
        )
        {
            throw new InvalidOperationException(
                "An explicit broadcast node identity must contain 1 to 256 non-whitespace characters."
            );
        }

        if (this.StartupDelay < TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "The Broadcasting startup delay cannot be negative."
            );
        }

        if (
            this.WaitForDatabaseReady
            && (
                this.DatabaseReadyTimeout <= TimeSpan.Zero
                || (
                    this.DatabaseReadyName is not null
                    && (
                        string.IsNullOrWhiteSpace(this.DatabaseReadyName)
                        || this.DatabaseReadyName.Trim().Length > 256
                    )
                )
            )
        )
        {
            throw new InvalidOperationException(
                "Database readiness requires a positive timeout and an optional name of at most 256 characters."
            );
        }

        if (
            this.MaximumPayloadBytes <= 0
            || this.MaximumPayloadBytes > int.MaxValue
            || this.DeliveryTimeout <= TimeSpan.Zero
            || this.MaximumConcurrentDeliveries <= 0
            || this.DefaultLifetime <= TimeSpan.Zero
            || this.DuplicateCapacity <= 0
            || this.HandlerQueueCapacity <= 0
            || this.UnreachableFailureThreshold <= 0
        )
        {
            throw new InvalidOperationException("Broadcasting limits must be greater than zero.");
        }

        if (this.DuplicateRetention <= this.DefaultLifetime)
        {
            throw new InvalidOperationException(
                "Duplicate retention must be longer than the broadcast lifetime."
            );
        }

        if (
            this.RegistrationLeaseEnabled
            && (
                this.RegistrationLeaseRenewalInterval <= TimeSpan.Zero
                || this.RegistrationLeaseDuration <= this.RegistrationLeaseRenewalInterval
            )
        )
        {
            throw new InvalidOperationException(
                "The lease duration must be longer than its positive renewal interval."
            );
        }
    }

    internal bool HasImplicitDefaultScope =>
        this.hasImplicitDefaultScope
        && this.Scopes.Count == 1
        && string.Equals(
            this.Scopes.First(),
            DefaultScope,
            StringComparison.OrdinalIgnoreCase);

    internal void EnsureDefaultScope()
    {
        if (this.Scopes.Count == 0)
        {
            this.Scopes.Add(DefaultScope);
            this.hasImplicitDefaultScope = true;
        }
        else if (
            this.Scopes.Count != 1
            || !string.Equals(
                this.Scopes.First(),
                DefaultScope,
                StringComparison.OrdinalIgnoreCase))
        {
            this.hasImplicitDefaultScope = false;
        }
    }

    internal void RemoveImplicitDefaultScope()
    {
        if (this.HasImplicitDefaultScope)
        {
            this.Scopes.Clear();
            this.hasImplicitDefaultScope = false;
        }
    }
}

/// <summary>
/// Fluently updates one shared <see cref="BroadcastingOptions"/> instance.
/// </summary>
/// <example><code>options.Enabled().Scopes("MyApp");</code></example>
public sealed class BroadcastingOptionsBuilder
{
    /// <summary>Creates a builder that updates the supplied options instance.</summary>
    /// <param name="target">The options instance to update.</param>
    /// <example><code>var builder = new BroadcastingOptionsBuilder(options);</code></example>
    public BroadcastingOptionsBuilder(BroadcastingOptions target)
    {
        this.Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    private BroadcastingOptions Target { get; }

    /// <summary>Enables or disables the complete host-wide runtime.</summary>
    public BroadcastingOptionsBuilder Enabled(bool enabled = true)
    {
        this.Target.Enabled = enabled;
        return this;
    }

    /// <summary>Sets the delay before the initial node registration begins.</summary>
    /// <param name="value">A non-negative startup delay.</param>
    /// <returns>This builder.</returns>
    /// <example><code>options.StartupDelay(TimeSpan.FromSeconds(15));</code></example>
    public BroadcastingOptionsBuilder StartupDelay(TimeSpan value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
        this.Target.StartupDelay = value;
        return this;
    }

    /// <summary>Sets the initial node-registration delay in milliseconds.</summary>
    /// <param name="milliseconds">A non-negative delay in milliseconds.</param>
    /// <returns>This builder.</returns>
    /// <example><code>options.StartupDelay(15000);</code></example>
    public BroadcastingOptionsBuilder StartupDelay(int milliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(milliseconds);
        return this.StartupDelay(TimeSpan.FromMilliseconds(milliseconds));
    }

    /// <summary>Parses and sets the delay before the initial node registration begins.</summary>
    /// <param name="value">A valid <see cref="TimeSpan"/> value.</param>
    /// <returns>This builder.</returns>
    /// <example><code>options.StartupDelay("00:00:15");</code></example>
    public BroadcastingOptionsBuilder StartupDelay(string value)
    {
        return this.StartupDelay(TimeSpan.Parse(value));
    }

    /// <summary>
    /// Configures optional database-readiness coordination for initial node registration.
    /// </summary>
    /// <remarks>
    /// The Entity Framework registry enables this automatically for its application
    /// <c>DbContext</c>. If no <see cref="IDatabaseReadyService"/> is registered, startup proceeds
    /// without waiting.
    /// </remarks>
    /// <param name="name">
    /// The readiness name, or <see langword="null"/> to wait for all tracked databases.
    /// </param>
    /// <param name="timeout">The maximum readiness wait.</param>
    /// <param name="enabled">Whether readiness coordination is enabled.</param>
    /// <returns>This builder.</returns>
    /// <example>
    /// <code>options.DatabaseReadiness("AppDbContext", TimeSpan.FromMinutes(2));</code>
    /// </example>
    public BroadcastingOptionsBuilder DatabaseReadiness(
        string name = null,
        TimeSpan? timeout = null,
        bool enabled = true)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "The database-readiness timeout must be greater than zero.");
        }

        var normalizedName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        if (normalizedName?.Length > 256)
        {
            throw new ArgumentException(
                "The database-readiness name cannot exceed 256 characters.",
                nameof(name));
        }

        this.Target.WaitForDatabaseReady = enabled;
        this.Target.DatabaseReadyName = normalizedName;
        this.Target.DatabaseReadyTimeout = timeout ?? this.Target.DatabaseReadyTimeout;
        return this;
    }

    /// <summary>Adds scopes using trimmed, case-insensitive uniqueness.</summary>
    public BroadcastingOptionsBuilder Scopes(params string[] scopes)
    {
        foreach (var scope in scopes ?? [])
        {
            var value = scope?.Trim();
            if (value?.Length > 256)
            {
                throw new ArgumentException(
                    "A broadcast scope cannot exceed 256 characters.",
                    nameof(scopes)
                );
            }

            if (
                !string.IsNullOrEmpty(value)
            )
            {
                this.Target.RemoveImplicitDefaultScope();
                if (!this.Target.Scopes.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    this.Target.Scopes.Add(value);
                }
            }
        }

        return this;
    }

    /// <summary>Sets an explicit node identity.</summary>
    public BroadcastingOptionsBuilder NodeIdentity(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 256)
        {
            throw new ArgumentException(
                "The node identity must contain 1 to 256 non-whitespace characters.",
                nameof(value)
            );
        }

        this.Target.NodeIdentity = value.Trim();
        return this;
    }

    /// <summary>Sets the remote-delivery timeout.</summary>
    public BroadcastingOptionsBuilder DeliveryTimeout(TimeSpan value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
        this.Target.DeliveryTimeout = value;
        return this;
    }

    /// <summary>Sets the maximum concurrent remote deliveries.</summary>
    public BroadcastingOptionsBuilder MaximumConcurrentDeliveries(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        this.Target.MaximumConcurrentDeliveries = value;
        return this;
    }

    /// <summary>Sets the raw serialized payload-size limit.</summary>
    public BroadcastingOptionsBuilder MaximumPayloadSize(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bytes, int.MaxValue);
        this.Target.MaximumPayloadBytes = bytes;
        return this;
    }

    /// <summary>Sets the default broadcast lifetime.</summary>
    public BroadcastingOptionsBuilder DefaultLifetime(TimeSpan value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
        if (value >= this.Target.DuplicateRetention)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "The default lifetime must be shorter than duplicate retention."
            );
        }

        this.Target.DefaultLifetime = value;
        return this;
    }

    /// <summary>Sets the duplicate-protection capacity and retention.</summary>
    public BroadcastingOptionsBuilder DuplicateProtection(int capacity, TimeSpan retention)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        if (retention <= this.Target.DefaultLifetime)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retention),
                "Duplicate retention must be longer than the default lifetime."
            );
        }

        this.Target.DuplicateCapacity = capacity;
        this.Target.DuplicateRetention = retention;
        return this;
    }

    /// <summary>Sets the node-local queue capacity for every handler.</summary>
    public BroadcastingOptionsBuilder HandlerQueueCapacity(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        this.Target.HandlerQueueCapacity = value;
        return this;
    }

    /// <summary>Sets the failed-delivery deactivation threshold.</summary>
    public BroadcastingOptionsBuilder UnreachableFailureThreshold(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        this.Target.UnreachableFailureThreshold = value;
        return this;
    }

    /// <summary>Configures optional registration leasing.</summary>
    public BroadcastingOptionsBuilder RegistrationLease(
        bool enabled = true,
        TimeSpan? renewalInterval = null,
        TimeSpan? duration = null
    )
    {
        var effectiveRenewalInterval =
            renewalInterval ?? this.Target.RegistrationLeaseRenewalInterval;
        var effectiveDuration = duration ?? this.Target.RegistrationLeaseDuration;
        if (
            enabled
            && (
                effectiveRenewalInterval <= TimeSpan.Zero
                || effectiveDuration <= effectiveRenewalInterval
            )
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                "The lease duration must be longer than its positive renewal interval."
            );
        }

        this.Target.RegistrationLeaseEnabled = enabled;
        this.Target.RegistrationLeaseRenewalInterval = effectiveRenewalInterval;
        this.Target.RegistrationLeaseDuration = effectiveDuration;
        return this;
    }
}