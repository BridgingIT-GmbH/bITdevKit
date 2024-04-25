// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class CoreDomainSeederTask : IStartupTask // TODO: move to domain layer?
{
    private readonly ILogger<CoreDomainSeederTask> logger;
    private readonly IGenericRepository<User> userRepository;
    private readonly IGenericRepository<Host> hostRepository;
    private readonly IGenericRepository<Menu> menuRepository;
    private readonly IGenericRepository<Dinner> dinnerRepository;

    public CoreDomainSeederTask(
        ILoggerFactory loggerFactory,
        IGenericRepository<User> userRepository,
        IGenericRepository<Host> hostRepository,
        IGenericRepository<Menu> menuRepository,
        IGenericRepository<Dinner> dinnerRepository)
    {
        this.logger = loggerFactory?.CreateLogger<CoreDomainSeederTask>() ?? NullLoggerFactory.Instance.CreateLogger<CoreDomainSeederTask>();
        this.userRepository = userRepository;
        this.hostRepository = hostRepository;
        this.menuRepository = menuRepository;
        this.dinnerRepository = dinnerRepository;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await this.SeedUsers(this.userRepository);
        await this.SeedHosts(this.hostRepository);
        await this.SeedMenus(this.menuRepository);
        await this.SeedDinners(this.dinnerRepository);
    }

    private async Task SeedUsers(IGenericRepository<User> repository)
    {
        foreach (var entity in CoreSeeds.Users(0))
        {
            if (!await repository.ExistsAsync(entity.Id))
            {
                entity.AuditState.SetCreated("seed");

                await repository.InsertAsync(entity);
            }
        }
    }

    private async Task SeedHosts(IGenericRepository<Host> repository)
    {
        foreach (var entity in CoreSeeds.Hosts(0))
        {
            if (!await repository.ExistsAsync(entity.Id))
            {
                entity.AuditState.SetCreated("seed");

                await repository.InsertAsync(entity);
            }
        }
    }

    private async Task SeedMenus(IGenericRepository<Menu> repository)
    {
        foreach (var entity in CoreSeeds.Menus(0))
        {
            if (!await repository.ExistsAsync(entity.Id))
            {
                entity.AuditState.SetCreated("seed");

                await repository.InsertAsync(entity);
            }
        }
    }

    private async Task SeedDinners(IGenericRepository<Dinner> repository)
    {
        foreach (var entity in CoreSeeds.Dinners(0))
        {
            if (!await repository.ExistsAsync(entity.Id))
            {
                entity.AuditState.SetCreated("seed");

                await repository.InsertAsync(entity);
            }
        }
    }
}
