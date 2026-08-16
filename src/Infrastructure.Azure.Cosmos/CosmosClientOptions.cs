// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using Common;

/// <summary>
/// Configures cosmos client.
/// </summary>
public class CosmosClientOptions : OptionsBase
{
    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public virtual string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the ignore server certificate validation.
    /// </summary>
    public bool IgnoreServerCertificateValidation { get; set; } = true;

    /// <summary>
    /// Gets or sets the client options.
    /// </summary>
    public virtual Microsoft.Azure.Cosmos.CosmosClientOptions ClientOptions { get; set; }
}
