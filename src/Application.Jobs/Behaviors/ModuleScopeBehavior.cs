// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using System.Diagnostics;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Captures module scope around a job execution.
/// </summary>
public sealed class ModuleScopeBehavior : IJobBehavior
{
    private readonly IEnumerable<IModuleContextAccessor> moduleAccessors;
    private readonly ILogger<ModuleScopeBehavior> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleScopeBehavior"/> class.
    /// </summary>
    public ModuleScopeBehavior(
        ILoggerFactory loggerFactory,
        IEnumerable<IModuleContextAccessor> moduleAccessors = null)
    {
        this.moduleAccessors = moduleAccessors;
        this.logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<ModuleScopeBehavior>();
    }

    /// <inheritdoc />
    public async Task<IResult<JobExecutionResult>> HandleAsync(
        JobBehaviorContext context,
        JobBehaviorDelegate next,
        CancellationToken cancellationToken = default)
    {
        var module = this.moduleAccessors.Find(context.JobType);
        var moduleName = module?.Name ?? context.Definition.Module ?? ModuleConstants.UnknownModuleName;

        using (this.logger.BeginScope(new Dictionary<string, object>
        {
            [ModuleConstants.ModuleNameKey] = moduleName,
        }))
        {
            if (module is not null && !module.Enabled)
            {
                throw new ModuleNotEnabledException(moduleName);
            }

            Activity.Current?.SetTag(ActivityConstants.ModuleNameTagKey, moduleName);
            Activity.Current?.SetBaggage(ActivityConstants.ModuleNameTagKey, moduleName);

            return await next().ConfigureAwait(false);
        }
    }
}
