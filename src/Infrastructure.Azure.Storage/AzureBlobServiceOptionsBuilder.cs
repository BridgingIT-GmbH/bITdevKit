// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using Common;
using global::Azure.Storage.Blobs;

public class AzureBlobServiceOptionsBuilder
    : OptionsBuilderBase<AzureBlobServiceOptions, AzureBlobServiceOptionsBuilder>
{
    public AzureBlobServiceOptionsBuilder UseConnectionString(string connectionString)
    {
        this.Target.ConnectionString = connectionString;
        return this;
    }

    public AzureBlobServiceOptionsBuilder IgnoreServerCertificateValidation(bool value = true)
    {
        this.Target.IgnoreServerCertificateValidation = value;
        return this;
    }

    public AzureBlobServiceOptionsBuilder ClientOptions(BlobClientOptions options)
    {
        this.Target.ClientOptions = options;
        return this;
    }
}