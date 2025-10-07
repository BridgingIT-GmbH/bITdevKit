// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Application.Profiles;

using Domain.Model;
using Mapster;
using Persons;

public class PersonMapperRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Person, PersonOverviewViewModel>(); config.NewConfig<Person, PersonOverview>();
    }
}