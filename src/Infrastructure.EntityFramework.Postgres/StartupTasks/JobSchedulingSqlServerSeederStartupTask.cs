// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Domain.Repositories;
using Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

/// <summary>
/// Represents job scheduling postgres seeder startup task.
/// </summary>
public class JobSchedulingPostgresSeederStartupTask
    : IStartupTask, IRetryStartupTask, ITimeoutStartupTask
{
    private const string LogKey = "UTL";
    private readonly ILogger<JobSchedulingPostgresSeederStartupTask> logger;
    private readonly string connectionString;
    private readonly string tablePrefix;
    private readonly IDatabaseReadyService databaseReadyService;

    /// <summary>
    /// Initializes a new instance of the <c>JobSchedulingPostgresSeederStartupTask</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    /// <param name="configuration">The configuration to apply.</param>
    /// <param name="databaseReadyService">The database ready service used by the operation.</param>
    public JobSchedulingPostgresSeederStartupTask(
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IDatabaseReadyService databaseReadyService = null)
        : this(loggerFactory,
            configuration.GetSection("JobScheduling:Quartz", false)["quartz.dataSource.default.connectionString"],
            configuration.GetSection("JobScheduling:Quartz", false)["quartz.jobStore.tablePrefix"])
    {
        this.databaseReadyService = databaseReadyService;
    }

    /// <summary>
    /// Initializes a new instance of the <c>JobSchedulingPostgresSeederStartupTask</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    /// <param name="connectionString">The connection string used by the operation.</param>
    /// <param name="tablePrefix">The table prefix used by the operation.</param>
    public JobSchedulingPostgresSeederStartupTask(
        ILoggerFactory loggerFactory,
        string connectionString,
        string tablePrefix = "[public].[QRTZ_")
    {
        EnsureArg.IsNotNullOrEmpty(connectionString, nameof(connectionString));

        this.logger = loggerFactory?.CreateLogger<JobSchedulingPostgresSeederStartupTask>() ??
            NullLoggerFactory.Instance.CreateLogger<JobSchedulingPostgresSeederStartupTask>();
        this.connectionString = connectionString;
        this.tablePrefix = tablePrefix.EmptyToNull() ?? "[public].[QRTZ_";
    }

    RetryStartupTaskOptions IRetryStartupTask.Options => new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 3) };

    TimeoutStartupTaskOptions ITimeoutStartupTask.Options => new() { Timeout = new TimeSpan(0, 0, 30) };

    /// <summary>
    /// Executes the execute operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (this.databaseReadyService != null)
            {
                await this.databaseReadyService.WaitForReadyAsync(cancellationToken: cancellationToken).AnyContext();
            }

            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(this.connectionString);
            var database = connectionStringBuilder.Database;
            var sql = SqlStatements.QuartzTables(database, this.tablePrefix);
            this.logger.LogInformation("[{LogKey}] quartz sqlserver seeding started (database={Database})", LogKey, database);

            await using var connection = new NpgsqlConnection(connectionStringBuilder.ConnectionString);
            await using var command = new NpgsqlCommand(sql, connection);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "[{LogKey}] quartz sqlserver seeding failed", LogKey);
        }
    }
}
