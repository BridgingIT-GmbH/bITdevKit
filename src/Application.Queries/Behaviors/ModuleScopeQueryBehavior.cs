// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

using System.Diagnostics;
using Common;
using MediatR;
using Microsoft.Extensions.Logging;

public class ModuleScopeQueryBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    IEnumerable<IModuleContextAccessor> moduleAccessors = null,
    IEnumerable<ActivitySource> activitySources = null) : QueryBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class, IRequest<TResponse>
{
    private readonly IEnumerable<IModuleContextAccessor> moduleAccessors = moduleAccessors;
    private readonly IEnumerable<ActivitySource> activitySources = activitySources;

    protected override bool CanProcess(TRequest request)
    {
        return true;
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var module = this.moduleAccessors.Find(request.GetType());
        var moduleName = module?.Name ?? ModuleConstants.UnknownModuleName;

        using (this.Logger.BeginScope(new Dictionary<string, object>
               {
                   [ModuleConstants.ModuleNameKey] = moduleName
               }))
        {
            if (module is not null && !module.Enabled)
            {
                throw new ModuleNotEnabledException(moduleName);
            }

            return await next().AnyContext();
        }
    }
}