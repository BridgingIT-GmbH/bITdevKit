// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Creates an isolated LiteDB feature host with explicit external delivery doubles.</summary>
public sealed class LiteDbFeatureTestHost : IAsyncDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("bdk-features-");

    public LiteDbFeatureTestHost(Action<IServiceCollection> configure = null, string file = null, INotifier notifier = null)
    {
        global::LiteDB.LiteDBPragmas.I_AM_AWARE_MY_DATABASE_BREAKS_WHEN_I_USE_THIS();
        this.File = file ?? Path.Combine(this.directory.FullName, "feature.db");
        var services = new ServiceCollection();
        services.AddLogging();
        this.Notifier = notifier ?? Substitute.For<INotifier>();
        this.Notifier.PublishDynamicAsync(Arg.Any<INotification>(), Arg.Any<PublishOptions>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IResult>(Result.Success()));
        services.AddSingleton(this.Notifier);
        services.AddLiteDbDatabase(o => o.ConnectionString = this.File);
        services.AddLiteDbRepository<HistoryAccount>(configure: o => o.CollectionName = "accounts");
        services.AddLiteDbEntityBulkInserter<HistoryAccount>(configure: o => o.CollectionName = "accounts");
        configure?.Invoke(services);
        this.Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        this.Scope = this.Services.CreateAsyncScope();
        this.Repository = this.Scope.ServiceProvider.GetRequiredService<IGenericRepository<HistoryAccount>>();
    }

    public string File { get; }

    public ServiceProvider Services { get; }

    public AsyncServiceScope Scope { get; }

    public IGenericRepository<HistoryAccount> Repository { get; }

    public INotifier Notifier { get; }

    public async Task<T> NativeAsync<T>(Func<global::LiteDB.LiteDatabase, T> action, bool write = false)
    {
        await using var scope = this.Services.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredKeyedService<LiteDbDatabaseSession>("default");
        var result = await session.ExecuteAsync(async () => Result<T>.Success(await session.NativeAsync(action)), write);
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    public async ValueTask DisposeAsync()
    {
        await this.Scope.DisposeAsync();
        await this.Services.DisposeAsync();
        this.directory.Delete(true);
    }
}

public class HistoryAccount : AggregateRoot<Guid>, IConcurrency
{
    public string Name { get; set; }

    public int Balance { get; set; }

    public string SecretToken { get; set; }

    public Guid ConcurrencyVersion { get; set; }

    public HistoryAddress Address { get; set; }

    public List<HistoryContact> Contacts { get; set; } = [];
}

public class HistoryAddress
{
    public string City { get; set; }
}

public class HistoryContact
{
    public string Id { get; set; }

    public string Name { get; set; }

    public string SecretToken { get; set; }
}

public sealed class AccountRenamed : DomainEventBase
{
    public string Name { get; set; }
}
