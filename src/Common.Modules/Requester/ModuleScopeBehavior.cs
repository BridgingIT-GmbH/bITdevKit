// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
///     Adds the request module name to the logging scope and rejects requests for disabled modules.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The result response type.</typeparam>
/// <param name="loggerFactory">The factory used to create the behavior logger.</param>
/// <param name="moduleAccessors">The accessors used to resolve the request's module.</param>
/// <param name="activitySources">Activity sources associated with the application modules.</param>
public class ModuleScopeBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    IEnumerable<IModuleContextAccessor> moduleAccessors = null,
    IEnumerable<ActivitySource> activitySources = null) : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly IEnumerable<IModuleContextAccessor> moduleAccessors = moduleAccessors;
    private readonly IEnumerable<ActivitySource> activitySources = activitySources;
    private const string ModuleNameLogKey = "ModuleName";

    /// <summary>
    ///     Indicates that every request is processed by this behavior.
    /// </summary>
    /// <returns>Always <see langword="true"/>.</returns>
    protected override bool CanProcess(TRequest request, Type handlerType)
    {
        return true; // Always process, no attribute required
    }

    /// <summary>
    ///     Executes the next handler within a module-name logging scope.
    /// </summary>
    /// <exception cref="ModuleNotEnabledException">The resolved module is disabled.</exception>
    protected override async Task<TResponse> Process(
        TRequest request,
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken)
    {
        var module = this.moduleAccessors?.Find(request.GetType());
        var moduleName = module?.Name ?? "UnknownModule";

        using (this.Logger.BeginScope(new Dictionary<string, object>
        {
            [ModuleNameLogKey] = moduleName
        }))
        {
            if (module?.Enabled == false)
            {
                throw new ModuleNotEnabledException(moduleName);
            }

            return await next();
        }
    }
}
