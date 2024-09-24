// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public interface ICircuitBreakerStartupTask : IStartupTask
{
    CircuitBreakerTaskOptions Options { get; }
}

public class CircuitBreakerTaskOptions
{
    public int Attempts { get; set; } = 3;

    public TimeSpan Backoff { get; set; } = new(0, 0, 0, 0, 200);

    public bool BackoffExponential { get; set; }

    public TimeSpan BreakDuration { get; set; } = new(0, 0, 0, 30);
}