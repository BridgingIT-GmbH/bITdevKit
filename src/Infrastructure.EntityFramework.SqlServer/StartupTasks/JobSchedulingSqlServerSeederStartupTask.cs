﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class JobSchedulingSqlServerSeederStartupTask :
    IStartupTask,
    IRetryStartupTask,
    ITimeoutStartupTask
{
    private readonly ILogger<JobSchedulingSqlServerSeederStartupTask> logger;
    private readonly string connectionString;
    private readonly string tablePrefix;

    public JobSchedulingSqlServerSeederStartupTask(ILoggerFactory loggerFactory, IConfiguration configuration)
        : this(loggerFactory, configuration["JobScheduling:Quartz:quartz.dataSource.default.connectionString"], configuration["JobScheduling:Quartz:quartz.jobStore.tablePrefix"])
    {
    }

    public JobSchedulingSqlServerSeederStartupTask(ILoggerFactory loggerFactory, string connectionString, string tablePrefix = "[dbo].[QRTZ_")
    {
        EnsureArg.IsNotNullOrEmpty(connectionString, nameof(connectionString));

        this.logger = loggerFactory?.CreateLogger<JobSchedulingSqlServerSeederStartupTask>() ?? NullLoggerFactory.Instance.CreateLogger<JobSchedulingSqlServerSeederStartupTask>();
        this.connectionString = connectionString;
        this.tablePrefix = tablePrefix.EmptyToNull() ?? "[dbo].[QRTZ_";
    }

    RetryStartupTaskOptions IRetryStartupTask.Options => new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 1) };

    TimeoutStartupTaskOptions ITimeoutStartupTask.Options => new() { Timeout = new TimeSpan(0, 0, 30) };

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var connectionStringBuilder = new SqlConnectionStringBuilder(this.connectionString);
        var database = connectionStringBuilder.InitialCatalog;

        await using var connection = new SqlConnection(connectionStringBuilder.ConnectionString);
        connection.Open();
        var sql = SqlStatements.CreateQuartzTables(database, this.tablePrefix);

        //this.logger.LogDebug(sql);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}