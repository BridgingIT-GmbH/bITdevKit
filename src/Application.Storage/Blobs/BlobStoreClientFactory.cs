// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Resolves configured blob-store clients by store name.
/// </summary>
/// <param name="serviceProvider">The service provider used to create clients.</param>
/// <param name="registrations">The registered blob-store clients.</param>
/// <example>
/// <code>
/// var client = factory.CreateClient("reports");
/// </code>
/// </example>
public sealed class BlobStoreClientFactory(
    IServiceProvider serviceProvider,
    IEnumerable<BlobStoreClientRegistration> registrations) : IBlobStoreClientFactory
{
    private readonly IReadOnlyList<BlobStoreClientRegistration> registrations = ValidateRegistrations(registrations);

    /// <inheritdoc />
    public IBlobStoreClient CreateClient(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Blob store client name must not be null or whitespace.", nameof(name));
        }

        var registration = this.registrations.FirstOrDefault(e =>
            string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        if (registration is null)
        {
            throw new InvalidOperationException($"Blob store client '{name}' is not registered.");
        }

        return serviceProvider.GetRequiredKeyedService<IBlobStoreClient>(registration.Name);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<BlobStoreClientRegistration> GetRegistrations() => this.registrations;

    private static IReadOnlyList<BlobStoreClientRegistration> ValidateRegistrations(
        IEnumerable<BlobStoreClientRegistration> registrations)
    {
        var items = registrations?
            .OrderBy(registration => registration.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        var duplicate = items
            .GroupBy(registration => registration.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Blob store client '{duplicate.Key}' is registered more than once.");
        }

        return items;
    }
}
