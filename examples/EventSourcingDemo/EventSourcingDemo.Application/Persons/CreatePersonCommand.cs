// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Application.Persons;

using DevKit.Application.Commands;

#pragma warning disable CS0618 // Type or member is obsolete
public class CreatePersonCommand : CommandRequestBase<PersonOverviewViewModel>
#pragma warning restore CS0618 // Type or member is obsolete
{
    public CreatePersonViewModel Model { get; set; }
}