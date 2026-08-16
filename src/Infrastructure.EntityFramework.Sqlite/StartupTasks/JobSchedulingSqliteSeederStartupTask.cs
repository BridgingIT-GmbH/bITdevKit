// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Data.SQLite;
using Common;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Represents job scheduling sqlite seeder startup task.
/// </summary>
public class JobSchedulingSqliteSeederStartupTask : IStartupTask, IRetryStartupTask, ITimeoutStartupTask
{
    private readonly ILogger<JobSchedulingSqliteSeederStartupTask> logger;
    private readonly string connectionString;
    private readonly string tablePrefix;

    /// <summary>
    /// Initializes a new instance of the <c>JobSchedulingSqliteSeederStartupTask</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    /// <param name="configuration">The configuration to apply.</param>
    public JobSchedulingSqliteSeederStartupTask(ILoggerFactory loggerFactory, IConfiguration configuration)
        : this(loggerFactory,
            configuration["JobScheduling:Quartz:quartz.dataSource.default.connectionString"],
            configuration["JobScheduling:Quartz:quartz.jobStore.tablePrefix"])
    { }

    /// <summary>
    /// Initializes a new instance of the <c>JobSchedulingSqliteSeederStartupTask</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    /// <param name="connectionString">The connection string used by the operation.</param>
    /// <param name="tablePrefix">The table prefix used by the operation.</param>
    public JobSchedulingSqliteSeederStartupTask(
        ILoggerFactory loggerFactory,
        string connectionString,
        string tablePrefix = "[dbo].[QRTZ_")
    {
        EnsureArg.IsNotNullOrEmpty(connectionString, nameof(connectionString));

        this.logger = loggerFactory?.CreateLogger<JobSchedulingSqliteSeederStartupTask>() ??
            NullLoggerFactory.Instance.CreateLogger<JobSchedulingSqliteSeederStartupTask>();
        this.connectionString = connectionString;
        this.tablePrefix = tablePrefix.EmptyToNull() ?? "QRTZ_";
    }

    RetryStartupTaskOptions IRetryStartupTask.Options => new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 1) };

    TimeoutStartupTaskOptions ITimeoutStartupTask.Options => new() { Timeout = new TimeSpan(0, 0, 30) };

    /// <summary>
    /// Executes the execute operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // https://github.com/akifmt/DotNetCoding/blob/3e2df14f3ac2eb227897c90377cc9bd76c7fce25/src/BlazorAppQuartzNETScheduler/BlazorAppQuartzNETScheduler/Program.cs#L89
        var connectionStringBuilder = new SqliteConnectionStringBuilder(this.connectionString);

        if (File.Exists(connectionStringBuilder.DataSource))
        {
            return;
        }

        SQLiteConnection.CreateFile(connectionStringBuilder.DataSource);

        await using var connection = new SQLiteConnection(connectionStringBuilder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = SqlStatements.CreateQuartzTables(this.tablePrefix);

        var command = new SQLiteCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        //connection.Close();
    }
}
