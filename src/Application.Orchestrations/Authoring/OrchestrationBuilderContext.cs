// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Provides the shared builder context for orchestration registration.
/// </summary>
public class OrchestrationBuilderContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationBuilderContext" /> class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public OrchestrationBuilderContext(IServiceCollection services)
        : this(services, EnsureExecutionSettings(services))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationBuilderContext" /> class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="executionSettings">The orchestration execution settings.</param>
    public OrchestrationBuilderContext(
        IServiceCollection services,
        OrchestrationExecutionSettings executionSettings)
    {
        this.Services = services ?? throw new ArgumentNullException(nameof(services));
        this.ExecutionSettings = executionSettings ?? throw new ArgumentNullException(nameof(executionSettings));
    }

    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Gets the orchestration execution settings configured for this registration.
    /// </summary>
    public OrchestrationExecutionSettings ExecutionSettings { get; }

    /// <summary>
    /// Enables or disables background orchestration execution and recovery.
    /// </summary>
    /// <param name="value">The enabled value.</param>
    /// <returns>The current builder context.</returns>
    public OrchestrationBuilderContext Enabled(bool value = true)
    {
        this.ExecutionSettings.EnableBackgroundExecution = value;

        return this;
    }

    /// <summary>
    /// Disables background orchestration execution and recovery.
    /// </summary>
    /// <returns>The current builder context.</returns>
    public OrchestrationBuilderContext Disabled()
    {
        this.ExecutionSettings.EnableBackgroundExecution = false;

        return this;
    }

    /// <summary>
    /// Sets the delay before hosted orchestration recovery starts.
    /// </summary>
    /// <param name="timeSpan">The startup delay.</param>
    /// <returns>The current builder context.</returns>
    public OrchestrationBuilderContext StartupDelay(TimeSpan timeSpan)
    {
        this.ExecutionSettings.StartupDelay = timeSpan;

        return this;
    }

    /// <summary>
    /// Sets the delay before hosted orchestration recovery starts in milliseconds.
    /// </summary>
    /// <param name="milliseconds">The startup delay in milliseconds.</param>
    /// <returns>The current builder context.</returns>
    public OrchestrationBuilderContext StartupDelay(int milliseconds)
    {
        this.ExecutionSettings.StartupDelay = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    /// <summary>
    /// Sets the delay before hosted orchestration recovery starts from a time span string.
    /// </summary>
    /// <param name="value">The startup delay value.</param>
    /// <returns>The current builder context.</returns>
    public OrchestrationBuilderContext StartupDelay(string value)
    {
        if (TimeSpan.TryParse(value, out var timeSpan))
        {
            this.ExecutionSettings.StartupDelay = timeSpan;
        }

        return this;
    }

    /// <summary>
    /// Sets the interval between hosted orchestration recovery sweeps.
    /// </summary>
    /// <param name="timeSpan">The background sweep interval.</param>
    /// <returns>The current builder context.</returns>
    public OrchestrationBuilderContext BackgroundSweepInterval(TimeSpan timeSpan)
    {
        this.ExecutionSettings.BackgroundSweepInterval = timeSpan;

        return this;
    }

    /// <summary>
    /// Sets the interval between hosted orchestration recovery sweeps in milliseconds.
    /// </summary>
    /// <param name="milliseconds">The background sweep interval in milliseconds.</param>
    /// <returns>The current builder context.</returns>
    public OrchestrationBuilderContext BackgroundSweepInterval(int milliseconds)
    {
        this.ExecutionSettings.BackgroundSweepInterval = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    /// <summary>
    /// Sets the interval between hosted orchestration recovery sweeps from a time span string.
    /// </summary>
    /// <param name="value">The background sweep interval value.</param>
    /// <returns>The current builder context.</returns>
    public OrchestrationBuilderContext BackgroundSweepInterval(string value)
    {
        if (TimeSpan.TryParse(value, out var timeSpan))
        {
            this.ExecutionSettings.BackgroundSweepInterval = timeSpan;
        }

        return this;
    }

    /// <summary>
    /// Sets the maximum number of orchestration instances processed in one recovery sweep.
    /// </summary>
    /// <param name="value">The background sweep batch size.</param>
    /// <returns>The current builder context.</returns>
    public OrchestrationBuilderContext BackgroundSweepBatchSize(int value)
    {
        this.ExecutionSettings.BackgroundSweepBatchSize = value;

        return this;
    }

    private static OrchestrationExecutionSettings EnsureExecutionSettings(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(OrchestrationExecutionSettings));
        if (descriptor?.ImplementationInstance is OrchestrationExecutionSettings settings)
        {
            return settings;
        }

        settings = new OrchestrationExecutionSettings();
        services.TryAddSingleton(settings);

        return settings;
    }
}