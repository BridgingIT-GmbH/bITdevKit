// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using Common;
using Microsoft.Azure.Cosmos;

public class CosmosSqlProviderOptions<T> : OptionsBase
{
    public static string DefaultPartitionKey { get; } =
        "/id"; // /id path not recommeded for large amounts of documents as partition is not optimal (10k RUs or 20GB collections)

    public CosmosClient Client { get; set; }

    //public string ConnectionString { get; set; }

    public string AccountEndPoint { get; set; }

    public string AccountKey { get; set; }

    public string Database { get; set; } = "master";

    public bool DatabaseAutoscale { get; set; }

    public int DatabaseThroughPut { get; set; } = 400;

    public string ContainerPrefix { get; set; }

    public char ContainerPrefixSeperator { get; set; } = '_';

    public string Container { get; set; }

    public string PartitionKey { get; set; } =
        DefaultPartitionKey; // /id path not recommeded for large amounts of documents as partition is not optimal (10k RUs or 20GB collections)

    public bool Autoscale { get; set; }

    public int ThroughPut { get; set; } = 400;

    public int? TimeToLive { get; set; }

    public bool LogRequestCharges { get; set; } = true;

    public Func<T, string> PartitionKeyStringExpression { get; set; }

    public Func<T, bool> PartitionKeyBoolExpression { get; set; }

    public Func<T, double> PartitionKeyDoubleExpression { get; set; }

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