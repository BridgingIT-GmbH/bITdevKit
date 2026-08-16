// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using System.Linq.Expressions;
using Common;
using Microsoft.Azure.Cosmos;

/// <summary>
/// Builds cosmos sql provider options configuration.
/// </summary>
/// <typeparam name="T">The  type.</typeparam>
public class CosmosSqlProviderOptionsBuilder<T>
    : OptionsBuilderBase<CosmosSqlProviderOptions<T>, CosmosSqlProviderOptionsBuilder<T>>
{
    /// <summary>
    /// Executes the client operation.
    /// </summary>
    /// <param name="client">The client used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public CosmosSqlProviderOptionsBuilder<T> Client(CosmosClient client)
    {
        this.Target.Client = client;

        return this;
    }

    //public CosmosSqlProviderOptionsBuilder<T> ConnectionString(string connectionString)
    //{
    //    this.Target.ConnectionString = connectionString;
    //    this.Target.Client = new CosmosClient(connectionString);
    //    return this;
    //}

    /// <summary>
    /// Executes the account operation.
    /// </summary>
    /// <param name="endPoint">The end point used by the operation.</param>
    /// <param name="key">The key used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public CosmosSqlProviderOptionsBuilder<T> Account(string endPoint, string key)
    {
        this.Target.AccountEndPoint = endPoint;
        this.Target.AccountKey = key;
        this.Target.Client = new CosmosClient(endPoint, key);

        return this;
    }

    /// <summary>
    /// Executes the database operation.
    /// </summary>
    /// <param name="database">The database used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public CosmosSqlProviderOptionsBuilder<T> Database(string database)
    {
        this.Target.Database = database ?? "master";

        return this;
    }

    /// <summary>
    /// Executes the database autoscale operation.
    /// </summary>
    /// <param name="maxThroughPut">The max through put used by the operation.</param>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public CosmosSqlProviderOptionsBuilder<T> DatabaseAutoscale(int maxThroughPut = 1000, bool value = true)
    {
        this.Target.DatabaseAutoscale = value;
        if (maxThroughPut < 1000)
        {
            maxThroughPut = 1000; // autoscale needs at least 1000 RUs
        }

        this.DatabaseThroughPut(maxThroughPut);

        return this;
    }

    /// <summary>
    /// Executes the database through put operation.
    /// </summary>
    /// <param name="throughPut">The through put used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public CosmosSqlProviderOptionsBuilder<T> DatabaseThroughPut(int throughPut = 400)
    {
        if (throughPut < 400)
        {
            throughPut = 400;
        }

        if (throughPut > 1000000)
        {
            throughPut = 1000000;
        }

        this.Target.DatabaseThroughPut = throughPut;

        return this;
    }

    /// <summary>
    /// Executes the container prefix operation.
    /// </summary>
    /// <param name="prefix">The prefix used by the operation.</param>
    /// <param name="seperator">The seperator used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public CosmosSqlProviderOptionsBuilder<T> ContainerPrefix(string prefix, char? seperator = null)
    {
        this.Target.ContainerPrefix = prefix;
        this.Target.ContainerPrefixSeperator = seperator ?? '_';

        return this;
    }

    /// <summary>
    /// Executes the container operation.
    /// </summary>
    /// <param name="name">The name of the value.</param>
    /// <returns>The result of the operation.</returns>
    public CosmosSqlProviderOptionsBuilder<T> Container(string name)
    {
        this.Target.Container = name;

        return this;
    }

    /// <summary>
    /// Executes the partition key operation.
    /// </summary>
    /// <param name="partitionKey">The partition key used by the operation.</param>
    /// <param name="partitionKeyCamelCase">The partition key camel case used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public CosmosSqlProviderOptionsBuilder<T> PartitionKey(string partitionKey, bool partitionKeyCamelCase = true)
    {
        if (partitionKeyCamelCase && char.IsUpper(partitionKey[0]))
        {
            partitionKey = char.ToLowerInvariant(partitionKey[0]) + partitionKey[1..];
        }

        this.Target.PartitionKey = partitionKey[0] == '/' ? partitionKey : $"/{partitionKey}";

        return this;
    }

    /// <summary>
    /// Executes the partition key operation.
    /// </summary>
    /// <param name="partitionKeyExpression">The partition key expression used by the operation.</param>
    /// <param name="partitionKeyCamelCase">The partition key camel case used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public CosmosSqlProviderOptionsBuilder<T> PartitionKey(
        Expression<Func<T, string>> partitionKeyExpression,
        bool partitionKeyCamelCase = true)
    {
        this.Target.PartitionKeyStringExpression = partitionKeyExpression.Compile();
        var partitionKey = partitionKeyExpression.ToExpressionString().Replace(".", "/");
        if (partitionKeyCamelCase && char.IsUpper(partitionKey[0]))
        {
            partitionKey = char.ToLowerInvariant(partitionKey[0]) + partitionKey[1..];
        }

        this.Target.PartitionKey = $"/{partitionKey}";

        return this;
    }

    /// <summary>
    /// Executes the partition key operation.
    /// </summary>
    /// <param name="partitionKeyExpression">The partition key expression used by the operation.</param>
    /// <param name="partitionKeyCamelCase">The partition key camel case used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public CosmosSqlProviderOptionsBuilder<T> PartitionKey(
        Expression<Func<T, bool>> partitionKeyExpression,
        bool partitionKeyCamelCase = true)
    {
        this.Target.PartitionKeyBoolExpression = partitionKeyExpression.Compile();
        var partitionKey = partitionKeyExpression.ToExpressionString().Replace(".", "/");
        if (partitionKeyCamelCase && char.IsUpper(partitionKey[0]))
        {
            partitionKey = char.ToLowerInvariant(partitionKey[0]) + partitionKey[1..];
        }

        this.Target.PartitionKey = $"/{partitionKey}";

        return this;
    }

    /// <summary>
    /// Executes the partition key operation.
    /// </summary>
    /// <param name="partitionKeyExpression">The partition key expression used by the operation.</param>
    /// <param name="partitionKeyCamelCase">The partition key camel case used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public CosmosSqlProviderOptionsBuilder<T> PartitionKey(
        Expression<Func<T, double>> partitionKeyExpression,
        bool partitionKeyCamelCase = true)
    {
        this.Target.PartitionKeyDoubleExpression = partitionKeyExpression.Compile();
        var partitionKey = partitionKeyExpression.ToExpressionString().Replace(".", "/");
        if (partitionKeyCamelCase && char.IsUpper(partitionKey[0]))
        {
            partitionKey = char.ToLowerInvariant(partitionKey[0]) + partitionKey[1..];
        }

        this.Target.PartitionKey = $"/{partitionKey}";

        return this;
    }

    /// <summary>
    /// Executes the partition key operation.
    /// </summary>
    /// <param name="partitionKeyExpression">The partition key expression used by the operation.</param>
    /// <param name="partitionKeyCamelCase">The partition key camel case used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public CosmosSqlProviderOptionsBuilder<T> PartitionKey(
        Expression<Func<T, Guid>> partitionKeyExpression,
        bool partitionKeyCamelCase = true)
    {
        this.Target.PartitionKeyGuidExpression = partitionKeyExpression.Compile();
        var partitionKey = partitionKeyExpression.ToExpressionString().Replace(".", "/");
        if (partitionKeyCamelCase && char.IsUpper(partitionKey[0]))
        {
            partitionKey = char.ToLowerInvariant(partitionKey[0]) + partitionKey[1..];
        }

        this.Target.PartitionKey = $"/{partitionKey}";

        return this;
    }

    /// <summary>
    /// Executes the autoscale operation.
    /// </summary>
    /// <param name="maxThroughPut">The max through put used by the operation.</param>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public CosmosSqlProviderOptionsBuilder<T> Autoscale(int maxThroughPut = 1000, bool value = true)
    {
        this.Target.Autoscale = value;
        if (maxThroughPut < 1000)
        {
            maxThroughPut = 1000; // autoscale needs at least 1000 RUs
        }

        this.ThroughPut(maxThroughPut);

        return this;
    }

    /// <summary>
    /// Executes the through put operation.
    /// </summary>
    /// <param name="throughPut">The through put used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public CosmosSqlProviderOptionsBuilder<T> ThroughPut(int throughPut = 400)
    {
        if (throughPut < 400)
        {
            throughPut = 400;
        }

        if (throughPut > 1000000)
        {
            throughPut = 1000000;
        }

        this.Target.ThroughPut = throughPut;

        return this;
    }

    /// <summary>
    /// Executes the time to live operation.
    /// </summary>
    /// <param name="seconds">The seconds used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public CosmosSqlProviderOptionsBuilder<T> TimeToLive(int seconds)
    {
        if (seconds < 0)
        {
            seconds = 0;
        }

        this.Target.TimeToLive = seconds;

        return this;
    }

    /// <summary>
    /// Writes a log entry for the request charges operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public CosmosSqlProviderOptionsBuilder<T> LogRequestCharges(bool value = true)
    {
        this.Target.LogRequestCharges = value;

        return this;
    }

    /// <summary>
    /// Executes the enable optimistic concurrency operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public CosmosSqlProviderOptionsBuilder<T> EnableOptimisticConcurrency(bool value = true)
    {
        this.Target.EnableOptimisticConcurrency = value;

        return this;
    }

    /// <summary>
    /// Executes the throw on concurrency conflict operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public CosmosSqlProviderOptionsBuilder<T> ThrowOnConcurrencyConflict(bool value = true)
    {
        this.Target.ThrowOnConcurrencyConflict = value;

        return this;
    }
}
