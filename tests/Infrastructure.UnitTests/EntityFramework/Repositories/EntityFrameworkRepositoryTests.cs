// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Repositories;

using System;
using System.Linq;
using System.Transactions;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using BridgingIT.DevKit.Infrastructure.Mapping;
using global::AutoMapper;

[UnitTest("Infrastructure")]
public class EntityFrameworkRepositoryTests : IClassFixture<TestDbContextFixture>
{
    private readonly TestDbContextFixture fixture;

    public EntityFrameworkRepositoryTests(TestDbContextFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task GenericRepositoryFindAllAsync()
    {
        using var scope = new TransactionScope();
        var mapper = CreateMapper();
        var options = Substitute.For<EntityFrameworkRepositoryOptions>();
        options.Mapper.Returns(mapper);
        options.DbContext.Returns(this.fixture.Context);
        var sut = new EntityFrameworkGenericRepository<PersonStub, PersonDtoStub>(options);
        var list = await sut.FindAllAsync().AnyContext();
        var datalist = list as PersonStub[] ?? list.ToArray();
        datalist.Length.ShouldBe(2);
        var first = datalist.FirstOrDefault();
        var second = datalist.LastOrDefault();
        first.ShouldNotBe(second);
    }

    [Fact]
    public async Task PersonRepositoryFindAllAsync()
    {
        using var scope = new TransactionScope();
        var mapper = CreateMapper();
        var sut = new PersonStubRepository(o => o.DbContext(this.fixture.Context).Mapper(mapper));
        var list = await sut.FindAllAsync().AnyContext();
        var datalist = list as PersonStub[] ?? list.ToArray();
        datalist.Length.ShouldBe(2);
        var first = datalist.FirstOrDefault();
        var second = datalist.LastOrDefault();
        first.ShouldNotBe(second);
    }

    [Fact]
    public async Task PersonRepositoryFindAllNoTrackingAsync()
    {
        using var scope = new TransactionScope();
        var mapper = CreateMapper();
        var sut = new PersonStubRepository(o => o.DbContext(this.fixture.Context).Mapper(mapper));
        var list = await sut.FindAllAsync(new FindOptions<PersonStub>() { NoTracking = true }).AnyContext();
        var datalist = list as PersonStub[] ?? list.ToArray();
        datalist.Length.ShouldBe(2);
        var first = datalist.FirstOrDefault();
        var second = datalist.LastOrDefault();
        first.ShouldNotBe(second);
    }

    [Fact]
    public async Task PersonRepositoryFindOne()
    {
        using var scope = new TransactionScope();
        var mapper = CreateMapper();
        var id = this.fixture.Context.Persons.First().Identifier;
        var sut = new PersonStubRepository(o => o.DbContext(this.fixture.Context).Mapper(mapper));
        var found = await sut.FindOneAsync(id).AnyContext();
        found.ShouldNotBeNull();
    }

    [Fact]
    public async Task PersonRepositoryFindAllLastnames()
    {
        using var scope = new TransactionScope();
        var mapper = CreateMapper();
        this.fixture.Context.Persons.Count().ShouldBe(2);
        var sut = new PersonStubRepository(o => o.DbContext(this.fixture.Context).Mapper(mapper));
        var found = await sut.FindAllLastNames().AnyContext(); // uses dapper to query the db (QueryAsync)
        found.ShouldNotBeNull();
    }

    [Fact]
    public void TestAutomapperConfigurationPerson()
    {
        var sut = CreateMapper();
        var person = CreatePerson();
        var personDto = sut.Map<PersonStub>(person);
        personDto.Id.ShouldBe(person.Id);
        personDto.FirstName.ShouldBe(person.FirstName);
        personDto.LastName.ShouldBe(person.LastName);
    }

    [Fact]
    public void TestAutomapperConfigurationPersonDto()
    {
        var sut = CreateMapper();
        var dto = CreateMalePersonEntity();
        var person = sut.Map<PersonStub>(dto);
        person.Id.ShouldBe(dto.Id);
        person.FirstName.ShouldBe(dto.FirstName);
        person.LastName.ShouldBe(dto.LastName);
    }

    private static PersonStub CreateMalePersonEntity()
    {
        return new PersonStub() { Id = Guid.NewGuid(), FirstName = "John", LastName = "Doe" };
    }

    private static PersonStub CreatePerson()
    {
        return new PersonStub { Id = Guid.NewGuid(), FirstName = "Jane", LastName = "Doe" };
    }

    private static IEntityMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg =>
            cfg.AddProfile(new MapperProfile()));

        return new AutoMapperEntityMapper(config.CreateMapper());
    }
}