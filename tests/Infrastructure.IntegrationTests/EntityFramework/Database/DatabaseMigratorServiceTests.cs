// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

//namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

//using BridgingIT.DevKit.Infrastructure.EntityFramework;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFrameworkCore.Infrastructure;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Logging;

//public class DatabaseMigratorServiceTests
//{
//    private readonly ILoggerFactory loggerFactory;
//    private readonly IServiceProvider serviceProvider;

//    public DatabaseMigratorServiceTests()
//    {
//        this.loggerFactory = Substitute.For<ILoggerFactory>();
//        this.serviceProvider = Substitute.For<IServiceProvider>();
//    }

//    [Fact]
//    public async Task StartAsync_WhenNoPendingMigrations_ShouldNotMigrate()
//    {
//        // Arrange
//        var sut = new DatabaseMigratorService<MyContext>(null, this.serviceProvider, new DatabaseMigratorOptions { EnsureDeleted = false });
//        var context = new MyContext(); //Substitute.For<MyContext>();
//        var db = Substitute.For<DatabaseFacade>(context);
//        db.ProviderName.Returns("Provider");
//        db.GetPendingMigrationsAsync().ReturnsForAnyArgs(Task.FromResult(Array.Empty<string>().AsEnumerable()));
//        //context.Database.ProviderName.Returns("Provider");
//        context.Database.Returns(db);
//        //context.Database.GetPendingMigrationsAsync().Returns(Task.FromResult(Array.Empty<string>().AsEnumerable()));
//        this.serviceProvider.CreateScope().Returns(Substitute.For<IServiceScope>());
//        this.serviceProvider.CreateScope().ServiceProvider.GetRequiredService<MyContext>().Returns(context);

//        // Act
//        await sut.StartAsync(default);

//        // Assert
//        await context.Database.DidNotReceive().EnsureDeletedAsync(default);
//        await context.Database.DidNotReceiveWithAnyArgs().MigrateAsync(default);
//    }

//    [Fact]
//    public async Task StartAsync_WhenEnsureDeletedTrue_ShouldDelete()
//    {
//        // Arrange
//        var sut = new DatabaseMigratorService<MyContext>(this.loggerFactory, this.serviceProvider, new DatabaseMigratorOptions { EnsureDeleted = true });
//        var context = Substitute.For<MyContext>();
//        //context.Database.ProviderName.Returns("Provider");
//        context.Database.GetPendingMigrationsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new[] { "Migration1" }.AsEnumerable()));
//        this.serviceProvider.CreateScope().Returns(Substitute.For<IServiceScope>());
//        this.serviceProvider.CreateScope().ServiceProvider.GetRequiredService<MyContext>().Returns(context);

//        // Act
//        await sut.StartAsync(default);

//        // Assert
//        await context.Database.Received().EnsureDeletedAsync(default);
//        await context.Database.Received().MigrateAsync(default);
//    }

//    [Fact]
//    public async Task StartAsync_WhenPendingMigrations_ShouldMigrate()
//    {
//        // Arrange
//        var sut = new DatabaseMigratorService<MyContext>(this.loggerFactory, this.serviceProvider, new DatabaseMigratorOptions { EnsureDeleted = false });
//        var context = Substitute.For<MyContext>();
//        //context.Database.ProviderName.Returns("Provider");
//        context.Database.GetPendingMigrationsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new[] { "Migration1" }.AsEnumerable()));
//        this.serviceProvider.CreateScope().Returns(Substitute.For<IServiceScope>());
//        this.serviceProvider.CreateScope().ServiceProvider.GetRequiredService<MyContext>().Returns(context);

//        // Act
//        await sut.StartAsync(default);

//        // Assert
//        await context.Database.DidNotReceive().EnsureDeletedAsync(default);
//        await context.Database.Received().MigrateAsync(default);
//    }

//    public class MyContext : DbContext
//    {
//    }
//}