// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Infrastructure;

using Domain.Model;
using Mapster;
using Models;

public class MapperRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<PersonDatabaseEntity, Person>();
        config.NewConfig<PersonDatabaseEntity, PersonOverview>().TwoWays();
    }
}