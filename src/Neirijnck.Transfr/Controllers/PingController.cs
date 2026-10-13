using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Neirijnck.Transfr.Controllers;

/// <summary>
/// Scaffold check: proves routing, auth and the generated client end to end. Replaced by real endpoints in later steps.
/// </summary>
public class PingController : TransfrApiControllerBase
{
    [HttpGet("ping")]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    public IActionResult Ping() => this.Ok("pong");
}