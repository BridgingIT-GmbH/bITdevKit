// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Registration;

[AttributeUsage(AttributeTargets.Class)]
public class ImmutableNameAttribute(string immutableName) : Attribute
{
    public string ImmutableName { get; } = immutableName;
}