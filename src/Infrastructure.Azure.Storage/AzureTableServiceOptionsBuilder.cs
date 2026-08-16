// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

/// <summary>
/// Builds azure table service options configuration.
/// </summary>
public class AzureTableServiceOptionsBuilder
    : OptionsBuilderBase<AzureTableServiceOptions, AzureTableServiceOptionsBuilder>
{
    /// <summary>
    /// Configures connection string.
    /// </summary>
    /// <param name="connectionString">The connection string used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public AzureTableServiceOptionsBuilder UseConnectionString(string connectionString)
    {
        this.Target.ConnectionString = connectionString;

        return this;
    }

    /// <summary>
    /// Executes the ignore server certificate validation operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public AzureTableServiceOptionsBuilder IgnoreServerCertificateValidation(bool value = true)
    {
        this.Target.IgnoreServerCertificateValidation = value;

        return this;
    }

    /// <summary>
    /// Executes the client options operation.
    /// </summary>
    /// <param name="options">The options controlling the operation.</param>
    /// <returns>The result of the operation.</returns>
    public AzureTableServiceOptionsBuilder ClientOptions(TableClientOptions options)
    {
        this.Target.ClientOptions = options;

        return this;
    }
}
