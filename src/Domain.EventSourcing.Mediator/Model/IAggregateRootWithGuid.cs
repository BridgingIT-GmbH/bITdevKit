// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Model;

using System;
using BridgingIT.DevKit.Domain.Model;

public interface IAggregateRootWithGuid : IEntity<Guid>, IAggregateRoot
{
}