// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Infrastructure.IntegrationTests;

using DevKit.Domain.Repositories;
using BridgingIT.DevKit.Domain;
using Domain.Model;

//[Collection(nameof(PresentationCollection))] // https://xunit.net/docs/shared-context#collection-fixture
[IntegrationTest("WeatherForecast.Infrastructure")]
[Module("Core")]
public class
    TestGuidEntityRepositoryTests
    : IClassFixture<CustomWebApplicationFactoryFixture<Program>> // https://xunit.net/docs/shared-context#class-fixture
{
    private readonly CustomWebApplicationFactoryFixture<Program> fixture;
    private readonly IGenericRepository<TestGuidEntity> sut;

    public TestGuidEntityRepositoryTests(ITestOutputHelper output, CustomWebApplicationFactoryFixture<Program> fixture)
    {
        this.fixture = fixture.WithOutput(output);
        this.sut = this.fixture.ServiceProvider.GetRequiredService<IGenericRepository<TestGuidEntity>>();
    }

    [Fact]
    public async Task InsertTest()
    {
        for (var i = 0; i < 5; i++)
        {
            var ticks = DateTime.UtcNow.Ticks;
            var entity = new TestGuidEntity
            {
                MyProperty1 = "John " + ticks,
                MyProperty2 = "Doe " + ticks,
                MyProperty3 = i,
                Children = new List<TestGuidChildEntity>(new[]
                {
                    new TestGuidChildEntity { MyProperty1 = "val a " + ticks, MyProperty2 = "val a " + ticks },
                    new TestGuidChildEntity { MyProperty1 = "val b " + ticks, MyProperty2 = "val b " + ticks },
                    new TestGuidChildEntity { MyProperty1 = "val c " + ticks, MyProperty2 = "val c " + ticks }
                })
            };

            entity.AuditState.SetCreated("test");
            await this.sut.InsertAsync(entity).AnyContext();

            entity.Id.ShouldNotBe(Guid.Empty);

            using var scope = this.fixture.Services.CreateScope();
            var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<TestGuidEntity>>();
            var result = await scopedSut.FindOneAsync(entity.Id,
                    new FindOptions<TestGuidEntity> { Include = new IncludeOption<TestGuidEntity>(e => e.Children) })
                .AnyContext();

            result.ShouldNotBeNull();
            result.Id.ShouldBe(entity.Id);
            result.MyProperty1.ShouldBe("John " + ticks);
            result.MyProperty2.ShouldBe("Doe " + ticks);
            result.Children.ShouldNotBeNull();
            result.Children.Count().ShouldBe(3);
            result.Children.Count().ShouldBe(entity.Children.Count());
            result.Children.First().MyProperty1.ShouldStartWith("val a");
            result.Children.First().MyProperty2.ShouldStartWith("val a");
            result.Children.Last().MyProperty1.ShouldStartWith("val c");
            result.Children.Last().MyProperty2.ShouldStartWith("val c");
        }
    }

    [Fact]
    public async Task UpsertTest()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new TestGuidEntity
        {
            MyProperty1 = "John " + ticks,
            MyProperty2 = "Doe " + ticks,
            MyProperty3 = 0,
            Children = new List<TestGuidChildEntity>(new[]
            {
                new TestGuidChildEntity { MyProperty1 = "val " + ticks, MyProperty2 = "val " + ticks }
            })
        };

        entity.AuditState.SetCreated("test");
        await this.sut.InsertAsync(entity).AnyContext();

        // Act
        using (var scope = this.fixture.Services.CreateScope())
        {
            var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<TestGuidEntity>>();
            entity.MyProperty1 = "Mary " + ticks;
            entity.MyProperty2 = "Jane " + ticks;
            entity.Children.Add(new TestGuidChildEntity
            {
                MyProperty1 = "val new " + ticks, MyProperty2 = "val new " + ticks
            });

            await scopedSut.UpsertAsync(entity).AnyContext();
        }

        // Assert
        using (var scope = this.fixture.Services.CreateScope())
        {
            var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<TestGuidEntity>>();
            var result = await scopedSut.FindOneAsync(entity.Id,
                    new FindOptions<TestGuidEntity> { Include = new IncludeOption<TestGuidEntity>(e => e.Children) })
                .AnyContext();

            result.ShouldNotBeNull();
            result.Id.ShouldBe(entity.Id);
            result.MyProperty1.ShouldBe("Mary " + ticks);
            result.MyProperty2.ShouldBe("Jane " + ticks);
            result.Children.ShouldNotBeNull();
            result.Children.Count().ShouldBe(2);
            result.Children.Count().ShouldBe(entity.Children.Count());
            result.Children.Last().MyProperty1.ShouldStartWith("val new");
            result.Children.Last().MyProperty2.ShouldStartWith("val new");
        }
    }

    [Fact]
    public async Task UpsertChildDeleteTest()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new TestGuidEntity
        {
            MyProperty1 = "John " + ticks,
            MyProperty2 = "Doe " + ticks,
            MyProperty3 = 0,
            Children = new List<TestGuidChildEntity>(new[]
            {
                new TestGuidChildEntity { MyProperty1 = "val " + ticks, MyProperty2 = "val " + ticks },
                new TestGuidChildEntity { MyProperty1 = "val " + ticks, MyProperty2 = "val " + ticks },
                new TestGuidChildEntity { MyProperty1 = "val " + ticks, MyProperty2 = "val " + ticks }
            })
        };

        entity.AuditState.SetCreated("test");
        await this.sut.InsertAsync(entity).AnyContext();

        // Act
        entity.MyProperty1 = "Mary " + ticks;
        entity.MyProperty2 = "Jane " + ticks;
        entity.Children.Remove(entity.Children.First());
        entity.Children.Remove(entity.Children.First());

        await this.sut.UpsertAsync(entity).AnyContext();

        // Assert
        using var scope = this.fixture.Services.CreateScope();
        var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<TestGuidEntity>>();
        var result = await scopedSut.FindOneAsync(entity.Id,
                new FindOptions<TestGuidEntity> { Include = new IncludeOption<TestGuidEntity>(e => e.Children) })
            .AnyContext();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(entity.Id);
        result.MyProperty1.ShouldBe("Mary " + ticks);
        result.MyProperty2.ShouldBe("Jane " + ticks);
        result.Children.ShouldNotBeNull();
        result.Children.Count().ShouldBe(1);
        result.Children.Count().ShouldBe(entity.Children.Count());
    }

    [Fact]
    public async Task UpsertDisconnectedTest()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new TestGuidEntity
        {
            MyProperty1 = "John " + ticks,
            MyProperty2 = "Doe " + ticks,
            MyProperty3 = 100,
            Children = new List<TestGuidChildEntity>(new[]
            {
                new TestGuidChildEntity { MyProperty1 = "val " + ticks, MyProperty2 = "val " + ticks },
                new TestGuidChildEntity { MyProperty1 = "val " + ticks, MyProperty2 = "val " + ticks },
                new TestGuidChildEntity { MyProperty1 = "val " + ticks, MyProperty2 = "val " + ticks }
            })
        };

        entity.AuditState.SetCreated("test");
        await this.sut.InsertAsync(entity).AnyContext();

        // Act
        using (var scope = this.fixture.Services.CreateScope())
        {
            var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<TestGuidEntity>>();
            var disconnectedEntity = new TestGuidEntity
            {
                Id = entity.Id, // has same id as entity > should update
                MyProperty1 = "Mary " + ticks,
                MyProperty2 = "Jane " + ticks,
                Children = new List<TestGuidChildEntity>(new[]
                {
                    new TestGuidChildEntity
                    {
                        Id = entity.Children.First().Id,
                        MyProperty1 = "val new " + ticks,
                        MyProperty2 = "val new " + ticks
                    }, // updated, because id present
                    new TestGuidChildEntity
                    {
                        MyProperty1 = "val " + ticks, MyProperty2 = "val " + ticks
                    } // added because no id present
                })
            };

            await scopedSut.UpsertAsync(disconnectedEntity).AnyContext();
        }

        // Assert
        using (var scope = this.fixture.Services.CreateScope())
        {
            var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<TestGuidEntity>>();
            var result = await scopedSut.FindOneAsync(entity.Id,
                    new FindOptions<TestGuidEntity> { Include = new IncludeOption<TestGuidEntity>(e => e.Children) })
                .AnyContext();

            result.ShouldNotBeNull();
            result.Id.ShouldBe(entity.Id);
            result.MyProperty1.ShouldBe("Mary " + ticks);
            result.MyProperty2.ShouldBe("Jane " + ticks);
            result.MyProperty3.ShouldBe(0); // changed as not specified in disconnectedEntity (defaults to 0)
            result.Children.ShouldNotBeNull();
            result.Children.Count().ShouldBe(4);
            result.Children.First().MyProperty1.ShouldStartWith("val new");
            result.Children.First().MyProperty2.ShouldStartWith("val new");
        }
    }

    [Fact]
    public async Task ExistsTest()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new TestGuidEntity
        {
            MyProperty1 = "John " + ticks,
            MyProperty2 = "Doe " + ticks,
            MyProperty3 = 0,
            Children = new List<TestGuidChildEntity>(new[]
            {
                new TestGuidChildEntity { MyProperty1 = "val " + ticks, MyProperty2 = "val " + ticks }
            })
        };

        entity.AuditState.SetCreated("test");
        await this.sut.InsertAsync(entity).AnyContext();

        // Act & Assert
        using var scope = this.fixture.Services.CreateScope();
        var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<TestGuidEntity>>();
        var result = await scopedSut.ExistsAsync(entity.Id).AnyContext();

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task FindOneByIdTest()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new TestGuidEntity
        {
            MyProperty1 = "John " + ticks,
            MyProperty2 = "Doe " + ticks,
            MyProperty3 = 0,
            Children = new List<TestGuidChildEntity>(new[]
            {
                new TestGuidChildEntity { MyProperty1 = "val " + ticks, MyProperty2 = "val " + ticks }
            })
        };

        entity.AuditState.SetCreated("test");
        await this.sut.InsertAsync(entity).AnyContext();

        // Assert
        using var scope = this.fixture.Services.CreateScope();
        var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<TestGuidEntity>>();
        var result = await scopedSut.FindOneAsync(entity.Id,
                new FindOptions<TestGuidEntity> { Include = new IncludeOption<TestGuidEntity>(e => e.Children) })
            .AnyContext();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(entity.Id);
        result.Children.ShouldNotBeNull();
        result.Children.Count.ShouldBe(1);
        result.Children.First().Id.ShouldBe(entity.Children.First().Id);
    }

    [Fact]
    public async Task FindOneByIdAsStringTest()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new TestGuidEntity
        {
            MyProperty1 = "John " + ticks,
            MyProperty2 = "Doe " + ticks,
            MyProperty3 = 0,
            Children = new List<TestGuidChildEntity>(new[]
            {
                new TestGuidChildEntity { MyProperty1 = "val " + ticks, MyProperty2 = "val " + ticks }
            })
        };

        entity.AuditState.SetCreated("test");
        await this.sut.InsertAsync(entity).AnyContext();

        // Assert
        using var scope = this.fixture.Services.CreateScope();
        var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<TestGuidEntity>>();
        var result = await scopedSut.FindOneAsync(entity.Id.ToString(),
                new FindOptions<TestGuidEntity> { Include = new IncludeOption<TestGuidEntity>(e => e.Children) })
            .AnyContext();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(entity.Id);
        result.Children.ShouldNotBeNull();
        result.Children.Count.ShouldBe(1);
        result.Children.First().Id.ShouldBe(entity.Children.First().Id);
    }

    [Fact]
    public async Task FindOneBySpecificationTest()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new TestGuidEntity
        {
            MyProperty1 = "John " + ticks,
            MyProperty2 = "Doe " + ticks,
            MyProperty3 = 0,
            Children = new List<TestGuidChildEntity>(new[]
            {
                new TestGuidChildEntity { MyProperty1 = "val " + ticks, MyProperty2 = "val " + ticks }
            })
        };

        entity.AuditState.SetCreated("test");
        await this.sut.InsertAsync(entity).AnyContext();

        // Assert
        using var scope = this.fixture.Services.CreateScope();
        var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<TestGuidEntity>>();
        var result = await scopedSut.FindOneAsync(
                new Specification<TestGuidEntity>(e => e.MyProperty1 == entity.MyProperty1),
                new FindOptions<TestGuidEntity> { Include = new IncludeOption<TestGuidEntity>(e => e.Children) })
            .AnyContext();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(entity.Id);
        result.Children.ShouldNotBeNull();
        result.Children.Count.ShouldBe(1);
        result.Children.First().Id.ShouldBe(entity.Children.First().Id);
    }

    [Fact]
    public async Task FindOneByIdSpecificationTest()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new TestGuidEntity
        {
            MyProperty1 = "John " + ticks,
            MyProperty2 = "Doe " + ticks,
            MyProperty3 = 0,
            Children = new List<TestGuidChildEntity>(new[]
            {
                new TestGuidChildEntity { MyProperty1 = "val " + ticks, MyProperty2 = "val " + ticks }
            })
        };

        entity.AuditState.SetCreated("test");
        await this.sut.InsertAsync(entity).AnyContext();

        // Assert
        using var scope = this.fixture.Services.CreateScope();
        var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<TestGuidEntity>>();
        var result = await scopedSut.FindOneAsync(new Specification<TestGuidEntity>(e => e.Id == entity.Id),
                new FindOptions<TestGuidEntity> { Include = new IncludeOption<TestGuidEntity>(e => e.Children) })
            .AnyContext();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(entity.Id);
        result.Children.ShouldNotBeNull();
        result.Children.Count.ShouldBe(1);
        result.Children.First().Id.ShouldBe(entity.Children.First().Id);
    }

    [Fact]
    public async Task FindAllTest()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new TestGuidEntity
        {
            MyProperty1 = "John " + ticks,
            MyProperty2 = "Doe " + ticks,
            MyProperty3 = 0,
            Children = new List<TestGuidChildEntity>(new[]
            {
                new TestGuidChildEntity { MyProperty1 = "val " + ticks, MyProperty2 = "val " + ticks }
            })
        };

        entity.AuditState.SetCreated("test");
        await this.sut.InsertAsync(entity).AnyContext();

        // Assert
        using var scope = this.fixture.Services.CreateScope();
        var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<TestGuidEntity>>();
        var result = await scopedSut.FindAllAsync(
                new FindOptions<TestGuidEntity> { Include = new IncludeOption<TestGuidEntity>(e => e.Children) })
            .AnyContext();

        result.ShouldNotBeNull();
        result.Count().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task FindAllBySpecificationTest()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new TestGuidEntity
        {
            MyProperty1 = "John " + ticks,
            MyProperty2 = "Doe " + ticks,
            MyProperty3 = 0,
            Children = new List<TestGuidChildEntity>(new[]
            {
                new TestGuidChildEntity { MyProperty1 = "val " + ticks, MyProperty2 = "val " + ticks }
            })
        };

        entity.AuditState.SetCreated("test");
        await this.sut.InsertAsync(entity).AnyContext();

        // Assert
        using var scope = this.fixture.Services.CreateScope();
        var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<TestGuidEntity>>();
        var result = await scopedSut.FindAllAsync(new Specification<TestGuidEntity>(e => e.MyProperty3 == 0),
                new FindOptions<TestGuidEntity> { Include = new IncludeOption<TestGuidEntity>(e => e.Children) })
            .AnyContext();

        result.ShouldNotBeNull();
        result.Count().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task FindAllWithSkipTake()
    {
        for (var i = 0; i < 15; i++)
        {
            var ticks = DateTime.UtcNow.Ticks;
            var entity = new TestGuidEntity
            {
                MyProperty1 = "John " + ticks,
                MyProperty2 = "Doe " + ticks,
                MyProperty3 = i,
                Children = new List<TestGuidChildEntity>(new[]
                {
                    new TestGuidChildEntity { MyProperty1 = "val " + ticks, MyProperty2 = "val " + ticks }
                })
            };

            entity.AuditState.SetCreated("test");
            await this.sut.InsertAsync(entity).AnyContext();
        }

        // Assert
        using var scope = this.fixture.Services.CreateScope();
        var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<TestGuidEntity>>();
        var result = await scopedSut.FindAllAsync(new FindOptions<TestGuidEntity>
            {
                Skip = 5, Take = 5, Include = new IncludeOption<TestGuidEntity>(e => e.Children)
            })
            .AnyContext();

        result.ShouldNotBeNull();
        result.Count().ShouldBe(5);
    }

    [Fact]
    public async Task DeleteByEntityTest()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new TestGuidEntity
        {
            MyProperty1 = "John " + ticks,
            MyProperty2 = "Doe " + ticks,
            MyProperty3 = 0,
            Children = new List<TestGuidChildEntity>(new[]
            {
                new TestGuidChildEntity { MyProperty1 = "val " + ticks, MyProperty2 = "val " + ticks }
            })
        };

        entity.AuditState.SetCreated("test");
        await this.sut.InsertAsync(entity).AnyContext();

        var result = await this.sut.DeleteAsync(entity).AnyContext();

        result.ShouldBe(RepositoryActionResult.Deleted);
    }

    [Fact]
    public async Task DeleteByIdTest()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new TestGuidEntity
        {
            MyProperty1 = "John " + ticks,
            MyProperty2 = "Doe " + ticks,
            MyProperty3 = 0,
            Children = new List<TestGuidChildEntity>(new[]
            {
                new TestGuidChildEntity { MyProperty1 = "val " + ticks, MyProperty2 = "val " + ticks }
            })
        };

        entity.AuditState.SetCreated("test");
        await this.sut.InsertAsync(entity).AnyContext();

        var result = await this.sut.DeleteAsync(entity.Id).AnyContext();

        result.ShouldBe(RepositoryActionResult.Deleted);
    }

    [Fact]
    public async Task DeleteByIdAsStringTest()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new TestGuidEntity
        {
            MyProperty1 = "John " + ticks,
            MyProperty2 = "Doe " + ticks,
            MyProperty3 = 0,
            Children = new List<TestGuidChildEntity>(new[]
            {
                new TestGuidChildEntity { MyProperty1 = "val " + ticks, MyProperty2 = "val " + ticks }
            })
        };

        entity.AuditState.SetCreated("test");
        await this.sut.InsertAsync(entity).AnyContext();

        var result = await this.sut.DeleteAsync(entity.Id.ToString()).AnyContext();

        result.ShouldBe(RepositoryActionResult.Deleted);
    }

    [Fact]
    public async Task UpsertTrackedTest()
    {
        // Arrange: get an existing entity
        var entities = await this.sut.FindAllAsync().AnyContext(); // =tracked
        var entity = entities.FirstOrDefault();
        entity.ShouldNotBeNull();

        // Act: update some properties
        var ticks = DateTime.UtcNow.Ticks;
        entity.MyProperty1 = "Mary " + ticks;
        entity.MyProperty2 = "Jane " + ticks;
        entity.AuditState.SetUpdated("test");
        await this.sut.UpsertAsync(entity);

        // Assert
        using var scope = this.fixture.Services.CreateScope();
        var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<TestGuidEntity>>();
        var entity2 = await scopedSut.FindOneAsync(entity.Id).AnyContext();
        entity2.ShouldNotBeNull();
        entity2.Id.ShouldBe(entity.Id);
        entity2.MyProperty1.ShouldBe("Mary " + ticks);
        entity2.MyProperty2.ShouldBe("Jane " + ticks);
    }

    [Fact]
    public async Task UpsertNotTrackedTest()
    {
        // Arrange: get an existing entity
        var entities =
            await this.sut.FindAllAsync(new FindOptions<TestGuidEntity> { NoTracking = true })
                .AnyContext(); // =not tracked
        var entity = entities.FirstOrDefault();
        entity.ShouldNotBeNull();

        // Act: update some properties
        var ticks = DateTime.UtcNow.Ticks;
        entity.MyProperty1 = "Mary " + ticks;
        entity.MyProperty2 = "Jane " + ticks;
        entity.AuditState.SetUpdated("test");
        await this.sut.UpsertAsync(entity);

        // asert
        using var scope = this.fixture.Services.CreateScope();
        var scopedSut = scope.ServiceProvider.GetRequiredService<IGenericRepository<TestGuidEntity>>();
        var entity2 = await scopedSut.FindOneAsync(entity.Id).AnyContext();
        entity2.ShouldNotBeNull();
        entity2.Id.ShouldBe(entity.Id);
        entity2.MyProperty1.ShouldBe("Mary " + ticks);
        entity2.MyProperty2.ShouldBe("Jane " + ticks);
    }
}