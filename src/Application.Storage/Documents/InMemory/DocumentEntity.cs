// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

public class DocumentEntity
{
    public string PartitionKey { get; set; }

    public string RowKey { get; set; }

    public string Type { get; set; }

    public object Content { get; set; }

    public DateTimeOffset CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTimeOffset UpdatedDate { get; set; } = DateTime.UtcNow;
}