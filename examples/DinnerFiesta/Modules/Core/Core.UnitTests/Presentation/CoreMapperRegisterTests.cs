// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.UnitTests.Presentation;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Presentation;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Presentation.Web.Controllers;
using Mapster;
using Shouldly;

public class CoreMapperRegisterTests
{
    private readonly IMapper sut;

    public CoreMapperRegisterTests()
    {
        var config = new TypeAdapterConfig();
        config.Scan(typeof(CoreMapperRegister).Assembly);

        this.sut = new Common.MapsterMapper(config);
    }

    [Fact]
    public void GetTypes_WhichImplementRegister_AreFound()
    {
        // Arrange
        // Act
        var result = typeof(CoreMapperRegister).Assembly.SafeGetTypes(typeof(IRegister));

        // Assert
        result.ShouldNotBeNull();
        result.ShouldContain(typeof(CoreMapperRegister));
    }

    [Fact]
    public void MapsterMap_DinnerResult_ToModel()
    {
        // Arrange
        var entity = Stubs.Dinners(DateTime.UtcNow.Ticks).First()
            .SetStatus(DinnerStatus.InProgress);
        var source = new Faker<Result<Dinner>>()
            .RuleFor(u => u.IsSuccess, true)
            .RuleFor(u => u.Messages, f => f.Lorem.Sentences().Split('\n'))
            .RuleFor(u => u.Value, entity).Generate();

        // Act
        var target = this.sut.Map<Dinner, DinnerResponseModel>(source.Value);

        // Assert
        target.ShouldNotBeNull();
        target.Name.ShouldBe(source.Value.Name);
        target.Description.ShouldBe(source.Value.Description);
        target.HostId.ShouldBe(source.Value.HostId.ToString());
        target.MenuId.ShouldBe(source.Value.MenuId.ToString());
        target.MaxGuests.ShouldBe(source.Value.MaxGuests);
        target.Status.ShouldBe(DinnerStatus.InProgress.ToString());
        target.Price.Amount.ShouldBe(source.Value.Price.Amount);
        target.Price.Currency.ShouldBe(source.Value.Price.Currency);
        target.Location.Name.ShouldBe(source.Value.Location.Name);
        target.Location.AddressLine1.ShouldBe(source.Value.Location.AddressLine1);
        target.Location.AddressLine2.ShouldBe(source.Value.Location.AddressLine2);
        target.Location.PostalCode.ShouldBe(source.Value.Location.PostalCode);
        target.Location.City.ShouldBe(source.Value.Location.City);
        target.Location.Country.ShouldBe(source.Value.Location.Country);
    }

    [Fact]
    public void MapsterMap_DinnersResult_ToModel()
    {
        // Arrange
        var entities = Stubs.Dinners(DateTime.UtcNow.Ticks).ForEach(e =>
            e.SetStatus(DinnerStatus.InProgress));
        var source = new Faker<Result<IEnumerable<Dinner>>>()
            .RuleFor(u => u.IsSuccess, true)
            .RuleFor(u => u.Messages, f => f.Lorem.Sentences().Split('\n'))
            .RuleFor(u => u.Value, new[] { entities.First(), entities.Last() }).Generate();

        // Act
        var target = this.sut.Map<IEnumerable<Dinner>, IEnumerable<DinnerResponseModel>>(source.Value);

        // Assert
        target.ShouldNotBeNull();
        target.First().Name.ShouldBe(source.Value.First().Name);
        target.First().Description.ShouldBe(source.Value.First().Description);
        target.First().HostId.ShouldBe(source.Value.First().HostId);
        target.First().MenuId.ShouldBe(source.Value.First().MenuId);
        target.First().MaxGuests.ShouldBe(source.Value.First().MaxGuests);
        target.First().Status.ShouldBe(DinnerStatus.InProgress.ToString());
        target.First().Price.Amount.ShouldBe(source.Value.First().Price.Amount);
        target.First().Price.Currency.ShouldBe(source.Value.First().Price.Currency);
        target.First().Location.Name.ShouldBe(source.Value.First().Location.Name);
        target.First().Location.AddressLine1.ShouldBe(source.Value.First().Location.AddressLine1);
        target.First().Location.AddressLine2.ShouldBe(source.Value.First().Location.AddressLine2);
        target.First().Location.PostalCode.ShouldBe(source.Value.First().Location.PostalCode);
        target.First().Location.City.ShouldBe(source.Value.First().Location.City);
        target.First().Location.Country.ShouldBe(source.Value.First().Location.Country);
        target.First().Schedule.StartDateTime.ShouldBe(source.Value.First().Schedule.StartDateTime);
        target.First().Schedule.EndDateTime.ShouldBe(source.Value.First().Schedule.EndDateTime);
    }

