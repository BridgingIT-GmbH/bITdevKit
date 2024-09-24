// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Presentation;

using Common;
using Domain;
using Mapster;
using Web.Controllers;

public class MarketingMapperRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<Customer, CustomerResponseModel>()
            .Map(d => d.FirstName, s => s.FirstName)
            .Map(d => d.Email, s => s.Email.Value);

        config.ForType<Result<Customer>, ResultOfCustomerResponseModel>()
            .Map(d => d.Value, s => s.Value.Adapt<CustomerResponseModel>(config));

        config.ForType<Result<IEnumerable<Customer>>, ResultOfCustomersResponseModel>()
            .Map(d => d.Value, s => s.Value.Adapt<IEnumerable<CustomerResponseModel>>(config));
    }
}