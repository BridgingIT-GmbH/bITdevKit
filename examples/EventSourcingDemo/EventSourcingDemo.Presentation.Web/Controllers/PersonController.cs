// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Presentation.Web.Controllers;

using Application.Persons;
using Common;
using DevKit.Domain.EventSourcing.Outbox;
using DevKit.Domain.EventSourcing.Store;
using Domain.Model;
using Microsoft.AspNetCore.Mvc;

/// <summary>
///     Person Controller.
/// </summary>
/// <remarks>
///     Uses Event Sourcing to save and load a person. Every change on the event store triggers a projection to
///     the table person.
/// </remarks>
[Route("api/[controller]")]
[ApiController]
public class PersonController(
    IPersonService personService,
    IProjectionRequester<Person> personProjectionRequester,
    IOutboxWorkerService outboxWorkerService)
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PersonOverviewViewModel>>> Get()
    {
        var persons = await personService.GetAllPersonsAsync().AnyContext();

        return persons.ToArray();
    }

    [HttpGet("{firstname}/{lastname}/{skip}/{take}")]
    public async Task<ActionResult<IEnumerable<PersonOverviewViewModel>>> Get(
        string firstname,
        string lastname,
        int skip,
        int take)
    {
        var persons = await personService.GetAllPersonsAsync(firstname, lastname, skip, take).AnyContext();

        return persons.ToArray();
    }

    [HttpGet("replay/{id}")]
    public async Task<ActionResult<Person>> GetReplay(Guid id)
    {
        return await personService.ReplayPersonAsync(id).AnyContext();
    }

    [HttpGet("startPersonProjection")]
    public async Task StartPersonProjection()
    {
        await personProjectionRequester.RequestProjectionAsync(CancellationToken.None).AnyContext();
    }

    [HttpGet("startOutboxWorker")]
    public async Task StartOutboxWorker()
    {
        await outboxWorkerService.DoWorkAsync().AnyContext();
    }

    [HttpPost]
    public async Task<PersonOverviewViewModel> CreatePersonAsync(CreatePersonViewModel model)
    {
        return await personService.CreatePersonAsync(model).AnyContext();
    }

    [HttpPut("ChangeSurname")]
    public async Task<PersonOverviewViewModel> ChangeSurname(ChangeSurnameViewModel model)
    {
        return await personService.ChangeSurnameAsync(model, CancellationToken.None).AnyContext();
    }

    [HttpDelete("{id}")]
    public async Task Deactivate(Guid id)
    {
        await personService.DeactivateAsync(id).AnyContext();
    }
}