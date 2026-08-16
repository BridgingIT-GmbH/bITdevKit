// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using Common;
using Microsoft.Azure.Cosmos;

/// <summary>
/// Configures cosmos sql provider.
/// </summary>
/// <typeparam name="T">The  type.</typeparam>
public class CosmosSqlProviderOptions<T> : OptionsBase
{
    /// <summary>
    /// Gets the default partition key.
    /// </summary>
    public static string DefaultPartitionKey { get; } =
        "/id"; // /id path not recommeded for large amounts of documents as partition is not optimal (10k RUs or 20GB collections)

    /// <summary>
    /// Gets or sets the client.
    /// </summary>
    public CosmosClient Client { get; set; }

    //public string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the account end point.
    /// </summary>
    public string AccountEndPoint { get; set; }

    /// <summary>
    /// Gets or sets the account key.
    /// </summary>
    public string AccountKey { get; set; }

    /// <summary>
    /// Gets or sets the database.
    /// </summary>
    public string Database { get; set; } = "master";

    /// <summary>
    /// Gets or sets the database autoscale.
    /// </summary>
    public bool DatabaseAutoscale { get; set; }

    /// <summary>
    /// Gets or sets the database through put.
    /// </summary>
    public int DatabaseThroughPut { get; set; } = 400;

    /// <summary>
    /// Gets or sets the container prefix.
    /// </summary>
    public string ContainerPrefix { get; set; }

    /// <summary>
    /// Gets or sets the container prefix seperator.
    /// </summary>
    public char ContainerPrefixSeperator { get; set; } = '_';

    /// <summary>
    /// Gets or sets the container.
    /// </summary>
    public string Container { get; set; }

    /// <summary>
    /// Gets or sets the partition key.
    /// </summary>
    public string PartitionKey { get; set; } =
        DefaultPartitionKey; // /id path not recommeded for large amounts of documents as partition is not optimal (10k RUs or 20GB collections)

    /// <summary>
    /// Gets or sets the autoscale.
    /// </summary>
    public bool Autoscale { get; set; }

    /// <summary>
    /// Gets or sets the through put.
    /// </summary>
    public int ThroughPut { get; set; } = 400;

    /// <summary>
    /// Gets or sets the time to live.
    /// </summary>
    public int? TimeToLive { get; set; }

    /// <summary>
    /// Gets or sets the log request charges.
    /// </summary>
    public bool LogRequestCharges { get; set; } = true;

    /// <summary>
    /// Gets or sets the partition key string expression.
    /// </summary>
    public Func<T, string> PartitionKeyStringExpression { get; set; }

    /// <summary>
    /// Gets or sets the partition key bool expression.
    /// </summary>
    public Func<T, bool> PartitionKeyBoolExpression { get; set; }

    /// <summary>
    /// Gets or sets the partition key double expression.
    /// </summary>
    public Func<T, double> PartitionKeyDoubleExpression { get; set; }

    /// <summary>
    /// Gets or sets the partition key guid expression.
    /// </summary>
    public Func<T, Guid> PartitionKeyGuidExpression { get; set; }

    /// <summary>
    /// Gets or sets whether optimistic concurrency control is enabled.
    /// When enabled, updates will check the Version property for concurrency conflicts.
    /// </summary>
    public bool EnableOptimisticConcurrency { get; set; } = true;

    /// <summary>
    /// Gets or sets whether concurrency conflicts should throw exceptions.
    /// When false, conflicts will be logged but the operation will proceed.
    /// </summary>
    public bool ThrowOnConcurrencyConflict { get; set; } = true;

    /// <summary>
    /// Gets or sets the strategy for generating new version identifiers.
    /// </summary>
    public Func<Guid> VersionGenerator { get; set; } = GuidGenerator.CreateSequential;
}
