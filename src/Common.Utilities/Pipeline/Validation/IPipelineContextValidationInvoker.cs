// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Validates pipeline execution contexts before the pipeline runtime enters hooks, behaviors, or steps.
/// </summary>
public interface IPipelineContextValidationInvoker
{
    /// <summary>
    /// Validates the specified pipeline context using all registered FluentValidation validators for its runtime type.
    /// </summary>
    /// <param name="context">The pipeline context to validate.</param>
    /// <param name="serviceProvider">The scoped service provider used to resolve validators.</param>
    /// <param name="cancellationToken">The cancellation token for the validation operation.</param>
    /// <returns>A successful <see cref="Result"/> when validation passes; otherwise a failed <see cref="Result"/> containing validation errors.</returns>
    Task<Result> ValidateAsync(
        PipelineContextBase context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}
