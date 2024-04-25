// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Presentation.Web.Controllers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.EventSourcing.Outbox;
using BridgingIT.DevKit.Domain.EventSourcing.Store;
using EventSourcingDemo.Application.Persons;
using EventSourcingDemo.Domain.Model;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Person Controller.
/// </summary>
/// <remarks>Uses Event Sourcing to save and load a person. Every change on the event store triggers a projection to
/// the table person.</remarks>
[Route("api/[controller]")]
[ApiController]
public class PersonController
{
    private readonly IPersonService personService;
    private readonly IProjectionRequester<Person> personProjectionRequester;
    private readonly IOutboxWorkerService outboxWorkerService;

    public PersonController(IPersonService personService, IProjectionRequester<Person> personProjectionRequester, IOutboxWorkerService outboxWorkerService)
    {
        this.personService = personService;
        this.personProjectionRequester = personProjectionRequester;
        this.outboxWorkerService = outboxWorkerService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PersonOverviewViewModel>>> Get()
    {
        var persons = await this.personService.GetAllPersonsAsync().AnyContext();
        return persons.ToArray();
    }

    [HttpGet("{firstname}/{lastname}/{skip}/{take}")]
    public async Task<ActionResult<IEnumerable<PersonOverviewViewModel>>> Get(string firstname, string lastname, int skip, int take)
    {
        var persons = await this.personService.GetAllPersonsAsync(firstname, lastname, skip, take).AnyContext();
        return persons.ToArray();
    }

    [HttpGet("replay/{id}")]
    public async Task<ActionResult<Person>> GetReplay(Guid id)
    {
        return await this.personService.ReplayPersonAsync(id).AnyContext();
    }

    [HttpGet("startPersonProjection")]
    public async Task StartPersonProjection()
    {
        await this.personProjectionRequester.RequestProjectionAsync(CancellationToken.None).AnyContext();
    }

    [HttpGet("startOutboxWorker")]
    public async Task StartOutboxWorker()
    {
        await this.outboxWorkerService.DoWorkAsync().AnyContext();
    }

    [HttpPost]
    public async Task<PersonOverviewViewModel> CreatePersonAsync(CreatePersonViewModel model)
    {
        return await this.personService.CreatePersonAsync(model).AnyContext();
    }

    [HttpPut("ChangeSurname")]
    public async Task<PersonOverviewViewModel> ChangeSurname(ChangeSurnameViewModel model)
    {
        return await this.personService.ChangeSurnameAsync(model, CancellationToken.None).AnyContext();
    }

    [HttpDelete("{id}")]
    public async Task Deactivate(Guid id)
    {
        await this.personService.DeactivateAsync(id).AnyContext();
    }
}