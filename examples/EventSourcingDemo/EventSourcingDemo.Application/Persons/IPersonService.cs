// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Application.Persons;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Model;

public interface IPersonService
{
    Task<PersonOverviewViewModel> CreatePersonAsync(CreatePersonViewModel model);

    Task<PersonOverviewViewModel> ChangeSurnameAsync(ChangeSurnameViewModel model, CancellationToken cancellationToken);

    Task<IEnumerable<PersonOverviewViewModel>> GetAllPersonsAsync();

    Task<IEnumerable<PersonOverviewViewModel>> GetAllPersonsAsync(string firstname, string lastname, int skip, int take);

    Task DeactivateAsync(Guid id);

    Task<Person> ReplayPersonAsync(Guid id);
}