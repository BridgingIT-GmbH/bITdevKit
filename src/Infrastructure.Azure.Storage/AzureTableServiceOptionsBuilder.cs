﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

public class AzureTableServiceOptionsBuilder
    : OptionsBuilderBase<AzureTableServiceOptions, AzureTableServiceOptionsBuilder>
{
    public AzureTableServiceOptionsBuilder UseConnectionString(string connectionString)
    {
        this.Target.ConnectionString = connectionString;

        return this;
    }

    public AzureTableServiceOptionsBuilder IgnoreServerCertificateValidation(bool value = true)
    {
        this.Target.IgnoreServerCertificateValidation = value;

        return this;
    }

    public AzureTableServiceOptionsBuilder ClientOptions(TableClientOptions options)
    {
        this.Target.ClientOptions = options;

        return this;
    }
}