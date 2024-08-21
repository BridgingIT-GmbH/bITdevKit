// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

public class CosmosClientBuilderContext(
    IServiceCollection services,
    ServiceLifetime lifetime = ServiceLifetime.Scoped,
    IConfiguration configuration = null,
    string connectionString = null)
{
    public IServiceCollection Services { get; } = services;

    public ServiceLifetime Lifetime { get; } = lifetime;

    public IConfiguration Configuration { get; } = configuration;

    public string ConnectionString { get; } = connectionString;

    public string AccountName
    {
        get
        {
            if (!string.IsNullOrEmpty(this.ConnectionString))
            {
                var match = Regex.Match(this.ConnectionString, @"(?i)AccountEndpoint=https://(.*?)(:|/|;)", RegexOptions.None, new TimeSpan(0, 0, 3));
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            return string.Empty;
        }
    }
}