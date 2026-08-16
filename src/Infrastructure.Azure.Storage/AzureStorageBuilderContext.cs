// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Text.RegularExpressions;
using Configuration;

/// <summary>
/// Represents azure storage builder context.
/// </summary>
/// <param name="services">The service collection to configure.</param>
/// <param name="lifetime">The lifetime used by the operation.</param>
/// <param name="configuration">The configuration to apply.</param>
/// <param name="connectionString">The connection string used by the operation.</param>
/// <param name="service">The service used by the operation.</param>
public class AzureStorageBuilderContext(
    IServiceCollection services,
    ServiceLifetime lifetime = ServiceLifetime.Scoped,
    IConfiguration configuration = null,
    string connectionString = null,
    Service service = Service.Blob)
{
    /// <summary>
    /// Gets the services.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Gets the lifetime.
    /// </summary>
    public ServiceLifetime Lifetime { get; } = lifetime;

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    public IConfiguration Configuration { get; } = configuration;

    /// <summary>
    /// Gets the connection string.
    /// </summary>
    public string ConnectionString { get; } = connectionString;

    /// <summary>
    /// Gets or sets the service.
    /// </summary>
    public Service Service { get; set; } = service;

    /// <summary>
    /// Stores the account name.
    /// </summary>
    public string AccountName
    {
        get
        {
            if (!string.IsNullOrEmpty(this.ConnectionString))
            {
                var match = Regex.Match(this.ConnectionString,
                    @"(?i)AccountName=(.*?)(:|/|;)",
                    RegexOptions.None,
                    new TimeSpan(0, 0, 3));
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            return string.Empty;
        }
    }
}

/// <summary>
/// Defines the supported service values.
/// </summary>
public enum Service
{
    /// <summary>
    /// Represents the blob value.
    /// </summary>
    Blob,
    /// <summary>
    /// Represents the table value.
    /// </summary>
    Table,
    /// <summary>
    /// Represents the queue value.
    /// </summary>
    Queue
}
