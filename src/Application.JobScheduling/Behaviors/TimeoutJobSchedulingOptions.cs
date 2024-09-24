// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

public interface ITimeoutJobScheduling
{
    TimeoutJobSchedulingOptions Options { get; }
}

public class TimeoutJobSchedulingOptions
{
    public TimeSpan Timeout { get; set; } = new(0, 0, 0, 30);
}