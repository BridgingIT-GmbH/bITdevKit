// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Domain.Model;

using System;
using BridgingIT.DevKit.Domain.Model;

public class PersonOverview : Entity<Guid>
{
    public string Firstname { get; set; }

    public string Lastname { get; set; }
}