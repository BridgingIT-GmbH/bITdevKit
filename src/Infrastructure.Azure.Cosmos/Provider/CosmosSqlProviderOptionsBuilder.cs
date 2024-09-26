// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using System.Linq.Expressions;
using Common;
using Microsoft.Azure.Cosmos;

public class CosmosSqlProviderOptionsBuilder<T>
    : OptionsBuilderBase<CosmosSqlProviderOptions<T>, CosmosSqlProviderOptionsBuilder<T>>
{
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

    public CosmosSqlProviderOptionsBuilder<T> Account(string endPoint, string key)
    {
        this.Target.AccountEndPoint = endPoint;
        this.Target.AccountKey = key;
        this.Target.Client = new CosmosClient(endPoint, key);

        return this;
    }

    public CosmosSqlProviderOptionsBuilder<T> Database(string database)
    {
        this.Target.Database = database ?? "master";

        return this;
    }

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

    public CosmosSqlProviderOptionsBuilder<T> ContainerPrefix(string prefix, char? seperator = null)
    {
        this.Target.ContainerPrefix = prefix;
        this.Target.ContainerPrefixSeperator = seperator ?? '_';

        return this;
    }

    public CosmosSqlProviderOptionsBuilder<T> Container(string name)
    {
        this.Target.Container = name;

        return this;
    }

    public CosmosSqlProviderOptionsBuilder<T> PartitionKey(string partitionKey, bool partitionKeyCamelCase = true)
    {
        if (partitionKeyCamelCase && char.IsUpper(partitionKey[0]))
        {
            partitionKey = char.ToLowerInvariant(partitionKey[0]) + partitionKey[1..];
        }

        this.Target.PartitionKey = partitionKey[0] == '/' ? partitionKey : $"/{partitionKey}";

        return this;
    }

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

    public CosmosSqlProviderOptionsBuilder<T> TimeToLive(int seconds)
    {
        if (seconds < 0)
        {
            seconds = 0;
        }

        this.Target.TimeToLive = seconds;

        return this;
    }

    public CosmosSqlProviderOptionsBuilder<T> LogRequestCharges(bool value = true)
    {
        this.Target.LogRequestCharges = value;

        return this;
    }
}