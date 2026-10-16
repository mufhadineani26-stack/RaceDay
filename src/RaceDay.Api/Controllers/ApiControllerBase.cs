using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Auth;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Controllers;

/// <summary>Shared helpers for reading the logged-in user from the session.</summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Id of the logged-in user. Only call this behind [SessionAuthorize].</summary>
    protected int CurrentUserId =>
        HttpContext.Session.GetInt32(SessionKeys.UserId)
        ?? throw new InvalidOperationException("No user in session.");

    protected string? CurrentRole => HttpContext.Session.GetString(SessionKeys.Role);

    protected ObjectResult Forbidden(string message) =>
        StatusCode(StatusCodes.Status403Forbidden, new ApiMessage(message));
}
