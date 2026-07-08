// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using System.Globalization;
using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Application.Storage;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles Open-Meteo weather archive queue messages by writing normalized ingestion documents.
/// </summary>
/// <param name="documentStoreClient">The typed document-store client used to upsert archive documents.</param>
/// <param name="logger">The logger used to record archive outcomes.</param>
/// <example>
/// <code>
/// services.AddQueueing()
///     .WithSubscription&lt;OpenMeteoWeatherArchiveMessage, OpenMeteoWeatherArchiveHandler&gt;();
/// </code>
/// </example>
public sealed class OpenMeteoWeatherArchiveHandler(
    IDocumentStoreClient<OpenMeteoWeatherArchiveDocument> documentStoreClient,
    ILogger<OpenMeteoWeatherArchiveHandler> logger) : IQueueMessageHandler<OpenMeteoWeatherArchiveMessage>
{
    /// <summary>
    /// The document partition used for Open-Meteo weather archives.
    /// </summary>
    /// <example>
    /// <code>
    /// var partitionKey = OpenMeteoWeatherArchiveHandler.PartitionKey;
    /// </code>
    /// </example>
    public const string PartitionKey = "archive/openmeteo/weather";

    /// <summary>
    /// Archives the normalized weather ingestion result from the queue message.
    /// </summary>
    /// <param name="message">The archive queue message.</param>
    /// <param name="cancellationToken">The token used to cancel the archive operation.</param>
    /// <example>
    /// <code>
    /// await handler.Handle(message, cancellationToken);
    /// </code>
    /// </example>
    public async Task Handle(OpenMeteoWeatherArchiveMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var documentKey = CreateDocumentKey(message);
        var document = CreateDocument(message);

        var upsertResult = await documentStoreClient.UpsertResultAsync(documentKey, document, cancellationToken);
        if (upsertResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"Open-Meteo weather archive failed for city '{message.CityId}': {string.Join("; ", upsertResult.Errors.Select(e => e.Message))}");
        }

        logger.LogInformation(
            "openmeteo weather archive stored (cityId={CityId}, messageId={MessageId}, partitionKey={PartitionKey}, rowKey={RowKey})",
            message.CityId,
            message.MessageId,
            documentKey.PartitionKey,
            documentKey.RowKey);
    }

    private static DocumentKey CreateDocumentKey(OpenMeteoWeatherArchiveMessage message)
    {
        var cityId = Guid.Parse(message.CityId);
        var retrievedAt = message.RetrievedAt.ToUniversalTime();
        var rowKey = string.Create(
            CultureInfo.InvariantCulture,
            $"{cityId:N}/{retrievedAt:yyyy}/{retrievedAt:MM}/{retrievedAt:dd}/{retrievedAt:HHmmssfff}-{message.MessageId}.json");

        return new DocumentKey(PartitionKey, rowKey);
    }

    private static OpenMeteoWeatherArchiveDocument CreateDocument(OpenMeteoWeatherArchiveMessage message)
    {
        return new OpenMeteoWeatherArchiveDocument
        {
            CityId = message.CityId,
            CityName = message.CityName,
            CountryCode = message.CountryCode,
            ProviderName = message.ProviderName,
            RetrievedAt = message.RetrievedAt.ToUniversalTime(),
            WeatherIngestionResult = message.WeatherIngestionResult
        };
    }
}
