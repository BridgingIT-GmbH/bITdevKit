// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

public class AzureStorageBuilderContext(
    IServiceCollection services,
    ServiceLifetime lifetime = ServiceLifetime.Scoped,
    IConfiguration configuration = null,
    string connectionString = null,
    Service service = Service.Blob)
{
    public IServiceCollection Services { get; } = services;

    public ServiceLifetime Lifetime { get; } = lifetime;

    public IConfiguration Configuration { get; } = configuration;

    public string ConnectionString { get; } = connectionString;

    public Service Service { get; set; } = service;

    public string AccountName
    {
        get
        {
            if (!string.IsNullOrEmpty(this.ConnectionString))
            {
                var match = Regex.Match(this.ConnectionString, @"(?i)AccountName=(.*?)(:|/|;)", RegexOptions.None, new TimeSpan(0, 0, 3));
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            return string.Empty;
        }
    }
}

public enum Service
{
    Blob,
    Table,
    Queue
}
