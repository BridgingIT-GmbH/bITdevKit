// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Configures options for a monitored location.
/// </summary>
public class LocationOptions(string name)
{
    public string LocationName { get; } = name;

    public string FilePattern { get; set; } = "*.*";

    public bool UseOnDemandOnly { get; set; }

    public RateLimitOptions RateLimit { get; set; } = RateLimitOptions.MediumSpeed;

    public List<Type> LocationProcessorBehaviors { get; } = [];

    public List<ProcessorConfiguration> ProcessorConfigs { get; } = [];

    public LocationOptions WithProcessorBehavior<TBehavior>() where TBehavior : IProcessorBehavior
    {
        this.LocationProcessorBehaviors.Add(typeof(TBehavior));
        return this;
    }

    public ProcessorConfiguration UseProcessor<TProcessor>(Action<ProcessorConfiguration> configure = null)
        where TProcessor : IFileEventProcessor
    {
        var config = new ProcessorConfiguration { ProcessorType = typeof(TProcessor) };
        configure?.Invoke(config);
        this.ProcessorConfigs.Add(config);

        return config;
    }
}

public class ProcessorConfiguration
{
    public Type ProcessorType { get; set; }

    public List<Type> BehaviorTypes { get; } = [];

    public Action<object> Configure { get; set; } // Delegate to configure the processor instance

    public ProcessorConfiguration WithBehavior<TBehavior>() where TBehavior : IProcessorBehavior
    {
        this.BehaviorTypes.Add(typeof(TBehavior));
        return this;
    }

    public ProcessorConfiguration WithConfiguration(Action<object> configure)
    {
        this.Configure = configure;
        return this;
    }
}

public class RateLimitOptions(int eventsPerSecond, int maxBurstSize)
{
    public int EventsPerSecond { get; set; } = eventsPerSecond;

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
    /// Default processing speed: 100 events/sec with 1000 burst.
    /// </summary>
    public static RateLimitOptions Default => new(100, 1000);
}

public class RateLimiter(int eventsPerSecond, int maxBurstSize)
{
    private readonly double tokensPerSecond = eventsPerSecond;
    private double currentTokens = maxBurstSize;
    private DateTime lastRefill = DateTime.UtcNow;

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