    [Fact]
    public void MapsterMap_DinnerCreateRequestModel_ToCommand()
    {
        // Arrange
        var entity = Stubs.Dinners(DateTime.UtcNow.Ticks).First();
        var source = new Faker<DinnerCreateRequestModel>()
            .RuleFor(u => u.Name, entity.Name)
            .RuleFor(u => u.Description, entity.Description)
            .RuleFor(u => u.HostId, entity.HostId)
            .RuleFor(u => u.MenuId, entity.MenuId)
            .RuleFor(u => u.MaxGuests, entity.MaxGuests)
            .RuleFor(u => u.Price, new PriceModel { Amount = entity.Price.Amount, Currency = entity.Price.Currency })
            .RuleFor(u => u.Location, new DinnerLocationModel { Name = entity.Location.Name, AddressLine1 = entity.Location.AddressLine1, AddressLine2 = entity.Location.AddressLine2, PostalCode = entity.Location.PostalCode, City = entity.Location.City, Country = entity.Location.Country })
            .RuleFor(u => u.Schedule, new DinnerScheduleModel { StartDateTime = entity.Schedule.StartDateTime, EndDateTime = entity.Schedule.EndDateTime }).Generate();

        // Act
        var target = this.sut.Map<DinnerCreateRequestModel, DinnerCreateCommand>(source);

        // Assert
        target.ShouldNotBeNull();
        target.Name.ShouldBe(source.Name);
        target.Description.ShouldBe(source.Description);
        target.HostId.ShouldBe(source.HostId);
        target.MenuId.ShouldBe(source.MenuId);
        target.MaxGuests.ShouldBe(source.MaxGuests);
        target.Price.Amount.ShouldBe(source.Price.Amount.To<decimal>());
        target.Price.Currency.ShouldBe(source.Price.Currency);
        target.Location.Name.ShouldBe(source.Location.Name);
        target.Location.AddressLine1.ShouldBe(source.Location.AddressLine1);
        target.Location.AddressLine2.ShouldBe(source.Location.AddressLine2);
        target.Location.PostalCode.ShouldBe(source.Location.PostalCode);
        target.Location.City.ShouldBe(source.Location.City);
        target.Location.Country.ShouldBe(source.Location.Country);
        target.Schedule.StartDateTime.ShouldBe(source.Schedule.StartDateTime);
        target.Schedule.EndDateTime.ShouldBe(source.Schedule.EndDateTime);
    }

    // Menu ==============================================================================
    [Fact]
    public void MapsterMap_MenuResult_ToModel()
    {
        // Arrange
        var entity = Stubs.Menus(DateTime.UtcNow.Ticks).First()
            .AddRating(Rating.Create(3));
        var source = new Faker<Result<Menu>>()
            .RuleFor(u => u.IsSuccess, true)
            .RuleFor(u => u.Messages, f => f.Lorem.Sentences().Split('\n'))
            .RuleFor(u => u.Value, entity).Generate();

        // Act
        var target = this.sut.Map<Menu, MenuResponseModel>(source.Value);

        // Assert
        target.ShouldNotBeNull();
        target.Name.ShouldBe(source.Value.Name);
        target.Description.ShouldBe(source.Value.Description);
        target.HostId.ShouldBe(source.Value.HostId.ToString());
        target.AverageRating.ShouldBe(source.Value.AverageRating.Value.Value);
    }

    [Fact]
    public void MapsterMap_MenuCreateRequestModel_ToCommand()
    {
        // Arrange
        var source = new Faker<MenuCreateRequestModel>()
            .RuleFor(u => u.Name, f => f.Lorem.Sentence(3))
            .RuleFor(u => u.Description, f => f.Lorem.Sentence(10))
            .RuleFor(u => u.HostId, f => f.Random.Guid().ToString())
            .Generate();

        // Act
        var target = this.sut.Map<MenuCreateRequestModel, MenuCreateCommand>(source);

        // Assert
        target.ShouldNotBeNull();
        target.Name.ShouldBe(source.Name);
        target.Description.ShouldBe(source.Description);
        target.HostId.ShouldBe(source.HostId);
    }
}