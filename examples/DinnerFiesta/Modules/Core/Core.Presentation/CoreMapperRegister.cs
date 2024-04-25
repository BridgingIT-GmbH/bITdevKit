// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Presentation;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Presentation.Web.Controllers;
using Mapster;

public class CoreMapperRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<User, UserResponseModel>()
            .Map(d => d.FirstName, s => s.FirstName)
            .Map(d => d.Email, s => s.Email.Value);

        config.ForType<Result<User>, ResultOfUserResponseModel>()
            .Map(d => d.Value, s => s.Value.Adapt<UserResponseModel>(config));

        config.ForType<Result<IEnumerable<User>>, ResultOfUsersResponseModel>()
            .Map(d => d.Value, s => s.Value.Adapt<IEnumerable<UserResponseModel>>(config));
    }
}