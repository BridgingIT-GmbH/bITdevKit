// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands.Outbox;

public enum OutboxMessageCommandResultErrorCodes
{
    NoError = 0,
    DuplicatedMessage = 1,
    OtherError = 2
}