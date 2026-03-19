// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Resolves typed row interceptors for import and export operations.
/// </summary>
public interface IRowInterceptorsProvider
{
    /// <summary>
    /// Gets the import row interceptors for the specified target type.
    /// </summary>
    IReadOnlyList<IImportRowInterceptor<TTarget>> GetImportInterceptors<TTarget>()
        where TTarget : class;

    /// <summary>
    /// Gets the export row interceptors for the specified source type.
    /// </summary>
    IReadOnlyList<IExportRowInterceptor<TSource>> GetExportInterceptors<TSource>()
        where TSource : class;
}
