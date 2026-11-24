namespace Presentation.TestHost.ProblemDetails;

using System.ComponentModel.DataAnnotations;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Mvc;

/// <summary>Provides MVC responses for the Problem Details HTTP fixture.</summary>
[ApiController]
[Route(ProblemDetailsScenario.ApiPrefix + "/mvc")]
public sealed class ProblemDetailsController : ControllerBase
{
    /// <summary>Throws the fixture validation exception.</summary>
    [HttpGet("throw/validation")]
    public IActionResult ThrowValidation() => throw ProblemDetailsScenario.CreateValidationException();

    /// <summary>Maps the fixture validation Result through MVC.</summary>
    [HttpGet("result/validation")]
    public IActionResult ResultValidation() => new DefaultActionResultMapper().Ok(ProblemDetailsScenario.CreateValidationResult());

    /// <summary>Throws the fixture server exception.</summary>
    [HttpGet("throw/server")]
    public IActionResult ThrowServer() => throw ProblemDetailsScenario.CreateServerException();

    /// <summary>Maps the fixture server Result through MVC.</summary>
    [HttpGet("result/server")]
    public IActionResult ResultServer() => new DefaultActionResultMapper().Ok(ProblemDetailsScenario.CreateServerResult());

    /// <summary>Exercises MVC automatic model validation.</summary>
    [HttpPost("model-state")]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json")]
    public IActionResult ValidateItem([FromBody] InventoryItem item) => this.Ok(item);

    /// <summary>Represents a request model with a required inventory name.</summary>
    public sealed record InventoryItem([Required] string Name);
}
