// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;

public interface ITimeoutStartupTask
{
    TimeoutStartupTaskOptions Options { get; }
}

public class TimeoutStartupTaskOptions
{
    public TimeSpan Timeout { get; set; } = new TimeSpan(0, 0, 0, 30);
}
