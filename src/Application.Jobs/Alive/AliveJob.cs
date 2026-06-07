// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;

/// <summary>
/// Represents the input for the built-in jobs alive probe.
/// </summary>
/// <example>
/// <code>
/// var data = new AliveJobData { Source = "dashboard" };
/// </code>
/// </example>
public sealed class AliveJobData
{
    /// <summary>
    /// Gets or sets the component that requested the probe.
    /// </summary>
    public string Source { get; set; } = "dashboard";

    /// <summary>
    /// Gets or sets the correlation identifier assigned to this probe.
    /// </summary>
    public string CorrelationId { get; set; } = GuidGenerator.CreateSequential().ToString("N");
}

/// <summary>
/// Executes the built-in jobs alive probe.
/// </summary>
/// <example>
/// <code>
/// await scheduler.DispatchAsync("alive-job", new AliveJobData(), cancellationToken: cancellationToken);
/// </code>
/// </example>
public sealed class AliveJob(ILogger<AliveJob> logger) : JobBase<AliveJobData>
{
    /// <summary>
    /// The stable job name used by the built-in alive probe.
    /// </summary>
    public const string JobName = "alive-job";

    /// <summary>
    /// The manual trigger name used by the built-in alive probe.
    /// </summary>
    public const string TriggerName = "manual";

    /// <inheritdoc />
    public override async Task<Result> ExecuteAsync(IJobExecutionContext<AliveJobData> context, CancellationToken cancellationToken = default)
    {
        var source = string.IsNullOrWhiteSpace(context.Data?.Source) ? "dashboard" : context.Data.Source.Trim();
        var correlationId = string.IsNullOrWhiteSpace(context.Data?.CorrelationId)
            ? GuidGenerator.CreateSequential().ToString("N")
            : context.Data.CorrelationId.Trim();

        await Task.Delay(600, cancellationToken); // Simulate some work

        context.Messages.Add($"Alive job completed from '{source}' with correlation '{correlationId}'.");

        logger.LogInformation(
            "{LogKey} job alive probe handled (job={JobName}, correlationId={CorrelationId}, source={Source})",
            Constants.LogKey,
            context.JobName,
            correlationId,
            source);

        return Result.Success();
    }
}
