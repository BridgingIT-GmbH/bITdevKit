// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using Common;

public class CosmosClientOptionsBuilder : OptionsBuilderBase<CosmosClientOptions, CosmosClientOptionsBuilder>
{
    public CosmosClientOptionsBuilder UseConnectionString(string connectionString)
    {
        this.Target.ConnectionString = connectionString;
        return this;
    }

    public CosmosClientOptionsBuilder IgnoreServerCertificateValidation(bool value = true)
    {
        this.Target.IgnoreServerCertificateValidation = value;
        return this;
    }

    public CosmosClientOptionsBuilder ClientOptions(Microsoft.Azure.Cosmos.CosmosClientOptions options)
    {
        this.Target.ClientOptions = options;
        return this;
    }
}