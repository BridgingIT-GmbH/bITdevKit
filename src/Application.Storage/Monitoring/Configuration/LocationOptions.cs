// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Configuration options for a monitored location (a source of file events).
/// Provides filters, processor registrations and rate limiting for that location.
/// </summary>
/// <param name="name">The logical name of the location. This value is exposed via <see cref="LocationName"/>.</param>
public class LocationOptions(string name)
{
    /// <summary>
    /// The logical name of the configured location.
    /// </summary>
    public string LocationName { get; } = name;

    /// <summary>
    /// Sets a glob pattern to filter files by path. Defaults to all files with the filter '*.*'.
    /// </summary>
    public string FileFilter { get; set; } = "*.*";

    /// <summary>
    /// A set of glob patterns that exclude matching files from processing.
    /// Patterns are evaluated against the file path. Defaults to an empty list.
    /// </summary>
    public string[] FileBlackListFilter { get; set; } = [];

    /// <summary>
    /// Gets or sets the use on demand only.
    /// </summary>
    public bool UseOnDemandOnly { get; set; }

    /// <summary>
    /// Gets or sets the scan on start.
    /// </summary>
    public bool ScanOnStart { get; set; }

    /// <summary>
    /// Rate limiting settings applied to processing for this location.
    /// </summary>
    public RateLimitOptions RateLimit { get; set; } = RateLimitOptions.MediumSpeed;

    /// <summary>
    /// A list of processor behavior types that should be applied at the location level.
    /// These types implement <c>IProcessorBehavior</c> and are applied to all processors for this location.
    /// </summary>
    public List<Type> LocationProcessorBehaviors { get; } = [];

    /// <summary>
    /// Configured processor instances for this location.
    /// Each entry describes a processor type, its behaviors and optional configuration delegate.
    /// </summary>
    public List<ProcessorConfiguration> ProcessorConfigs { get; } = [];

    /// <summary>
    /// Registers a processor behavior type to be applied to all processors in this location.
    /// </summary>
    /// <typeparam name="TBehavior">A type implementing <c>IProcessorBehavior</c>.</typeparam>
    /// <returns>The current <see cref="LocationOptions"/> for fluent configuration.</returns>
    public LocationOptions WithProcessorBehavior<TBehavior>() where TBehavior : IProcessorBehavior
    {
        this.LocationProcessorBehaviors.Add(typeof(TBehavior));
        return this;
    }

    /// <summary>
    /// Registers a processor type for this location and returns its configuration object.
    /// </summary>
    /// <typeparam name="TProcessor">The processor type implementing <c>IFileEventProcessor</c>.</typeparam>
    /// <param name="configure">An optional delegate to further configure the <see cref="ProcessorConfiguration"/>.</param>
    /// <returns>The created <see cref="ProcessorConfiguration"/> for the registered processor.</returns>
    public ProcessorConfiguration UseProcessor<TProcessor>(Action<ProcessorConfiguration> configure = null)
        where TProcessor : IFileEventProcessor
    {
        var config = new ProcessorConfiguration { ProcessorType = typeof(TProcessor) };
        configure?.Invoke(config);
        this.ProcessorConfigs.Add(config);

        return config;
    }
}

/// <summary>
/// Configures a processor with a specific type and behaviors. Allows setting up a delegate for custom configuration.
/// </summary>
/// <summary>
/// Describes a processor registration including the processor type, attached behaviors and an optional
/// configuration callback that will be invoked with the processor instance.
/// </summary>
public class ProcessorConfiguration
{
    /// <summary>
    /// The concrete processor type to instantiate. Must implement <c>IFileEventProcessor</c>.
    /// </summary>
    public Type ProcessorType { get; set; }

    /// <summary>
    /// Behavior types to attach to the processor. Each type should implement <c>IProcessorBehavior</c>.
    /// </summary>
    public List<Type> BehaviorTypes { get; } = [];

    /// <summary>
    /// An optional delegate that will be called with the constructed processor instance to perform
    /// custom configuration. The delegate receives the instance as <see cref="object"/>.
    /// </summary>
    public Action<object> Configure { get; set; } // Delegate to configure the processor instance

