// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

internal sealed class InlineJobRuntime(InlineJobHandlerRegistry registry, IServiceProvider serviceProvider) : IJob
{
    public Task<IResult> ExecuteAsync(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return this.ExecuteInternalAsync(context, cancellationToken);
    }

    private async Task<IResult> ExecuteInternalAsync(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        return await registry.ExecuteAsync(context, serviceProvider, cancellationToken).ConfigureAwait(false);
    }
}