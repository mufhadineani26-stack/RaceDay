using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Auth;

/// <summary>Keys used to store the logged-in user's state in the server-side session.</summary>
public static class SessionKeys
{
    public const string UserId = "RaceDay.UserId";
    public const string Role = "RaceDay.Role";
    public const string FullName = "RaceDay.FullName";
}

/// <summary>
/// Protects a controller or action using the server-side session.
/// <list type="bullet">
/// <item>No session (not logged in) → 401 Unauthorized.</item>
/// <item>Logged in but wrong role → 403 Forbidden.</item>
/// </list>
/// Usage: <c>[SessionAuthorize]</c> for any logged-in user,
/// <c>[SessionAuthorize(RoleNames.Organiser)]</c> for one role only.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    public string[] Roles { get; }

    public SessionAuthorizeAttribute(params string[] roles)
    {
        Roles = roles;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        var userId = session.GetInt32(SessionKeys.UserId);

        if (userId is null)
        {
            context.Result = new UnauthorizedObjectResult(
                new ApiMessage("You must be logged in to access this resource."));
            return;
        }

        if (Roles.Length == 0) return;

        var role = session.GetString(SessionKeys.Role);
        if (role is null || !Roles.Contains(role, StringComparer.OrdinalIgnoreCase))
        {
            context.Result = new ObjectResult(
                new ApiMessage($"Access denied. This action requires the {string.Join(" or ", Roles)} role."))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
