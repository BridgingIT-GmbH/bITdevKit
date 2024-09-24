// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.UnitTests.Presentation;

using Domain;
using Mapster;
using Marketing.Presentation;
using Marketing.Presentation.Web.Controllers;
using MapsterMapper = Common.MapsterMapper;

public class MarketingMapperRegisterTests
{
    private readonly IMapper sut;

    public MarketingMapperRegisterTests()
    {
        var config = new TypeAdapterConfig();
        config.Scan(typeof(MarketingMapperRegister).Assembly);

        this.sut = new MapsterMapper(config);
    }

    [Fact]
    public void GetTypes_WhichImplementRegister_AreFound()
    {
        // Arrange
        // Act
        var result = typeof(MarketingMapperRegister).Assembly.SafeGetTypes(typeof(IRegister));

        // Assert
        result.ShouldNotBeNull();
        result.ShouldContain(typeof(MarketingMapperRegister));
    }

    [Fact]
    public void MapsterMap_CustomerResult_ToModel()
    {
        // Arrange
        var entity = Stubs.Customers(DateTime.UtcNow.Ticks).First();
        var source = new Faker<Result<Customer>>()
            .RuleFor(u => u.IsSuccess, true)
            .RuleFor(u => u.Messages, f => f.Lorem.Sentences().Split('\n'))
            .RuleFor(u => u.Value, entity)
            .Generate();

        // Act
        var target = this.sut.Map<Result<Customer>, ResultOfCustomerResponseModel>(source);

        // Assert
        target.ShouldNotBeNull();
        target.IsSuccess.ShouldBeTrue();
        target.Messages.ShouldBe(source.Messages);
        target.Value.FirstName.ShouldBe(source.Value.FirstName);
        target.Value.LastName.ShouldBe(source.Value.LastName);
        target.Value.Email.ShouldNotBeNull();
        target.Value.Email.ShouldBe(source.Value.Email.Value);
    }

    [Fact]
    public void MapsterMap_Customers_ToModel()
    {
        // Arrange
        var source = Stubs.Customers(DateTime.UtcNow.Ticks);

        // Act
        var target = this.sut.Map<IEnumerable<Customer>, IEnumerable<CustomerResponseModel>>(source);

        // Assert
        target.ShouldNotBeNull();
        target.First().FirstName.ShouldBe(source.First().FirstName);
        target.First().LastName.ShouldBe(source.First().LastName);
        target.First().Email.ShouldNotBeNull();
        target.First().Email.ShouldBe(source.First().Email.Value);
    }

    [Fact]
    public void MapsterMap_CustomersResult_ToModel()
    {
        // Arrange
        var entities = Stubs.Customers(DateTime.UtcNow.Ticks);
        var source = new Faker<Result<IEnumerable<Customer>>>()
            .RuleFor(u => u.IsSuccess, true)
            .RuleFor(u => u.Messages, f => f.Lorem.Sentences().Split('\n'))
            .RuleFor(u => u.Value, new[] { entities.First(), entities.Last() })
            .Generate();

        // Act
        var target = this.sut.Map<Result<IEnumerable<Customer>>, ResultOfCustomersResponseModel>(source);

        // Assert
        target.ShouldNotBeNull();
        target.IsSuccess.ShouldBeTrue();
        target.Messages.ShouldBe(source.Messages);
        target.Value.First().FirstName.ShouldBe(source.Value.First().FirstName);
        target.Value.First().LastName.ShouldBe(source.Value.First().LastName);
        target.Value.First().Email.ShouldNotBeNull();
        target.Value.First().Email.ShouldBe(source.Value.First().Email.Value);
    }

    //[Fact]
    //public void MapsterMap_CustomerCreateRequestModel_ToCommand()
    //{
    //    // Arrange
    //    var entity = Stubs.Customers(DateTime.UtcNow.Ticks).First();
    //    var source = new Faker<CustomerCreateRequestModel>()
    //        .RuleFor(u => u.Name, entity.Name)
    //        .RuleFor(u => u.Description, entity.Description)
    //        .RuleFor(u => u.HostId, entity.HostId)
    //        .RuleFor(u => u.MenuId, entity.MenuId)
    //        .RuleFor(u => u.MaxGuests, entity.MaxGuests)
    //        .RuleFor(u => u.Price, new PriceModel { Amount = entity.Price.Amount, Currency = entity.Price.Currency })
    //        .RuleFor(u => u.Location, new DinnerLocationModel { Name = entity.Location.Name, AddressLine1 = entity.Location.AddressLine1, AddressLine2 = entity.Location.AddressLine2, PostalCode = entity.Location.PostalCode, City = entity.Location.City, Country = entity.Location.Country })
    //        .RuleFor(u => u.Schedule, new DinnerScheduleModel { StartDateTime = entity.Schedule.StartDateTime, EndDateTime = entity.Schedule.EndDateTime }).Generate();

    //    // Act
    //    var target = this.sut.Map<DinnerCreateRequestModel, DinnerCreateCommand>(source);

    //    // Assert
    //    target.ShouldNotBeNull();
    //    target.Name.ShouldBe(source.Name);
    //    target.Description.ShouldBe(source.Description);
    //    target.HostId.ShouldBe(source.HostId);
    //    target.MenuId.ShouldBe(source.MenuId);
    //    target.MaxGuests.ShouldBe(source.MaxGuests);
    //    target.Price.Amount.ShouldBe(source.Price.Amount.To<decimal>());
    //    target.Price.Currency.ShouldBe(source.Price.Currency);
    //    target.Location.Name.ShouldBe(source.Location.Name);
    //    target.Location.AddressLine1.ShouldBe(source.Location.AddressLine1);
    //    target.Location.AddressLine2.ShouldBe(source.Location.AddressLine2);
    //    target.Location.PostalCode.ShouldBe(source.Location.PostalCode);
    //    target.Location.City.ShouldBe(source.Location.City);
    //    target.Location.Country.ShouldBe(source.Location.Country);
    //    target.Schedule.StartDateTime.ShouldBe(source.Schedule.StartDateTime);
    //    target.Schedule.EndDateTime.ShouldBe(source.Schedule.EndDateTime);
    //}
}