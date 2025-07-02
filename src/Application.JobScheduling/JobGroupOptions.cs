// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

public class JobGroupOptions
{
    public string[] DisallowConcurrentExecutionGroups { get; set; } = [];

    public bool DisallowConcurrentExecutionDefaultGroup { get; set; }
}
