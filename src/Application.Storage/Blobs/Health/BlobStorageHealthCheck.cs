// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Checks whether all registered blob-store clients can execute a non-mutating existence probe.
/// </summary>
/// <param name="scopeFactory">The scope factory used to resolve named clients without capturing scoped services.</param>
/// <param name="registrations">The registered blob-store clients to probe.</param>
/// <example>
/// <code>
/// services.AddHealthChecks()
///     .AddCheck&lt;BlobStorageHealthCheck&gt;("BlobStorage");
/// </code>
/// </example>
public sealed class BlobStorageHealthCheck(
    IServiceScopeFactory scopeFactory,
    IEnumerable<BlobStoreClientRegistration> registrations) : IHealthCheck
{
    private static readonly BlobKey ProbeKey = new("__bdk", "healthcheck/probe");
    private readonly IReadOnlyList<BlobStoreClientRegistration> registrations = registrations?.ToArray() ?? [];

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["container"] = ProbeKey.Container,
            ["name"] = ProbeKey.Name,
            ["clientCount"] = this.registrations.Count
        };

        if (this.registrations.Count == 0)
        {
            data["healthyClientCount"] = 0;
            data["failedClientCount"] = 0;
            data["checkedClients"] = string.Empty;
            data["failedClients"] = string.Empty;

            return HealthCheckResult.Healthy("No blob storage clients are registered.", data);
        }

        var probeResults = new List<BlobStoreClientProbeResult>(this.registrations.Count);

        using var scope = scopeFactory.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IBlobStoreClientFactory>();
        foreach (var registration in this.registrations)
        {
            probeResults.Add(await ProbeClientAsync(factory, registration, cancellationToken).ConfigureAwait(false));
        }

        var failedResults = probeResults.Where(result => !result.IsHealthy).ToArray();
        data["healthyClientCount"] = probeResults.Count - failedResults.Length;
        data["failedClientCount"] = failedResults.Length;
        data["checkedClients"] = string.Join(", ", probeResults.Select(result => result.ClientName));
        data["failedClients"] = string.Join(", ", failedResults.Select(result => result.ClientName));

        if (failedResults.Length > 0)
        {
            data["clientErrors"] = string.Join("; ", failedResults.Select(result => $"{result.ClientName}: {result.Details}"));

            return new HealthCheckResult(
                context.Registration.FailureStatus,
                $"Blob storage client probe failed for {failedResults.Length} client(s): {string.Join(", ", failedResults.Select(result => result.ClientName))}.",
                data: data);
        }

        return HealthCheckResult.Healthy(
            $"All {probeResults.Count} blob storage client(s) are reachable.",
            data);
    }

    private static async Task<BlobStoreClientProbeResult> ProbeClientAsync(
        IBlobStoreClientFactory factory,
        BlobStoreClientRegistration registration,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = factory.CreateClient(registration.Name);
            var result = await client.ExistsAsync(ProbeKey, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess || result.HasError<BlobStoreNotFoundError>())
            {
                return BlobStoreClientProbeResult.Healthy(registration);
            }

            return BlobStoreClientProbeResult.Unhealthy(registration, CreateResultDetails(result));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return BlobStoreClientProbeResult.Unhealthy(registration, ex.GetBaseException().Message);
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

    private sealed record BlobStoreClientProbeResult(
        string ClientName,
        bool IsHealthy,
        string Details)
    {
        public static BlobStoreClientProbeResult Healthy(BlobStoreClientRegistration registration) =>
            new(registration.Name, true, string.Empty);

        public static BlobStoreClientProbeResult Unhealthy(BlobStoreClientRegistration registration, string details) =>
            new(registration.Name, false, details);
    }
}
