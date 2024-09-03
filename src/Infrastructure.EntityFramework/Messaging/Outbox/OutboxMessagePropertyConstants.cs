﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;

public struct OutboxMessagePropertyConstants
{
    public const string ProcessStatusKey = "ProcessStatus";

    public const string ProcessMessageKey = "ProcessMessage";

    public const string ProcessAttemptsKey = "ProcessAttempts";
}