// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands.Outbox;

public class OutboxMessageCommandResult
{
    public OutboxMessageCommandResult()
    {
        this.ErrorCode = OutboxMessageCommandResultErrorCodes.NoError;
    }

    public OutboxMessageCommandResult(OutboxMessageCommandResultErrorCodes errorCode)
    {
        this.ErrorCode = errorCode;
    }

    public bool HasError
    {
        get { return this.ErrorCode != OutboxMessageCommandResultErrorCodes.NoError; }
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public OutboxMessageCommandResultErrorCodes ErrorCode { get; set; }
}