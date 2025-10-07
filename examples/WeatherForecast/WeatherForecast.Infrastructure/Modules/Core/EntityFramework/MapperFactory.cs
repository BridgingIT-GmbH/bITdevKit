// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Infrastructure.EntityFramework;

using Domain.Model;
using Mapster;
using MapsterMapper;

public static class MapperFactory
{
    public static IMapper Create()
    {
        var config = new TypeAdapterConfig();

        config.NewConfig<UserAccount, DbUserAccount>()
            .Map(dest => dest.Identifier, src => src.Id)
            .Map(dest => dest.EmailAddress, src => src.Email)
            .Map(dest => dest.Visits, src => src.VisitCount)
            .Map(dest => dest.LastVisitDate, src => src.LastVisitDate)
            .Map(dest => dest.RegisterDate, src => src.RegisterDate)
            .Map(dest => dest.AdDomain, src => src.AdAccount.Domain)
            .Map(dest => dest.AdName, src => src.AdAccount.Name);

        config.NewConfig<DbUserAccount, UserAccount>()
            .Map(dest => dest.Id, src => src.Identifier)
            .Map(dest => dest.Email, src => src.EmailAddress)
            .Map(dest => dest.VisitCount, src => src.Visits)
            .Map(dest => dest.LastVisitDate, src => src.LastVisitDate)
            .Map(dest => dest.RegisterDate, src => src.RegisterDate)
            .Map(dest => dest.AdAccount, src => AdAccount.Create($"{src.AdDomain}\\{src.AdName}"));

        return new Mapper(config);
    }
}