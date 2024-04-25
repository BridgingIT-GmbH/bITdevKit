// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Infrastructure.EntityFramework;

using AutoMapper;
using BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;

public static class MapperFactory
{
    public static IMapper Create()
    {
        var configuration = new MapperConfiguration(c =>
        {
            c.CreateMap<UserAccount, DbUserAccount>()
                .ForMember(d => d.Identifier, o => o.MapFrom(s => s.Id))
                .ForMember(d => d.EmailAddress, o => o.MapFrom(s => s.Email))
                .ForMember(d => d.Visits, o => o.MapFrom(s => s.VisitCount))
                .ForMember(d => d.LastVisitDate, o => o.MapFrom(s => s.LastVisitDate))
                .ForMember(d => d.RegisterDate, o => o.MapFrom(s => s.RegisterDate))
                .ForMember(d => d.AdDomain, o => o.MapFrom(s => s.AdAccount.Domain))
                .ForMember(d => d.AdName, o => o.MapFrom(s => s.AdAccount.Name));

            c.CreateMap<DbUserAccount, UserAccount>()
                .ForMember(d => d.Id, o => o.MapFrom(s => s.Identifier))
                .ForMember(d => d.Email, o => o.MapFrom(s => s.EmailAddress))
                .ForMember(d => d.VisitCount, o => o.MapFrom(s => s.Visits))
                .ForMember(d => d.LastVisitDate, o => o.MapFrom(s => s.LastVisitDate))
                .ForMember(d => d.RegisterDate, o => o.MapFrom(s => s.RegisterDate))
                .ForMember(d => d.AdAccount, o => o.MapFrom(new AdAccountResolver()));
        });

        configuration.AssertConfigurationIsValid();
        return configuration.CreateMapper();
    }

    private class AdAccountResolver : IValueResolver<DbUserAccount, UserAccount, AdAccount>
    {
        public AdAccount Resolve(DbUserAccount source, UserAccount destination, AdAccount destMember, ResolutionContext context)
        {
            return AdAccount.For($"{source.AdDomain}\\{source.AdName}");
        }
    }
}