// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public interface IRetryStartupTask
{
    RetryStartupTaskOptions Options { get; }
}

public class RetryStartupTaskOptions
{
    public int Attempts { get; set; } = 3;

    public TimeSpan Backoff { get; set; } = new TimeSpan(0, 0, 0, 0, 200);

    public bool BackoffExponential { get; set; }
}