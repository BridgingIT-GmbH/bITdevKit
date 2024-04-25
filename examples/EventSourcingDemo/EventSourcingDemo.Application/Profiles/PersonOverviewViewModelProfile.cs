// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Application.Profiles;

using Domain.Model;
using Persons;

public class PersonOverviewViewModelProfile : AutoMapper.Profile
{
    public PersonOverviewViewModelProfile()
    {
        this.CreateMap<PersonOverviewViewModel, Person>();
    }
}