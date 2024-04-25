// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application.Jobs;

using System.Threading.Tasks;
using BridgingIT.DevKit.Application.JobScheduling;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using Microsoft.Extensions.Logging;
using Quartz;

public class DinnerSnapshotExportJob : JobBase //IRetryJob, IChaosExceptionJobScheduling
{
    private readonly IGenericRepository<Dinner> dinnerRepository;
    private readonly IGenericRepository<Menu> menuRepository;
    private readonly IDocumentStoreClient<DinnerSnapshotDocument> documentStoreClient;

    public DinnerSnapshotExportJob(
        ILoggerFactory loggerFactory,
        IGenericRepository<Dinner> dinnerRepository,
        IGenericRepository<Menu> menuRepository,
        IDocumentStoreClient<DinnerSnapshotDocument> documentStoreClient)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(dinnerRepository, nameof(dinnerRepository));
        EnsureArg.IsNotNull(menuRepository, nameof(menuRepository));
        EnsureArg.IsNotNull(documentStoreClient, nameof(documentStoreClient));

        this.dinnerRepository = dinnerRepository;
        this.menuRepository = menuRepository;
        this.documentStoreClient = documentStoreClient;
    }

    //RetryJobOptions IRetryJob.Options => new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 1) };

    //ChaosExceptionJobSchedulingOptions IChaosExceptionJobScheduling.Options => new() { InjectionRate = 0.10 };

    public override async Task Process(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var dinners = await this.dinnerRepository.FindAllAsync(cancellationToken: cancellationToken);
        var menus = await this.menuRepository.FindAllAsync(cancellationToken: cancellationToken);

        foreach (var dinner in dinners.SafeNull())
        {
            var menu = menus.FirstOrDefault(m => m.Id == dinner.MenuId);
            var document = new DinnerSnapshotDocument
            {
                Id = dinner.Id.ToString(),
                Name = dinner.Name,
                Description = dinner.Description,
                Menu = new MenuSnapshotDocument
                {
                    Id = menu?.Id?.ToString(),
                    Name = menu?.Name,
                    Description = menu?.Description,
                }
            };
            // TODO: map everything incl. the menus to the snapshot

            await this.documentStoreClient.UpsertAsync(
                new DocumentKey("Dinner", document.Id),
                document, cancellationToken);
        }
    }
}
