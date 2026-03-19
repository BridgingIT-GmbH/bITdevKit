// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Default row interceptor resolver backed by dependency injection.
/// </summary>
public sealed class RowInterceptorsProvider(IServiceProvider serviceProvider) : IRowInterceptorsProvider
{
    private readonly IServiceProvider serviceProvider = serviceProvider;

    /// <inheritdoc/>
    public IReadOnlyList<IImportRowInterceptor<TTarget>> GetImportInterceptors<TTarget>()
        where TTarget : class
    {
        return this.serviceProvider.GetServices<IImportRowInterceptor<TTarget>>().ToArray();
    }

    /// <inheritdoc/>
    public IReadOnlyList<IExportRowInterceptor<TSource>> GetExportInterceptors<TSource>()
        where TSource : class
    {
        return this.serviceProvider.GetServices<IExportRowInterceptor<TSource>>().ToArray();
    }
}
