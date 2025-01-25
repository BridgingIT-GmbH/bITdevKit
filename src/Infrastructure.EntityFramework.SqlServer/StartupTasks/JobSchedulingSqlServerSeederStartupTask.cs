// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class JobSchedulingSqlServerSeederStartupTask
    : IStartupTask, IRetryStartupTask, ITimeoutStartupTask
{
    private const string LogKey = "UTL";
    private readonly ILogger<JobSchedulingSqlServerSeederStartupTask> logger;
    private readonly string connectionString;
    private readonly string tablePrefix;

    public JobSchedulingSqlServerSeederStartupTask(ILoggerFactory loggerFactory, IConfiguration configuration)
        : this(loggerFactory,
            configuration.GetSection("JobScheduling:Quartz", false)["quartz.dataSource.default.connectionString"],
            configuration.GetSection("JobScheduling:Quartz", false)["quartz.jobStore.tablePrefix"])
    { }

    public JobSchedulingSqlServerSeederStartupTask(
        ILoggerFactory loggerFactory,
        string connectionString,
        string tablePrefix = "[dbo].[QRTZ_")
    {
        EnsureArg.IsNotNullOrEmpty(connectionString, nameof(connectionString));

        this.logger = loggerFactory?.CreateLogger<JobSchedulingSqlServerSeederStartupTask>() ??
            NullLoggerFactory.Instance.CreateLogger<JobSchedulingSqlServerSeederStartupTask>();
        this.connectionString = connectionString;
        this.tablePrefix = tablePrefix.EmptyToNull() ?? "[dbo].[QRTZ_";
    }

    RetryStartupTaskOptions IRetryStartupTask.Options => new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 3) };

    TimeoutStartupTaskOptions ITimeoutStartupTask.Options => new() { Timeout = new TimeSpan(0, 0, 30) };

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(this.connectionString);
            var database = connectionStringBuilder.InitialCatalog;
            var sql = SqlStatements.QuartzTables(database, this.tablePrefix);
            this.logger.LogInformation("{LogKey} quartz sqlserver seeding started (database={Database})", LogKey, database);

            await using var connection = new SqlConnection(connectionStringBuilder.ConnectionString);
            await using var command = new SqlCommand(sql, connection);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} quartz sqlserver seeding failed", LogKey);
        }
    }
}