// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Optional internal path for serializers that can consume and produce complete buffers directly.</summary>
internal interface IBufferSerializer
{
    bool SupportsDirectBuffers { get; }

    byte[] SerializeToBytes(object value);

    object Deserialize(ReadOnlyMemory<byte> input, Type type);
}