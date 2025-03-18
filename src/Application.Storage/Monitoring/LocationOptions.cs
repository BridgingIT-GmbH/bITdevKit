// File: BridgingIT.DevKit.Application.FileMonitoring/LocationHandler.cs
namespace BridgingIT.DevKit.Application.FileMonitoring;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.FileMonitoring;

/// <summary>
/// Configures options for a monitored location.
/// </summary>
public class LocationOptions(string name)
{
    public string Name { get; } = name;
    public string FilePattern { get; set; } = "*.*";
    public bool UseOnDemandOnly { get; set; }
    public RateLimitOptions RateLimit { get; } = new();
    public List<Type> LocationProcessorBehaviors { get; } = [];
    public List<ProcessorConfig> ProcessorConfigs { get; } = [];

    public LocationOptions WithProcessorBehavior<TBehavior>() where TBehavior : IProcessorBehavior
    {
        this.LocationProcessorBehaviors.Add(typeof(TBehavior));
        return this;
    }

    public ProcessorConfig UseProcessor<TProcessor>() where TProcessor : IFileEventProcessor
    {
        var config = new ProcessorConfig { ProcessorType = typeof(TProcessor) };
        this.ProcessorConfigs.Add(config);

        return config;
    }
}

public class ProcessorConfig
{
    public Type ProcessorType { get; set; }

    public List<Type> BehaviorTypes { get; } = [];

    public ProcessorConfig WithBehavior<TBehavior>() where TBehavior : IProcessorBehavior
    {
        this.BehaviorTypes.Add(typeof(TBehavior));
        return this;
    }
}

public class RateLimitOptions
{
    public int EventsPerSecond { get; set; } = 100;

    public int MaxBurstSize { get; set; } = 1000;
}

internal class RateLimiter(int eventsPerSecond, int maxBurstSize)
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