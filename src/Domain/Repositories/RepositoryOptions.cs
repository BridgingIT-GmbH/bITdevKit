// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

public class RepositoryOptions(IEntityMapper mapper) : IRepositoryOptions
{
    public IEntityMapper Mapper { get; set; } = mapper;

    public bool Autosave { get; set; } = true;
}