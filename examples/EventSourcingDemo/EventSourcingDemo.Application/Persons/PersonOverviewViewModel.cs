﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Application.Persons;

public class PersonOverviewViewModel
{
    public Guid Id { get; set; }

    public string Firstname { get; set; }

    public string Lastname { get; set; }
}