    /// <summary>
    /// Adds a behavior type to the processor configuration.
    /// </summary>
    /// <typeparam name="TBehavior">A type implementing <c>IProcessorBehavior</c>.</typeparam>
    /// <returns>The current <see cref="ProcessorConfiguration"/> for fluent configuration.</returns>
    public ProcessorConfiguration WithBehavior<TBehavior>() where TBehavior : IProcessorBehavior
    {
        this.BehaviorTypes.Add(typeof(TBehavior));
        return this;
    }

    /// <summary>
    /// Sets a custom configuration action that will be invoked with the processor instance.
    /// </summary>
    /// <param name="configure">A delegate that mutates or configures the processor instance.</param>
    /// <returns>The current <see cref="ProcessorConfiguration"/> for fluent configuration.</returns>
    public ProcessorConfiguration WithConfiguration(Action<object> configure)
    {
        this.Configure = configure;
        return this;
    }
}

/// <summary>
/// Configures rate limiting options for event processing with specified limits on events per second and burst size.
/// </summary>
/// <param name="eventsPerSecond">Defines the maximum number of events that can be processed each second.</param>
/// <param name="maxBurstSize">Specifies the maximum number of events that can be processed in a single burst.</param>
/// <summary>
/// Options that control token-bucket-style rate limiting: configured events per second and burst capacity.
/// </summary>
/// <param name="eventsPerSecond">The number of events permitted per second.</param>
/// <param name="maxBurstSize">The maximum number of tokens that may accumulate for burst processing.</param>
public class RateLimitOptions(int eventsPerSecond, int maxBurstSize)
{
    /// <summary>
    /// The number of events permitted per second.
    /// </summary>
    public int EventsPerSecond { get; set; } = eventsPerSecond;

    /// <summary>
    /// The maximum number of events that may be processed in a burst (the token bucket capacity).
    /// </summary>
    public int MaxBurstSize { get; set; } = maxBurstSize;

    /// <summary>
    /// Low processing speed: 100 events/sec with a 1000 event burst.
    /// Suitable for lightweight or resource-constrained scenarios.
    /// </summary>
    public static RateLimitOptions LowSpeed => new(100, 1000);

    /// <summary>
    /// Medium processing speed: 1000 events/sec with a 5000 event burst.
    /// Balanced for typical workloads with moderate event volumes.
    /// </summary>
    public static RateLimitOptions MediumSpeed => new(1000, 5000);

    /// <summary>
    /// High processing speed: 10,000 events/sec with a 10,000 event burst.
    /// Ideal for high-throughput scenarios like large scans.
    /// </summary>
    public static RateLimitOptions HighSpeed => new(10000, 10000);

    /// <summary>
    /// Unrestricted processing speed: 1,000,000 events/sec with a 1,000,000 event burst.
    /// For testing or scenarios where maximum speed is needed with no throttling.
    /// </summary>
    public static RateLimitOptions Unrestricted => new(1000000, 1000000);

    /// <summary>
    /// A conservative default rate limit (100 events/sec, 1000 burst capacity).
    /// </summary>
    public static RateLimitOptions Default => new(100, 1000);
}

/// <summary>
/// A simple token-bucket rate limiter that grants permission to process events according to
/// the configured rate and burst size. Callers await <see cref="WaitForTokenAsync"/> to obtain a token.
/// </summary>
/// <param name="eventsPerSecond">Number of tokens replenished per second.</param>
/// <param name="maxBurstSize">Maximum number of tokens that can be accumulated.</param>
public class RateLimiter(int eventsPerSecond, int maxBurstSize)
{
    private readonly double tokensPerSecond = eventsPerSecond;
    private double currentTokens = maxBurstSize;
    private DateTime lastRefill = DateTime.UtcNow;

    /// <summary>
    /// Waits asynchronously until a token is available and then consumes one token.
    /// The method observes the provided <paramref name="token"/> and will stop waiting if cancellation is requested.
    /// </summary>
    /// <param name="token">Cancellation token used to cancel waiting for a token.</param>
    public async Task WaitForTokenAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            this.RefillTokens();

            if (this.currentTokens >= 1)
            {
                this.currentTokens -= 1;
                return;
            }

            await Task.Delay(100, token);
        }
    }

    private void RefillTokens()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - this.lastRefill).TotalSeconds;
        this.currentTokens = Math.Min(maxBurstSize, this.currentTokens + elapsed * this.tokensPerSecond);
        this.lastRefill = now;
    }
}
