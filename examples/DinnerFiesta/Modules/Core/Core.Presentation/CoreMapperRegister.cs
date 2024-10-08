﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Presentation;

using Common;
using Domain;
using Mapster;
using Web.Controllers;

public class CoreMapperRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<User, UserModel>()
            .Map(d => d.FirstName, s => s.FirstName)
            .Map(d => d.Email, s => s.Email.Value);

        config.ForType<Result<User>, UserModel>()
            .Map(d => d, s => s.Value.Adapt<UserModel>(config));
    }
}