// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using BridgingIT.DevKit.Common;

public interface IChaosExceptionMessageHandler
{
    ChaosExceptionMessageHandlerOptions Options { get; }
}

public class ChaosExceptionMessageHandlerOptions
{
    /// <summary>
    /// A decimal between 0 and 1 inclusive. The policy will inject the fault, randomly, that proportion of the time, eg: if 0.2, twenty percent of calls will be randomly affected; if 0.01, one percent of calls; if 1, all calls.
    /// </summary>
    public double InjectionRate { get; set; }

    public Exception Fault { get; set; } = new ChaosException();
}
