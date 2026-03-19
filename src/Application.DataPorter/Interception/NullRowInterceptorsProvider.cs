// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Provides an empty row interceptor set for import and export operations.
/// </summary>
public sealed class NullRowInterceptorsProvider : IRowInterceptorsProvider
{
    /// <summary>
    /// Gets a shared empty provider instance.
    /// </summary>
    public static readonly NullRowInterceptorsProvider Instance = new();

    /// <inheritdoc/>
    public IReadOnlyList<IImportRowInterceptor<TTarget>> GetImportInterceptors<TTarget>()
        where TTarget : class
    {
        return [];
    }

    /// <inheritdoc/>
    public IReadOnlyList<IExportRowInterceptor<TSource>> GetExportInterceptors<TSource>()
        where TSource : class
    {
        return [];
    }
}
