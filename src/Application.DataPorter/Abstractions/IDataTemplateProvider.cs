// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Provider interface for template generation operations.
/// </summary>
public interface IDataTemplateProvider : IDataPorterProvider
{
    /// <summary>
    /// Gets a value indicating whether this provider supports template generation.
    /// </summary>
    bool SupportsTemplateExport { get; }

    /// <summary>
    /// Generates a template to a stream.
    /// </summary>
    Task<ExportResult> GenerateTemplateAsync<TTarget>(
        Stream outputStream,
        TemplateConfiguration configuration,
        CancellationToken cancellationToken = default)
        where TTarget : class, new();
}
