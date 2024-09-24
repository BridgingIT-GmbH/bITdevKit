// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Infrastructure.Profiles;

using AutoMapper;
using Domain.Model;
using Models;

public class PersonDatabaseProfile : Profile
{
    public PersonDatabaseProfile()
    {
        this.CreateMap<PersonDatabaseEntity, Person>();
    }
}