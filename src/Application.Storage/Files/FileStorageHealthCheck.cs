// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Checks whether all registered file-storage providers report healthy.
/// </summary>
/// <param name="factory">The file-storage provider factory containing the registered providers.</param>
/// <example>
/// <code>
/// services.AddHealthChecks()
///     .AddCheck&lt;FileStorageHealthCheck&gt;("FileStorage");
/// </code>
/// </example>
public sealed class FileStorageHealthCheck(IFileStorageProviderFactory factory) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var providerNames = factory.GetProviderNames().ToArray();
        var data = new Dictionary<string, object>
        {
            ["providerCount"] = providerNames.Length
        };

        if (providerNames.Length == 0)
        {
            data["healthyProviderCount"] = 0;
            data["failedProviderCount"] = 0;

            return HealthCheckResult.Healthy("No file storage providers are registered.", data);
        }

        var probeResults = new List<FileStorageProviderProbeResult>(providerNames.Length);
        foreach (var providerName in providerNames)
        {
            probeResults.Add(await ProbeProviderAsync(providerName, cancellationToken));
        }

        var failedResults = probeResults.Where(result => !result.IsHealthy).ToArray();
        data["healthyProviderCount"] = probeResults.Count - failedResults.Length;
        data["failedProviderCount"] = failedResults.Length;

        if (failedResults.Length > 0)
        {
            data["failedProviders"] = failedResults.Select(result => result.ProviderName).ToArray();
            data["providerErrors"] = failedResults.Select(result => $"{result.ProviderLabel}: {result.Details}").ToArray();

            return new HealthCheckResult(
                context.Registration.FailureStatus,
                $"File storage provider probe failed for {failedResults.Length} provider(s): {string.Join(", ", failedResults.Select(result => result.ProviderLabel))}.",
                data: data);
        }

        data["checkedProviders"] = probeResults.Select(result => result.ProviderName).ToArray();

        return HealthCheckResult.Healthy(
            $"All {probeResults.Count} file storage provider(s) are healthy.",
            data);
    }

    private async Task<FileStorageProviderProbeResult> ProbeProviderAsync(
        string providerName,
        CancellationToken cancellationToken)
    {
        try
        {
            var provider = factory.CreateProvider(providerName);
            var result = await provider.CheckHealthAsync(cancellationToken);

            if (result.IsSuccess)
            {
                return FileStorageProviderProbeResult.Healthy(providerName, provider);
            }

            return FileStorageProviderProbeResult.Unhealthy(providerName, provider, CreateResultDetails(result));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return FileStorageProviderProbeResult.Unhealthy(providerName, providerLabel: providerName, ex.GetBaseException().Message);
        }
    }

    private static string CreateResultDetails(IResult result)
    {
        var errors = result.Errors?.Select(error => error.Message).Where(message => !string.IsNullOrWhiteSpace(message)).ToArray() ?? [];
        if (errors.Length > 0)
        {
            return string.Join("; ", errors);
        }

        var messages = result.Messages?.Where(message => !string.IsNullOrWhiteSpace(message)).ToArray() ?? [];
        if (messages.Length > 0)
        {
            return string.Join("; ", messages);
        }

        return "Probe returned a failed result without details.";
    }

    private sealed record FileStorageProviderProbeResult(
        string ProviderName,
        string ProviderLabel,
        bool IsHealthy,
        string Details)
    {
        public static FileStorageProviderProbeResult Healthy(string providerName, IFileStorageProvider provider) =>
            new(
                providerName,
                CreateLabel(providerName, provider),
                true,
                string.Empty);

        public static FileStorageProviderProbeResult Unhealthy(string providerName, IFileStorageProvider provider, string details) =>
            new(
                providerName,
                CreateLabel(providerName, provider),
                false,
                details);

        public static FileStorageProviderProbeResult Unhealthy(string providerName, string providerLabel, string details) =>
            new(
                providerName,
                providerLabel,
                false,
                details);

        private static string CreateLabel(string providerName, IFileStorageProvider provider)
        {
            var locationName = provider?.LocationName;

            return string.IsNullOrWhiteSpace(locationName)
                ? providerName
                : $"{providerName} ({locationName})";
        }
    }
}
