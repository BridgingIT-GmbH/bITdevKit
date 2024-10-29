// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

public class CommandResponse
{
    public CommandResponse(string cancelledReason = null)
    {
        if (string.IsNullOrEmpty(cancelledReason))
        {
            return;
        }

        this.Cancelled = true;
        this.CancelledReason = cancelledReason;
    }

    public bool Cancelled { get; private set; }

    public string CancelledReason { get; private set; }

    public static CommandResponse For()
    {
        return new CommandResponse();
    }

    public static CommandResponse<TResult> For<TResult>()
    {
        return new CommandResponse<TResult>();
    }

    public static CommandResponse<TResult> For<TResult>(TResult result)
    {
        return new CommandResponse<TResult> { Result = result };
    }

    public void SetCancelled(string cancelledReason)
    {
        if (string.IsNullOrEmpty(cancelledReason))
        {
            return;
        }

        this.Cancelled = true;
        this.CancelledReason = cancelledReason;
    }
}