// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;
using BridgingIT.DevKit.Common;

public class QueryResponse<TResult>
{
    public QueryResponse(string cancelledReason = null)
    {
        if (!string.IsNullOrEmpty(cancelledReason))
        {
            this.Cancelled = true;
            this.CancelledReason = cancelledReason;
        }
    }

    public bool Cancelled { get; private set; }

    public string CancelledReason { get; private set; }

    public TResult Result { get; set; }

    public static QueryResponse<Result<TResult>> For(Result result = null)
    {
        if (result?.IsFailure == true)
        {
            return new QueryResponse<Result<TResult>>()
            {
                Result = Result<TResult>.Failure()
                    .WithMessages(result?.Messages)
                    .WithErrors(result?.Errors),
            };
        }

        return new QueryResponse<Result<TResult>>()
        {
            Result = Result<TResult>.Success()
                    .WithMessages(result?.Messages)
                    .WithErrors(result?.Errors),
        };
    }

    public void SetCancelled(string cancelledReason)
    {
        if (!string.IsNullOrEmpty(cancelledReason))
        {
            this.Cancelled = true;
            this.CancelledReason = cancelledReason;
        }
    }
}