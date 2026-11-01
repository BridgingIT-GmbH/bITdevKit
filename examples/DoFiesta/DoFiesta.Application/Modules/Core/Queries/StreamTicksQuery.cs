// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using System.Runtime.CompilerServices;
using BridgingIT.DevKit.Common;
using FluentValidation;

/// <summary>Prepares a cancellable live fictional tick sequence.</summary>
/// <example><code>var query = new StreamTicksQuery { IntervalMilliseconds = 20000 };</code></example>
[Query]
public partial class StreamTicksQuery
{
    /// <summary>Tick interval between 1,000 and 60,000 milliseconds.</summary>
    /// <example><code>query.IntervalMilliseconds = 20000;</code></example>
    public int IntervalMilliseconds { get; set; } = 1000;

    [Validate]
    private static void Validate(InlineValidator<StreamTicksQuery> validator) => validator.RuleFor(q => q.IntervalMilliseconds).InclusiveBetween(1000, 60000);

    [Handle]
    private Task<Result<IAsyncEnumerable<StreamTickModel>>> HandleAsync(TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        if (this.IntervalMilliseconds is < 1000 or > 60000)
        {
            return Task.FromResult(Result<IAsyncEnumerable<StreamTickModel>>.Failure().WithError(new ValidationError("Tick interval must be between 1000 and 60000 milliseconds.")));
        }

        return Task.FromResult(Result<IAsyncEnumerable<StreamTickModel>>.Success(StreamAsync(timeProvider, TimeSpan.FromMilliseconds(this.IntervalMilliseconds))));
    }

    private static async IAsyncEnumerable<StreamTickModel> StreamAsync(TimeProvider clock, TimeSpan interval, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        long sequence = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new StreamTickModel(checked(++sequence), clock.GetUtcNow());
            await Task.Delay(interval, clock, cancellationToken);
        }
    }
}
