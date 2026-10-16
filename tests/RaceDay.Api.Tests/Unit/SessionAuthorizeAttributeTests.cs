using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using RaceDay.Api.Auth;
using RaceDay.Api.Models;
using RaceDay.Api.Tests.Helpers;

namespace RaceDay.Api.Tests.Unit;

/// <summary>Verifies the role-based access rules enforced on every protected endpoint.</summary>
public class SessionAuthorizeAttributeTests
{
    private static AuthorizationFilterContext ContextFor(int? userId, string? role)
    {
        var http = new DefaultHttpContext { Session = new FakeSession() };
        if (userId.HasValue) http.Session.SetInt32(SessionKeys.UserId, userId.Value);
        if (role is not null) http.Session.SetString(SessionKeys.Role, role);

        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    [Fact]
    public void NotLoggedIn_IsRejectedWith401()
    {
        var context = ContextFor(null, null);

        new SessionAuthorizeAttribute().OnAuthorization(context);

        Assert.IsType<UnauthorizedObjectResult>(context.Result);
    }

    [Fact]
    public void NotLoggedIn_OnRoleEndpoint_IsRejectedWith401_Not403()
    {
        var context = ContextFor(null, null);

        new SessionAuthorizeAttribute(RoleNames.Organiser).OnAuthorization(context);

        Assert.IsType<UnauthorizedObjectResult>(context.Result);
    }

    [Theory]
    [InlineData(RoleNames.Organiser)]
    [InlineData(RoleNames.Participant)]
    public void AnyLoggedInUser_IsAllowed_WhenNoRoleRequired(string role)
    {
        var context = ContextFor(1, role);

        new SessionAuthorizeAttribute().OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void Participant_OnOrganiserEndpoint_IsRejectedWith403()
    {
        var context = ContextFor(TestDb.ParticipantThabo, RoleNames.Participant);

        new SessionAuthorizeAttribute(RoleNames.Organiser).OnAuthorization(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public void Organiser_OnParticipantEndpoint_IsRejectedWith403()
    {
        var context = ContextFor(TestDb.OrganiserSipho, RoleNames.Organiser);

        new SessionAuthorizeAttribute(RoleNames.Participant).OnAuthorization(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.Organiser)]
    [InlineData(RoleNames.Participant)]
    public void MatchingRole_IsAllowed(string role)
    {
        var context = ContextFor(1, role);

        new SessionAuthorizeAttribute(role).OnAuthorization(context);

        Assert.Null(context.Result);
    }
}
