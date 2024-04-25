// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

public class RepositoryOptions : IRepositoryOptions
{
    public RepositoryOptions(IEntityMapper mapper)
    {
        this.Mapper = mapper;
    }

    public IEntityMapper Mapper { get; set; }

    public bool Autosave { get; set; } = true;
}