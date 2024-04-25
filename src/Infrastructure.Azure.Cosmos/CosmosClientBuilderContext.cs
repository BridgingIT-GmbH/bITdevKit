// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

public class CosmosClientBuilderContext
{
    public CosmosClientBuilderContext(
        IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        IConfiguration configuration = null,
        string connectionString = null)
    {
        this.Services = services;
        this.Lifetime = lifetime;
        this.Configuration = configuration;
        this.ConnectionString = connectionString;
    }

    public IServiceCollection Services { get; }

    public ServiceLifetime Lifetime { get; }

    public IConfiguration Configuration { get; }

    public string ConnectionString { get; }

    public string AccountName
    {
        get
        {
            if (!string.IsNullOrEmpty(this.ConnectionString))
            {
                var match = Regex.Match(this.ConnectionString, @"(?i)AccountEndpoint=https://(.*?)(:|/|;)");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            return string.Empty;
        }
    }
}