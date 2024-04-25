// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Utilities;

using BridgingIT.DevKit.Common;

public class StartupTaskDefinition
{
    public Type TaskType { get; set; }

    public StartupTaskOptions Options { get; set; } = new StartupTaskOptions();
}