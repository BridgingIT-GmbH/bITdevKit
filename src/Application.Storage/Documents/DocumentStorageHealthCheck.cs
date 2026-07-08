// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Checks whether all registered document-store clients can execute an exact-key existence probe.
/// </summary>
/// <param name="scopeFactory">The scope factory used to resolve typed clients without capturing scoped services.</param>
/// <param name="descriptors">The registered document-store client descriptors to probe.</param>
/// <example>
/// <code>
/// services.AddHealthChecks()
///     .AddCheck&lt;DocumentStorageHealthCheck&gt;("DocumentStorage");
/// </code>
/// </example>
public sealed class DocumentStorageHealthCheck(
    IServiceScopeFactory scopeFactory,
    IEnumerable<DocumentStoreClientDescriptor> descriptors) : IHealthCheck
{
    private static readonly DocumentKey ProbeKey = new("__bdk/healthcheck", "probe");
    private static readonly System.Reflection.MethodInfo ProbeClientMethod = typeof(DocumentStorageHealthCheck)
        .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        .Single(method => method.Name == nameof(ProbeClientAsync) && method.IsGenericMethodDefinition);

    private readonly IReadOnlyList<DocumentStoreClientDescriptor> descriptors = descriptors?.ToArray() ?? [];

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["partitionKey"] = ProbeKey.PartitionKey,
            ["rowKey"] = ProbeKey.RowKey,
            ["clientCount"] = this.descriptors.Count
        };

        if (this.descriptors.Count == 0)
        {
            data["healthyClientCount"] = 0;
            data["failedClientCount"] = 0;

            return HealthCheckResult.Healthy("No document storage clients are registered.", data);
        }

        var probeResults = new List<DocumentStoreClientProbeResult>(this.descriptors.Count);

        using var scope = scopeFactory.CreateScope();
        foreach (var descriptor in this.descriptors)
        {
            probeResults.Add(await this.ProbeClientAsync(scope.ServiceProvider, descriptor, cancellationToken));
        }

        var failedResults = probeResults.Where(result => !result.IsHealthy).ToArray();
        data["healthyClientCount"] = probeResults.Count - failedResults.Length;
        data["failedClientCount"] = failedResults.Length;

        if (failedResults.Length > 0)
        {
            data["failedClients"] = failedResults.Select(result => result.ClientId).ToArray();
            data["clientErrors"] = failedResults.Select(result => $"{result.ClientLabel}: {result.Details}").ToArray();

            return new HealthCheckResult(
                context.Registration.FailureStatus,
                $"Document storage client probe failed for {failedResults.Length} client(s): {string.Join(", ", failedResults.Select(result => result.ClientLabel))}.",
                data: data);
        }

        data["checkedClients"] = probeResults.Select(result => result.ClientId).ToArray();

        return HealthCheckResult.Healthy(
            $"All {probeResults.Count} document storage client(s) are reachable.",
            data);
    }

    private async Task<DocumentStoreClientProbeResult> ProbeClientAsync(
        IServiceProvider serviceProvider,
        DocumentStoreClientDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        try
        {
            var method = ProbeClientMethod.MakeGenericMethod(descriptor.DocumentType);
            var task = (Task<DocumentStoreClientProbeResult>)method.Invoke(this, [serviceProvider, descriptor, cancellationToken]);

            return await task;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return DocumentStoreClientProbeResult.Unhealthy(descriptor, ex.GetBaseException().Message);
        }
    }

    private async Task<DocumentStoreClientProbeResult> ProbeClientAsync<T>(
        IServiceProvider serviceProvider,
        DocumentStoreClientDescriptor descriptor,
        CancellationToken cancellationToken)
        where T : class, new()
    {
        try
        {
            var client = serviceProvider.GetRequiredService<IDocumentStoreClient<T>>();
            var result = await client.ExistsResultAsync(ProbeKey, cancellationToken);

            if (result.IsSuccess)
            {
                return DocumentStoreClientProbeResult.Healthy(descriptor);
            }

            return DocumentStoreClientProbeResult.Unhealthy(descriptor, CreateResultDetails(result));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return DocumentStoreClientProbeResult.Unhealthy(descriptor, ex.GetBaseException().Message);
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

    private sealed record DocumentStoreClientProbeResult(
        string ClientId,
        string ClientLabel,
        bool IsHealthy,
        string Details)
    {
        public static DocumentStoreClientProbeResult Healthy(DocumentStoreClientDescriptor descriptor) =>
            new(
                descriptor.ClientId,
                CreateLabel(descriptor),
                true,
                string.Empty);

        public static DocumentStoreClientProbeResult Unhealthy(DocumentStoreClientDescriptor descriptor, string details) =>
            new(
                descriptor.ClientId,
                CreateLabel(descriptor),
                false,
                details);

        private static string CreateLabel(DocumentStoreClientDescriptor descriptor) =>
            $"{descriptor.DocumentTypeName} ({descriptor.ClientId})";
    }
}
