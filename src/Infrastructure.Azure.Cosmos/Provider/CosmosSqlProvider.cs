// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using Common;
using Cosmos.Repositories;
using Humanizer;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class CosmosSqlProvider<TItem> : ICosmosSqlProvider<TItem>, IDisposable
    where TItem : class
//where T : IHaveDiscriminator // needed? each type T is persisted in own collection
{
    private readonly ILogger<CosmosSqlProvider<TItem>> logger;
    private readonly CosmosSqlProviderOptions<TItem> options;
    private readonly PropertyInfo idProperty;
    private Container container;
    private string containerName;
    private Database database;

    public CosmosSqlProvider(CosmosSqlProviderOptions<TItem> options)
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.Client, nameof(options.Client));
        EnsureArg.IsNotNullOrEmpty(options.PartitionKey, nameof(options.PartitionKey));
        EnsureArg.IsTrue(
            typeof(TItem).GetProperties().Any(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase)),
            nameof(TItem),
            o => o.WithMessage($"Item type {typeof(TItem).Name} has to have a mandatory 'Id' property"));

        this.options = options;
        this.logger = this.options.LoggerFactory?.CreateLogger<CosmosSqlProvider<TItem>>() ??
            NullLoggerFactory.Instance.CreateLogger<CosmosSqlProvider<TItem>>();
        this.idProperty = typeof(TItem).GetProperty("Id") ?? typeof(TItem).GetProperty("id");
    }

    public CosmosSqlProvider(
        Builder<CosmosSqlProviderOptionsBuilder<TItem>, CosmosSqlProviderOptions<TItem>> optionsBuilder)
        : this(optionsBuilder(new CosmosSqlProviderOptionsBuilder<TItem>()).Build()) { }

    public async Task<TItem> ReadItemAsync(
        string id,
        object partitionKeyValue = null,
        CancellationToken cancellationToken = default)
    {
        if (id.IsNullOrEmpty())
        {
            return default;
        }

        await this.InitializeAsync(this.options);
        var watch = ValueStopwatch.StartNew();
        var requestOptions = this.EnsureQueryRequestOptions(partitionKeyValue, id);

        if (!requestOptions.PartitionKey.HasValue || requestOptions.PartitionKey == PartitionKey.None)
        {
            var iterator = this.container.GetItemQueryIterator<TItem>(
                new QueryDefinition($"select * from {this.containerName} c where c.id = @id").WithParameter("@id", id),
                requestOptions: requestOptions);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken).AnyContext();
                this.LogRequestCharge(response.RequestCharge, response.ActivityId);

                foreach (var result in response.Resource)
                {
                    this.logger.LogDebug("{LogKey} ReadItemAsync finished -> took {TimeElapsed:0.0000} ms",
                        "IFR",
                        watch.GetElapsedMilliseconds());

                    return result;
                }
            }
        }
        else
        {
            try
            {
                var response = await this.container.ReadItemAsync<TItem>(id,
                        requestOptions.PartitionKey.Value,
                        cancellationToken: cancellationToken)
                    .AnyContext();
                this.LogRequestCharge(response.RequestCharge, response.ActivityId);
                this.logger.LogDebug("{LogKey} ReadItemAsync finished -> took {TimeElapsed:0.0000} ms",
                    "IFR",
                    watch.GetElapsedMilliseconds());

                return response.Resource;
            }
            catch (CosmosException ex)
            {
                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    return default;
                }

                throw;
            }
        }

        return default;
    }

    public async Task<TItem> CreateItemAsync(
        TItem item,
        object partitionKeyValue = null,
        CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(item, nameof(item));
        EnsureArg.IsTrue(this.ValidateItemId(item),
            this.idProperty.Name,
            o => o.WithMessage($"Item property '{this.idProperty.Name}' has to have a mandatory value."));

        await this.InitializeAsync(this.options);
        var watch = ValueStopwatch.StartNew();
        var requestOptions = this.EnsureQueryRequestOptions(partitionKeyValue, item);

        if (!requestOptions.PartitionKey.HasValue || requestOptions.PartitionKey == PartitionKey.None)
        {
            // Partition key value will be populated by extracting from {T}
            var response = await this.container.CreateItemAsync(item, cancellationToken: cancellationToken)
                .AnyContext();
            this.LogRequestCharge(response.RequestCharge, response.ActivityId);
            this.logger.LogDebug("{LogKey} CreateItemAsync finished -> took {TimeElapsed:0.0000} ms",
                "IFR",
                watch.GetElapsedMilliseconds());

            return response.Resource;
        }
        else
        {
            var response = await this.container.CreateItemAsync(item,
                    requestOptions.PartitionKey.Value,
                    cancellationToken: cancellationToken)
                .AnyContext();
            this.LogRequestCharge(response.RequestCharge, response.ActivityId);
            this.logger.LogDebug("{LogKey} CreateItemAsync finished -> took {TimeElapsed:0.0000} ms",
                "IFR",
                watch.GetElapsedMilliseconds());

            return response.Resource;
        }
    }

    public async Task<TItem> UpsertItemAsync(
        TItem item,
        object partitionKeyValue = null,
        CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(item, nameof(item));
        EnsureArg.IsTrue(this.ValidateItemId(item),
            this.idProperty.Name,
            o => o.WithMessage($"Property '{this.idProperty.Name}' should have a value."));

        await this.InitializeAsync(this.options);
        var watch = ValueStopwatch.StartNew();
        var requestOptions = this.EnsureQueryRequestOptions(partitionKeyValue, item);

        if (!requestOptions.PartitionKey.HasValue || requestOptions.PartitionKey == PartitionKey.None)
        {
            var response = await this.container.UpsertItemAsync(item, cancellationToken: cancellationToken)
                .AnyContext();
            this.LogRequestCharge(response.RequestCharge, response.ActivityId);
            this.logger.LogDebug("{LogKey} UpsertItemAsync finished -> took {TimeElapsed:0.0000} ms",
                "IFR",
                watch.GetElapsedMilliseconds());

            return response.Resource;
        }
        else
        {
            var response = await this.container.UpsertItemAsync(item,
                    requestOptions.PartitionKey.Value,
                    cancellationToken: cancellationToken)
                .AnyContext();
            this.LogRequestCharge(response.RequestCharge, response.ActivityId);
            this.logger.LogDebug("{LogKey} UpsertItemAsync finished -> took {TimeElapsed:0.0000} ms",
                "IFR",
                watch.GetElapsedMilliseconds());

            return response.Resource;
        }
    }

    public async Task<IEnumerable<TItem>> ReadItemsAsync(
        Expression<Func<TItem, bool>> expression,
        int? skip = null,
        int? take = null,
        Expression<Func<TItem, object>> orderExpression = null,
        bool orderDescending = false,
        object partitionKeyValue = null,
        CancellationToken cancellationToken = default)
    {
        await this.InitializeAsync(this.options);
        var watch = ValueStopwatch.StartNew();
        var requestOptions = this.EnsureQueryRequestOptions(partitionKeyValue);

        double requestCharge = 0;
        var result = new List<TItem>();
        var iterator = this.container.GetItemLinqQueryable<TItem>(requestOptions: requestOptions,
                linqSerializerOptions: new CosmosLinqSerializerOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                })
            .WhereIf(expression)
            .OrderByIf(orderExpression, orderDescending)
            .SkipIf(skip)
            .TakeIf(take)
            .ToFeedIterator(); // https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.cosmos.container.getitemlinqqueryable?view=azure-dotnet

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken).AnyContext();
            requestCharge += response.RequestCharge;
            result.AddRange(response.Resource);
        }

        this.LogRequestCharge(requestCharge);
        this.logger.LogDebug("{LogKey} ReadItemsAsync finished -> took {TimeElapsed:0.0000} ms",
            "IFR",
            watch.GetElapsedMilliseconds());

        return result; // TODO: replace with IAsyncEnumerable (netstandard 2.1)
    }

    public async Task<IEnumerable<TItem>> ReadItemsAsync(
        IEnumerable<Expression<Func<TItem, bool>>> expressions = null,
        int? skip = null,
        int? take = null,
        Expression<Func<TItem, object>> orderExpression = null,
        bool orderDescending = false,
        object partitionKeyValue = null,
        CancellationToken cancellationToken = default)
    {
        await this.InitializeAsync(this.options);
        var watch = ValueStopwatch.StartNew();
        var requestOptions = this.EnsureQueryRequestOptions(partitionKeyValue);

        double requestCharge = 0;
        var result = new List<TItem>();
        var iterator = this.container.GetItemLinqQueryable<TItem>(requestOptions: requestOptions,
                linqSerializerOptions: new CosmosLinqSerializerOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                })
            .WhereIf(expressions)
            .OrderByIf(orderExpression, orderDescending)
            .SkipIf(skip)
            .TakeIf(take)
            .ToFeedIterator(); // https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.cosmos.container.getitemlinqqueryable?view=azure-dotnet

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken).AnyContext();
            requestCharge += response.RequestCharge;
            result.AddRange(response.Resource);
        }

        this.LogRequestCharge(requestCharge);
        this.logger.LogDebug("{LogKey} ReadItemsAsync finished -> took {TimeElapsed:0.0000} ms",
            "IFR",
            watch.GetElapsedMilliseconds());

        return result; // TODO: replace with IAsyncEnumerable (netstandard 2.1)
    }

    public async Task<bool> DeleteItemAsync(
        string id,
        object partitionKeyValue = null,
        CancellationToken cancellationToken = default)
    {
        if (id.IsNullOrEmpty())
        {
            return false;
        }

        await this.InitializeAsync(this.options);
        var watch = ValueStopwatch.StartNew();
        var item = await this.ReadItemAsync(id, partitionKeyValue, cancellationToken).AnyContext();
        if (item is null)
        {
            return false;
        }

        try
        {
            var requestOptions = this.EnsureQueryRequestOptions(partitionKeyValue, item);
            var response = await this.container.DeleteItemAsync<TItem>(id,
                    requestOptions.PartitionKey.Value,
                    cancellationToken: cancellationToken)
                .AnyContext();
            this.LogRequestCharge(response.RequestCharge, response.ActivityId);
            this.logger.LogDebug("{LogKey} DeleteItemAsync finished -> took {TimeElapsed:0.0000} ms",
                "IFR",
                watch.GetElapsedMilliseconds());

            return response.StatusCode == HttpStatusCode.NoContent;
        }
        catch (CosmosException ex)
        {
            if (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            throw;
        }
    }

    public void Dispose()
    {
        //this.client?.Dispose(); don't dispose instances which were injected (ctor)
    }

    private QueryRequestOptions EnsureQueryRequestOptions(object partitionKeyValue, string id = null)
    {
        var requestOptions = new QueryRequestOptions();
        if (partitionKeyValue is not null)
        {
            requestOptions.PartitionKey = partitionKeyValue switch
            {
                string s => new PartitionKey(s),
                bool b => new PartitionKey(b),
                double d => new PartitionKey(d),
                Guid g => new PartitionKey(g.ToString()),
                _ => throw new ArgumentException("Unsupported partition key value type (string, bool, double, guid)",
                    nameof(partitionKeyValue))
            };
        }

        if (requestOptions.PartitionKey is null &&
            this.options.PartitionKey.SafeEquals(CosmosSqlProviderOptions<TItem>.DefaultPartitionKey) &&
            !id.IsNullOrEmpty())
        {
            return this.EnsureQueryRequestOptions(id);
        }

        return requestOptions;
    }

    private QueryRequestOptions EnsureQueryRequestOptions(object partitionKeyValue, TItem item)
    {
        var requestOptions = new QueryRequestOptions();

        if (partitionKeyValue is not null)
        {
            requestOptions.PartitionKey = partitionKeyValue switch
            {
                string s => new PartitionKey(s),
                bool b => new PartitionKey(b),
                double d => new PartitionKey(d),
                Guid g => new PartitionKey(g.ToString()),
                _ => throw new ArgumentException("Unsupported partition key value type (string, bool, double, guid)",
                    nameof(partitionKeyValue))
            };
        }

        if (requestOptions.PartitionKey is null && item != null)
        {
            if (this.options.PartitionKeyStringExpression is not null)
            {
                return this.EnsureQueryRequestOptions(this.options.PartitionKeyStringExpression.Invoke(item));
            }

            if (this.options.PartitionKeyBoolExpression is not null)
            {
                return this.EnsureQueryRequestOptions(this.options.PartitionKeyBoolExpression.Invoke(item));
            }

            if (this.options.PartitionKeyDoubleExpression is not null)
            {
                return this.EnsureQueryRequestOptions(this.options.PartitionKeyDoubleExpression.Invoke(item));
            }

            if (this.options.PartitionKeyGuidExpression is not null)
            {
                return this.EnsureQueryRequestOptions(this.options.PartitionKeyGuidExpression.Invoke(item));
            }
        }

        return requestOptions;
    }

    private async Task InitializeAsync(CosmosSqlProviderOptions<TItem> options)
    {
        EnsureArg.IsNotNull(options.Client, nameof(options.Client));

        if (this.container is null)
        {
            var watch = ValueStopwatch.StartNew();
            ThroughputProperties throughputProperties;
            if (this.options.DatabaseAutoscale)
            {
                throughputProperties = ThroughputProperties.CreateAutoscaleThroughput(options.DatabaseThroughPut);
            }
            else
            {
                throughputProperties = ThroughputProperties.CreateManualThroughput(options.DatabaseThroughPut);
            }

            var databaseResponse =
                await options.Client.CreateDatabaseIfNotExistsAsync(options.Database.EmptyToNull() ?? "master",
                    throughputProperties);
            this.database = databaseResponse.Database;

            // replace the throughput on an already existing database, if the value has been configured differently
            if (databaseResponse.StatusCode == HttpStatusCode.OK &&
                options.DatabaseThroughPut > 0 &&
                await this.database.ReadThroughputAsync() != options.DatabaseThroughPut)
            {
                await this.database.ReplaceThroughputAsync(throughputProperties);
            }

            await this.InitializeContainerAsync(options);
            this.logger.LogDebug("{LogKey} InitializeAsync finished -> took {TimeElapsed:0.0000} ms",
                "IFR",
                watch.GetElapsedMilliseconds());
        }
    }

    private async Task InitializeContainerAsync(CosmosSqlProviderOptions<TItem> options)
    {
        this.containerName = options.Container.EmptyToNull() ?? typeof(TItem).Name.Pluralize();
        if (!options.ContainerPrefix.IsNullOrEmpty())
        {
            this.containerName =
                $"{this.options.ContainerPrefix}{this.options.ContainerPrefixSeperator}{this.containerName}"
                    .ToLowerInvariant();
        }
        else
        {
            this.containerName = this.containerName.ToLowerInvariant();
        }

        ThroughputProperties throughputProperties;
        if (this.options.Autoscale)
        {
            throughputProperties = ThroughputProperties.CreateAutoscaleThroughput(options.ThroughPut);
        }
        else
        {
            throughputProperties = ThroughputProperties.CreateManualThroughput(options.ThroughPut);
        }

        var containerResponse = await this.database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(this.containerName, this.options.PartitionKey)
                {
                    DefaultTimeToLive = this.options.TimeToLive
                    //IndexingPolicy = new Microsoft.Azure.Cosmos.IndexingPolicy(new RangeIndex(Microsoft.Azure.Cosmos.DataType.String) { Precision = -1 })
                },
                throughputProperties)
            .AnyContext();
        this.container = containerResponse.Container;

        // replace the throughput on an already existing container, if the value has been configured differently
        if (containerResponse.StatusCode == HttpStatusCode.OK &&
            options.ThroughPut > 0 &&
            await this.container.ReadThroughputAsync() != options.ThroughPut)
        {
            await this.container.ReplaceThroughputAsync(throughputProperties);
        }
    }

    private bool ValidateItemId(TItem item) // cosmos v3 needs an id for inserts/updates
    {
        if (this.idProperty?.PropertyType == typeof(string) &&
            this.idProperty.GetValue(item).To<string>().IsNullOrEmpty())
        {
            return false;
        }

        if (this.idProperty?.PropertyType == typeof(Guid) &&
            this.idProperty.GetValue(item).To<Guid>().Equals(Guid.Empty))
        {
            return false;
        }

        return true;
    }

    private void LogRequestCharge(double requestCharge, string activityId = null)
    {
        if (this.options.LogRequestCharges)
        {
            this.logger?.LogTrace(
                $"cosmos request charge: {requestCharge} (instance={this.database.Id}.{this.container.Id}, activityId={activityId})");
        }
    }

    //private void LogRequestCharge(IEnumerable<double> requestCharges, IEnumerable<string> activityIds)
    //{
    //    this.logger.LogDebug($"cosmos request charge total: {requestCharges.Sum()} (instance={this.database.Id}.{this.container.Id}, activityId=multiple)");

    //    //_logger.LogInformation($"cosmos request charge: {this.database.Id}.{this.container.Id};  Total RC: {requestCharges.Sum()}");
    //    //_logger.LogInformation($"cosmos request charge: detail: ActiveIds: {activityIds.ToString(", ")}");
    //    //_logger.LogInformation($"cosmos request charge: detail: requestCharges: {requestCharges.ToString(", ")}");
    //}
}