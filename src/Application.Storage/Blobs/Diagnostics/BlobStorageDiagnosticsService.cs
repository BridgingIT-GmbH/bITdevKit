// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Creates provider-neutral Blob Storage diagnostics snapshots.
/// </summary>
/// <param name="scopeFactory">The scope factory used to resolve named clients safely.</param>
/// <param name="registrations">The registered blob clients.</param>
/// <param name="admissionCoordinator">The optional shared upload-admission coordinator.</param>
/// <example>
/// <code>
/// var snapshot = await service.GetSnapshotAsync();
/// </code>
/// </example>
public sealed class BlobStorageDiagnosticsService(
    IServiceScopeFactory scopeFactory,
    IEnumerable<BlobStoreClientRegistration> registrations,
    IBlobUploadAdmissionCoordinator admissionCoordinator = null) : IBlobStorageDiagnosticsService
{
    private static readonly BlobKey ProbeKey = new("__bdk", "healthcheck/probe");
    private readonly IReadOnlyList<BlobStoreClientRegistration> registrations = registrations?.ToArray() ?? [];

    /// <inheritdoc />
    public async Task<Result<BlobStorageDiagnosticsSnapshot>> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var clients = new List<BlobStorageClientDiagnostics>(this.registrations.Count);
        if (this.registrations.Count == 0)
        {
            return Result<BlobStorageDiagnosticsSnapshot>.Success(new BlobStorageDiagnosticsSnapshot
            {
                ClientCount = 0,
                HealthyClientCount = 0,
                UnhealthyClientCount = 0,
                Clients = clients
            });
        }

        using var scope = scopeFactory.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IBlobStoreClientFactory>();

        foreach (var registration in this.registrations)
        {
            clients.Add(await ProbeAsync(
                factory,
                registration,
                admissionCoordinator,
                cancellationToken).ConfigureAwait(false));
        }

        return Result<BlobStorageDiagnosticsSnapshot>.Success(new BlobStorageDiagnosticsSnapshot
        {
            ClientCount = clients.Count,
            HealthyClientCount = clients.Count(client => client.IsHealthy),
            UnhealthyClientCount = clients.Count(client => !client.IsHealthy),
            Clients = clients
        });
    }

    private static async Task<BlobStorageClientDiagnostics> ProbeAsync(
        IBlobStoreClientFactory factory,
        BlobStoreClientRegistration registration,
        IBlobUploadAdmissionCoordinator admissionCoordinator,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = factory.CreateClient(registration.Name);
            var result = await client.ExistsAsync(ProbeKey, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess || result.HasError<BlobStoreNotFoundError>())
            {
                return Create(
                    registration,
                    admissionCoordinator,
                    true,
                    "Healthy",
                    "Probe completed.");
            }

            return Create(
                registration,
                admissionCoordinator,
                false,
                "Unhealthy",
                CreateDetails(result));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Create(
                registration,
                admissionCoordinator,
                false,
                "Unhealthy",
                ex.GetBaseException().Message);
        }
    }

    private static BlobStorageClientDiagnostics Create(
        BlobStoreClientRegistration registration,
        IBlobUploadAdmissionCoordinator admissionCoordinator,
        bool isHealthy,
        string status,
        string details)
    {
        var admission = admissionCoordinator?.GetSnapshots().FirstOrDefault(snapshot =>
            string.Equals(
                snapshot.StoreName,
                registration.Name.Trim(),
                StringComparison.OrdinalIgnoreCase));

        return new()
        {
            Name = registration.Name,
            ProviderName = registration.ProviderName,
            Capabilities = registration.Capabilities,
            IsHealthy = isHealthy,
            HealthStatus = status,
            HealthDetails = details,
            UploadAdmissionEnabled = admission is not null,
            MaxConcurrentUploads = admission?.MaxConcurrentUploads ?? 0,
            MaxQueuedUploads = admission?.MaxQueuedUploads ?? 0,
            ActiveUploads = admission?.ActiveUploads ?? 0,
            QueuedUploads = admission?.QueuedUploads ?? 0
        };
    }

    private static string CreateDetails(IResult result)
    {
        var errors = result.Errors?.Select(error => error.Message).Where(message => !string.IsNullOrWhiteSpace(message)).ToArray() ?? [];
        if (errors.Length > 0)
        {
            return string.Join("; ", errors);
        }

        var messages = result.Messages?.Where(message => !string.IsNullOrWhiteSpace(message)).ToArray() ?? [];
        return messages.Length > 0 ? string.Join("; ", messages) : "Probe returned a failed result without details.";
    }
}
