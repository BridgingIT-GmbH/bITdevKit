// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Infrastructure.IntegrationTests;

using DevKit.Domain.Repositories;
using BridgingIT.DevKit.Domain;
using Domain.Model;
using Microsoft.Extensions.DependencyInjection;

//[Collection(nameof(PresentationCollection))] // https://xunit.net/docs/shared-context#collection-fixture
[IntegrationTest("WeatherForecast.Infrastructure")]
[Module("Core")]
public class
    UserAccountRepositoryTests
    : IClassFixture<CustomWebApplicationFactoryFixture<Program>> // https://xunit.net/docs/shared-context#class-fixture
{
    private readonly CustomWebApplicationFactoryFixture<Program> fixture;
    private readonly IGenericRepository<UserAccount> sut;

    public UserAccountRepositoryTests(ITestOutputHelper output, CustomWebApplicationFactoryFixture<Program> fixture)
    {
        this.fixture = fixture.WithOutput(output);
        this.sut = this.fixture.ServiceProvider.GetRequiredService<IGenericRepository<UserAccount>>();
    }

    [Fact]
    public async Task InsertTest()
    {
        for (var i = 0; i < 5; i++)
        {
            var ticks = DateTime.UtcNow.Ticks;
            var entity = new UserAccount
            {
                Email = $"John.Doe-{ticks}@test.com",
                VisitCount = 1,
                RegisterDate = DateTime.UtcNow.AddDays(-7),
                LastVisitDate = DateTime.UtcNow.AddHours(-6),
                AdAccount = AdAccount.Create($"domain\\john.doe.{ticks}")
            };

            var result = await this.sut.InsertAsync(entity).AnyContext();

            result.ShouldNotBeNull();
            result.Id.ShouldNotBe(Guid.Empty);
        }
    }

    [Fact]
    public async Task UpsertTest()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new UserAccount
        {
            Email = $"John.Doe-{ticks}@test.com",
            VisitCount = 1,
            RegisterDate = DateTime.UtcNow.AddDays(-7),
            LastVisitDate = DateTime.UtcNow.AddHours(-6),
            AdAccount = AdAccount.Create($"domain\\john.doe.{ticks}")
        };

        await this.sut.InsertAsync(entity).AnyContext();
        entity.Id.ShouldNotBe(Guid.Empty);

        // Act
        using (var scope = this.fixture.Services.CreateScope())
        {
            var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<UserAccount>>();

            entity.Email = $"Mary.Jane-{ticks}@test.com";

            await scopedSut.UpsertAsync(entity).AnyContext();
        }

        // Assert
        using (var scope = this.fixture.Services.CreateScope())
        {
            var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<UserAccount>>();
            var result = await scopedSut.FindOneAsync(entity.Id).AnyContext();

            result.ShouldNotBeNull();
            result.Id.ShouldBe(entity.Id);
            result.Email.ShouldBe($"Mary.Jane-{ticks}@test.com");
        }
    }

    [Fact]
    public async Task ExistsTest()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new UserAccount
        {
            Email = $"John.Doe-{ticks}@test.com",
            VisitCount = 1,
            RegisterDate = DateTime.UtcNow.AddDays(-7),
            LastVisitDate = DateTime.UtcNow.AddHours(-6),
            AdAccount = AdAccount.Create($"domain\\john.doe.{ticks}")
        };

        await this.sut.InsertAsync(entity).AnyContext();
        entity.Id.ShouldNotBe(Guid.Empty);

        // Act & Assert
        using var scope = this.fixture.Services.CreateScope();
        var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<UserAccount>>();
        var result = await scopedSut.ExistsAsync(entity.Id).AnyContext();

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task FindOneByIdTest()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new UserAccount
        {
            Email = $"John.Doe-{ticks}@test.com",
            VisitCount = 1,
            RegisterDate = DateTime.UtcNow.AddDays(-7),
            LastVisitDate = DateTime.UtcNow.AddHours(-6),
            AdAccount = AdAccount.Create($"domain\\john.doe.{ticks}")
        };

        await this.sut.InsertAsync(entity).AnyContext();
        entity.Id.ShouldNotBe(Guid.Empty);

        // Assert
        using var scope = this.fixture.Services.CreateScope();
        var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<UserAccount>>();
        var result = await scopedSut.FindOneAsync(entity.Id).AnyContext();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(entity.Id);
    }

    [Fact]
    public async Task FindOneByIdAsStringTest()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new UserAccount
        {
            Email = $"John.Doe-{ticks}@test.com",
            VisitCount = 1,
            RegisterDate = DateTime.UtcNow.AddDays(-7),
            LastVisitDate = DateTime.UtcNow.AddHours(-6),
            AdAccount = AdAccount.Create($"domain\\john.doe.{ticks}")
        };

        await this.sut.InsertAsync(entity).AnyContext();
        entity.Id.ShouldNotBe(Guid.Empty);

        // Assert
        using var scope = this.fixture.Services.CreateScope();
        var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<UserAccount>>();
        var result = await scopedSut.FindOneAsync(entity.Id.ToString()).AnyContext();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(entity.Id);
    }

    [Fact]
    public async Task FindOneBySpecificationTest()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new UserAccount
        {
            Email = $"John.Doe-{ticks}@test.com",
            VisitCount = 1,
            RegisterDate = DateTime.UtcNow.AddDays(-7),
            LastVisitDate = DateTime.UtcNow.AddHours(-6),
            AdAccount = AdAccount.Create($"domain\\john.doe.{ticks}")
        };

        await this.sut.InsertAsync(entity).AnyContext();
        entity.Id.ShouldNotBe(Guid.Empty);

        // Assert
        using var scope = this.fixture.Services.CreateScope();
        var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<UserAccount>>();
        var result = await scopedSut.FindOneAsync(new Specification<UserAccount>(e => e.Email == entity.Email))
            .AnyContext();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(entity.Id);
    }

    [Fact]
    public async Task FindOneByIdSpecificationTest()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new UserAccount
        {
            Email = $"John.Doe-{ticks}@test.com",
            VisitCount = 1,
            RegisterDate = DateTime.UtcNow.AddDays(-7),
            LastVisitDate = DateTime.UtcNow.AddHours(-6),
            AdAccount = AdAccount.Create($"domain\\john.doe.{ticks}")
        };

        await this.sut.InsertAsync(entity).AnyContext();
        entity.Id.ShouldNotBe(Guid.Empty);

        // Assert
        using var scope = this.fixture.Services.CreateScope();
        var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<UserAccount>>();
        var result = await scopedSut.FindOneAsync(new Specification<UserAccount>(e => e.Id == entity.Id)).AnyContext();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(entity.Id);
    }

    [Fact]
    public async Task FindAllTest()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new UserAccount
        {
            Email = $"John.Doe-{ticks}@test.com",
            VisitCount = 1,
            RegisterDate = DateTime.UtcNow.AddDays(-7),
            LastVisitDate = DateTime.UtcNow.AddHours(-6),
            AdAccount = AdAccount.Create($"domain\\john.doe.{ticks}")
        };

        await this.sut.InsertAsync(entity).AnyContext();
        entity.Id.ShouldNotBe(Guid.Empty);

        // Assert
        using var scope = this.fixture.Services.CreateScope();
        var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<UserAccount>>();
        var result = await scopedSut.FindAllAsync().AnyContext();

        result.ShouldNotBeNull();
        result.Count().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task FindAllEmailAddressesWithSpecificRepositoryTest()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new UserAccount
        {
            Email = $"John.Doe-{ticks}@test.com",
            VisitCount = 1,
            RegisterDate = DateTime.UtcNow.AddDays(-7),
            LastVisitDate = DateTime.UtcNow.AddHours(-6),
            AdAccount = AdAccount.Create($"domain\\john.doe.{ticks}")
        };

        await this.sut.InsertAsync(entity).AnyContext();
        entity.Id.ShouldNotBe(Guid.Empty);

        // Assert
        using var scope = this.fixture.Services.CreateScope();
        var scopedSut = scope.ServiceProvider.GetRequiredService<UserAccountRepository>();
        var result = (await scopedSut.FindAllEmailAddresses()).ToList();

        result.ShouldNotBeNull();
        result.Count().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task FindAllBySpecificationTest()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new UserAccount
        {
            Email = $"John.Doe-{ticks}@test.com",
            VisitCount = 1,
            RegisterDate = DateTime.UtcNow.AddDays(-7),
            LastVisitDate = DateTime.UtcNow.AddHours(-6),
            AdAccount = AdAccount.Create($"domain\\john.doe.{ticks}")
        };

        await this.sut.InsertAsync(entity).AnyContext();
        entity.Id.ShouldNotBe(Guid.Empty);

        // Assert
        using var scope = this.fixture.Services.CreateScope();
        var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<UserAccount>>();
        var result = await scopedSut.FindAllAsync(new Specification<UserAccount>(e => e.VisitCount >= 1)).AnyContext();

        result.ShouldNotBeNull();
        result.Count().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task FindAllWithSkipTake()
    {
        for (var i = 0; i < 15; i++)
        {
            var ticks = DateTime.UtcNow.Ticks;
            var entity = new UserAccount
            {
                Email = $"John.Doe-{ticks}@test.com",
                VisitCount = 1,
                RegisterDate = DateTime.UtcNow.AddDays(-7),
                LastVisitDate = DateTime.UtcNow.AddHours(-6),
                AdAccount = AdAccount.Create($"domain\\john.doe.{ticks}")
            };

            await this.sut.InsertAsync(entity).AnyContext();
            entity.Id.ShouldNotBe(Guid.Empty);
        }

        // Assert
        using var scope = this.fixture.Services.CreateScope();
        var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<UserAccount>>();
        var result = await scopedSut.FindAllAsync(new FindOptions<UserAccount> { Skip = 5, Take = 5 }).AnyContext();

        result.ShouldNotBeNull();
        result.Count().ShouldBe(5);
    }

    [Fact]
    public async Task DeleteByEntityTest()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new UserAccount
        {
            Email = $"John.Doe-{ticks}@test.com",
            VisitCount = 1,
            RegisterDate = DateTime.UtcNow.AddDays(-7),
            LastVisitDate = DateTime.UtcNow.AddHours(-6),
            AdAccount = AdAccount.Create($"domain\\john.doe.{ticks}")
        };

        await this.sut.InsertAsync(entity).AnyContext();
        entity.Id.ShouldNotBe(Guid.Empty);

        var result = await this.sut.DeleteAsync(entity).AnyContext();

        result.ShouldBe(RepositoryActionResult.Deleted);
    }

    [Fact]
    public async Task DeleteByIdTest()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new UserAccount
        {
            Email = $"John.Doe-{ticks}@test.com",
            VisitCount = 1,
            RegisterDate = DateTime.UtcNow.AddDays(-7),
            LastVisitDate = DateTime.UtcNow.AddHours(-6),
            AdAccount = AdAccount.Create($"domain\\john.doe.{ticks}")
        };

        await this.sut.InsertAsync(entity).AnyContext();
        entity.Id.ShouldNotBe(Guid.Empty);

        var result = await this.sut.DeleteAsync(entity.Id).AnyContext();

        result.ShouldBe(RepositoryActionResult.Deleted);
    }

    [Fact]
    public async Task DeleteByIdAsStringTest()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new UserAccount
        {
            Email = $"John.Doe-{ticks}@test.com",
            VisitCount = 1,
            RegisterDate = DateTime.UtcNow.AddDays(-7),
            LastVisitDate = DateTime.UtcNow.AddHours(-6),
            AdAccount = AdAccount.Create($"domain\\john.doe.{ticks}")
        };

        await this.sut.InsertAsync(entity).AnyContext();
        entity.Id.ShouldNotBe(Guid.Empty);

        var result = await this.sut.DeleteAsync(entity.Id.ToString()).AnyContext();

        result.ShouldBe(RepositoryActionResult.Deleted);
    }
}