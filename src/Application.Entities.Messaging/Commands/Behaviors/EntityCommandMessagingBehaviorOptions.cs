// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using BridgingIT.DevKit.Common;

public class EntityCommandMessagingBehaviorOptions : OptionsBase
{
    public bool Enabled { get; set; } = true;

    public List<Type> ExcludedEntityTypes { get; set; }

    public int PublishDelay { get; set; } = 100;
}