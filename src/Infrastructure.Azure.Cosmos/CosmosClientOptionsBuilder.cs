// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using Common;

/// <summary>
/// Builds cosmos client options configuration.
/// </summary>
public class CosmosClientOptionsBuilder : OptionsBuilderBase<CosmosClientOptions, CosmosClientOptionsBuilder>
{
    /// <summary>
    /// Configures connection string.
    /// </summary>
    /// <param name="connectionString">The connection string used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public CosmosClientOptionsBuilder UseConnectionString(string connectionString)
    {
        this.Target.ConnectionString = connectionString;

        return this;
    }

    /// <summary>
    /// Executes the ignore server certificate validation operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public CosmosClientOptionsBuilder IgnoreServerCertificateValidation(bool value = true)
    {
        this.Target.IgnoreServerCertificateValidation = value;

        return this;
    }

    /// <summary>
    /// Executes the client options operation.
    /// </summary>
    /// <param name="options">The options controlling the operation.</param>
    /// <returns>The result of the operation.</returns>
    public CosmosClientOptionsBuilder ClientOptions(Microsoft.Azure.Cosmos.CosmosClientOptions options)
    {
        this.Target.ClientOptions = options;

        return this;
    }
}
