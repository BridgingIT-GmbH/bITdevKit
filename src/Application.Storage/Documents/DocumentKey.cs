// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Diagnostics;

#pragma warning disable SA1313 // Parameter names should begin with lower-case letter

[DebuggerDisplay("PartitionKey={PartitionKey}, RowKey={RowKey}")]
public readonly record struct DocumentKey(string PartitionKey, string RowKey);

#pragma warning restore SA1313 // Parameter names should begin with lower-case letter