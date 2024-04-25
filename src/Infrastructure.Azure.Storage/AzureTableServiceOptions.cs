// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using BridgingIT.DevKit.Common;
using global::Azure.Data.Tables;

public class AzureTableServiceOptions : OptionsBase
{
    public virtual string ConnectionString { get; set; }

    public bool IgnoreServerCertificateValidation { get; set; } = true;

    public virtual TableClientOptions ClientOptions { get; set; }
}