// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Orchestrations;

using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Data contract for the WeatherFiesta hello-world orchestration sample.
/// </summary>
/// <example>
/// <code>
/// var data = new WeatherHelloWorldOrchestrationData { Greeting = "Hello WeatherFiesta" };
/// </code>
/// </example>
public sealed class WeatherHelloWorldOrchestrationData : IOrchestrationData
{
    /// <summary>
    /// Gets or sets the greeting that is processed by the orchestration.
    /// </summary>
    public string Greeting { get; set; }

    /// <summary>
    /// Gets or sets the source that started the orchestration.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the sample was requested.
    /// </summary>
    public DateTimeOffset RequestedUtc { get; set; }

    /// <summary>
    /// Gets or sets the mutable orchestration trace.
    /// </summary>
    public List<string> Steps { get; set; } = [];
}

/// <summary>
/// Small durable orchestration that demonstrates multiple states, activities, and context mutation.
/// </summary>
/// <example>
/// <code>
/// services.AddOrchestrations().WithOrchestration&lt;WeatherHelloWorldOrchestration&gt;();
/// </code>
/// </example>
public sealed class WeatherHelloWorldOrchestration : Orchestration<WeatherHelloWorldOrchestrationData>
{
    /// <inheritdoc />
    protected override void Define(IOrchestrationBuilder<WeatherHelloWorldOrchestrationData> builder)
    {
        builder
            .State("Created", state => state
                .Activity<WeatherHelloWorldPrepareActivity>()
                .TransitionTo("Forecast"))
            .State("Forecast", state => state
                .Activity<WeatherHelloWorldForecastActivity>()
                .TransitionTo("Report"))
            .State("Report", state => state
                .Activity<WeatherHelloWorldReportActivity>()
                .TransitionTo("PublishMessage"))
            .State("PublishMessage", state => state
                .PublishMessageActivity<WeatherHelloWorldOrchestrationData, WeatherHelloWorldMessage>(message => message
                    .Message(context => new WeatherHelloWorldMessage(
                        context.Data.Greeting,
                        context.Data.Source,
                        context.Data.RequestedUtc,
                        context.Data.Steps))
                    .CorrelationId(context => context.CorrelationId)
                    .Property("Scope", _ => "hello-world")
                    .Property("Source", context => context.Data.Source)
                    .Property("StepCount", context => context.Data.Steps?.Count ?? 0))
                .TransitionTo("QueueMessage"))
            .State("QueueMessage", state => state
                .SendQueueMessageActivity<WeatherHelloWorldOrchestrationData, WeatherHelloWorldQueueMessage>(message => message
                    .Message(context => new WeatherHelloWorldQueueMessage(
                        context.Data.Greeting,
                        context.Data.Source,
                        context.Data.RequestedUtc,
                        context.Data.Steps))
                    .CorrelationId(context => context.CorrelationId)
                    .Property("Scope", _ => "hello-world")
                    .Property("Source", context => context.Data.Source)
                    .Property("StepCount", context => context.Data.Steps?.Count ?? 0))
                .TransitionTo("Finished"))
            .State("Finished", state => state
                .Activity<WeatherHelloWorldCompleteActivity>()
                .Complete());
    }
}

/// <summary>
/// Prepares the hello-world orchestration context.
/// </summary>
/// <example>
/// <code>
/// state.Activity&lt;WeatherHelloWorldPrepareActivity&gt;();
/// </code>
/// </example>
public sealed class WeatherHelloWorldPrepareActivity(ILogger<WeatherHelloWorldPrepareActivity> logger) : IOrchestrationActivity<WeatherHelloWorldOrchestrationData>
{
    /// <inheritdoc />
    public Task<OrchestrationOutcome> ExecuteAsync(
        OrchestrationContext<WeatherHelloWorldOrchestrationData> context,
        CancellationToken cancellationToken = default)
    {
        context.Data.Steps.Add($"Prepared greeting '{context.Data.Greeting}'.");
        logger.LogInformation("Prepared WeatherFiesta hello-world orchestration {CorrelationId}.", context.CorrelationId);

        return Task.FromResult(OrchestrationOutcome.Continue());
    }
}

/// <summary>
/// Adds a synthetic weather forecast step to the hello-world orchestration context.
/// </summary>
/// <example>
/// <code>
/// state.Activity&lt;WeatherHelloWorldForecastActivity&gt;();
/// </code>
/// </example>
public sealed class WeatherHelloWorldForecastActivity(ILogger<WeatherHelloWorldForecastActivity> logger) : IOrchestrationActivity<WeatherHelloWorldOrchestrationData>
{
    /// <inheritdoc />
    public Task<OrchestrationOutcome> ExecuteAsync(
        OrchestrationContext<WeatherHelloWorldOrchestrationData> context,
        CancellationToken cancellationToken = default)
    {
        context.Data.Steps.Add("Looked outside: sunny with a chance of dashboard insight.");
        logger.LogInformation("Forecasted WeatherFiesta hello-world orchestration {CorrelationId}.", context.CorrelationId);

        return Task.FromResult(OrchestrationOutcome.Continue());
    }
}

/// <summary>
/// Adds a synthetic report step to the hello-world orchestration context.
/// </summary>
/// <example>
/// <code>
/// state.Activity&lt;WeatherHelloWorldReportActivity&gt;();
/// </code>
/// </example>
public sealed class WeatherHelloWorldReportActivity(ILogger<WeatherHelloWorldReportActivity> logger) : IOrchestrationActivity<WeatherHelloWorldOrchestrationData>
{
    /// <inheritdoc />
    public Task<OrchestrationOutcome> ExecuteAsync(
        OrchestrationContext<WeatherHelloWorldOrchestrationData> context,
        CancellationToken cancellationToken = default)
    {
        context.Data.Steps.Add($"Reported '{context.Data.Greeting}' from {context.Data.Source}.");
        logger.LogInformation("Reported WeatherFiesta hello-world orchestration {CorrelationId}.", context.CorrelationId);

        return Task.FromResult(OrchestrationOutcome.Continue());
    }
}

/// <summary>
/// Completes the hello-world orchestration context.
/// </summary>
/// <example>
/// <code>
/// state.Activity&lt;WeatherHelloWorldCompleteActivity&gt;();
/// </code>
/// </example>
public sealed class WeatherHelloWorldCompleteActivity(ILogger<WeatherHelloWorldCompleteActivity> logger) : IOrchestrationActivity<WeatherHelloWorldOrchestrationData>
{
    /// <inheritdoc />
    public Task<OrchestrationOutcome> ExecuteAsync(
        OrchestrationContext<WeatherHelloWorldOrchestrationData> context,
        CancellationToken cancellationToken = default)
    {
        context.Data.Steps.Add("Completed hello-world orchestration.");
        logger.LogInformation("Completed WeatherFiesta hello-world orchestration {CorrelationId}.", context.CorrelationId);

        return Task.FromResult(OrchestrationOutcome.Continue());
    }
}
