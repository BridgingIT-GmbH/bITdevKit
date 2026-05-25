// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Injects chaos exceptions into orchestration activity execution for orchestration types that opt in.
/// </summary>
public class ChaosExceptionOrchestrationBehavior(ILoggerFactory loggerFactory = null) : OrchestrationBehaviorBase(loggerFactory)
{
    /// <inheritdoc />
    public override async Task<OrchestrationOutcome> ExecuteAsync(
        OrchestrationActivityExecutionContext context,
        CancellationToken cancellationToken,
        OrchestrationDelegate next)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var options = this.ResolveOptions(context);
        if (options?.InjectionRate <= 0)
        {
            return await next().ConfigureAwait(false);
        }

        if (Random.Shared.NextDouble() < options.InjectionRate)
        {
            throw options.Fault ?? new ChaosException();
        }

        return await next().ConfigureAwait(false);
    }

    private ChaosExceptionOrchestrationOptions ResolveOptions(OrchestrationActivityExecutionContext context)
    {
        var registrations = context.Services.GetService<OrchestrationRegistrationStore>();
        if (registrations is null || !registrations.TryGetByName(context.OrchestrationName, out var orchestrationType))
        {
            return null;
        }

        return context.Services.GetService(orchestrationType) is IChaosExceptionOrchestration orchestration
            ? orchestration.Options
            : null;
    }
}
