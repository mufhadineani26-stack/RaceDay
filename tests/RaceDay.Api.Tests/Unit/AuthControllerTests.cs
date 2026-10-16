using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Tests.Helpers;
using static RaceDay.Api.Tests.Helpers.ControllerSetup;

namespace RaceDay.Api.Tests.Unit;

public class AuthControllerTests
{
    private static RegisterRequest NewRegistration(string role = RoleNames.Participant, string email = "naledi.dlamini@gmail.com") => new()
    {
        FirstName = "Naledi",
        LastName = "Dlamini",
        Email = email,
        Password = "Str0ngPass!",
        PhoneNumber = "0821112222",
        DateOfBirth = new DateTime(1998, 5, 21),
        Role = role
    };

    [Theory]
    [InlineData(RoleNames.Participant)]
    [InlineData(RoleNames.Organiser)]
    [InlineData("participant")] // role is matched case-insensitively
    public async Task Register_WithValidDetails_Returns201_AndAssignsRole(string role)
    {
        using var db = TestDb.Create();
        var controller = new AuthController(db, TestDb.Hasher()).WithSession();

        var result = await controller.Register(NewRegistration(role));

        Assert.Equal(StatusCodes.Status201Created, StatusOf(result));
        var dto = ValueOf<SessionUserDto>(result);
        Assert.Equal(role, dto.Role, ignoreCase: true);
        Assert.True(await db.Users.AnyAsync(u => u.Email == "naledi.dlamini@gmail.com"));
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotPlainText()
    {
        using var db = TestDb.Create();
        var hasher = TestDb.Hasher();
        var controller = new AuthController(db, hasher).WithSession();

        await controller.Register(NewRegistration());

        var user = await db.Users.SingleAsync(u => u.Email == "naledi.dlamini@gmail.com");
        Assert.NotEqual("Str0ngPass!", user.PasswordHash);
        Assert.DoesNotContain("Str0ngPass!", user.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, user.PasswordHash, "Str0ngPass!"));
    }

    [Fact]
    public async Task Register_WithExistingEmail_Returns409()
    {
        using var db = TestDb.Create();
        var controller = new AuthController(db, TestDb.Hasher()).WithSession();

        // Seeded participant's email, different casing
        var result = await controller.Register(NewRegistration(email: "THABO.khumalo@gmail.com"));

        Assert.Equal(StatusCodes.Status409Conflict, StatusOf(result));
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("")]
    [InlineData("Spectator")]
    public async Task Register_WithInvalidRole_Returns400(string role)
    {
        using var db = TestDb.Create();
        var controller = new AuthController(db, TestDb.Hasher()).WithSession();

        var result = await controller.Register(NewRegistration(role));

        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(result));
        Assert.False(await db.Users.AnyAsync(u => u.Email == "naledi.dlamini@gmail.com"));
    }

    [Fact]
    public async Task Register_WithFutureDateOfBirth_Returns400()
    {
        using var db = TestDb.Create();
        var controller = new AuthController(db, TestDb.Hasher()).WithSession();
        var request = NewRegistration();
        request.DateOfBirth = DateTime.Today.AddDays(1);

        var result = await controller.Register(request);

        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(result));
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200_AndStoresUserAndRoleInSession()
    {
        using var db = TestDb.Create();
        var controller = new AuthController(db, TestDb.Hasher()).WithSession();

        var result = await controller.Login(new LoginRequest { Email = "sipho.ndlovu@raceday.co.za", Password = SeedData.DemoPassword });

        Assert.Equal(StatusCodes.Status200OK, StatusOf(result));
        var dto = ValueOf<SessionUserDto>(result);
        Assert.Equal(RoleNames.Organiser, dto.Role);

        var session = controller.HttpContext.Session;
        Assert.Equal(TestDb.OrganiserSipho, session.GetInt32(SessionKeys.UserId));
        Assert.Equal(RoleNames.Organiser, session.GetString(SessionKeys.Role));
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401_AndNoSession()
    {
        using var db = TestDb.Create();
        var controller = new AuthController(db, TestDb.Hasher()).WithSession();

        var result = await controller.Login(new LoginRequest { Email = "thabo.khumalo@gmail.com", Password = "wrong-password" });

        Assert.Equal(StatusCodes.Status401Unauthorized, StatusOf(result));
        Assert.Null(controller.HttpContext.Session.GetInt32(SessionKeys.UserId));
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        using var db = TestDb.Create();
        var controller = new AuthController(db, TestDb.Hasher()).WithSession();

        var result = await controller.Login(new LoginRequest { Email = "nobody@nowhere.co.za", Password = SeedData.DemoPassword });

        Assert.Equal(StatusCodes.Status401Unauthorized, StatusOf(result));
    }

    [Fact]
    public async Task Register_ThenLogin_WithNewAccount_Succeeds()
    {
        using var db = TestDb.Create();
        var controller = new AuthController(db, TestDb.Hasher()).WithSession();

        await controller.Register(NewRegistration());
        var result = await controller.Login(new LoginRequest { Email = "naledi.dlamini@gmail.com", Password = "Str0ngPass!" });

        Assert.Equal(StatusCodes.Status200OK, StatusOf(result));
        Assert.Equal(RoleNames.Participant, controller.HttpContext.Session.GetString(SessionKeys.Role));
    }

    [Fact]
    public void Logout_ClearsTheSession()
    {
        using var db = TestDb.Create();
        var controller = new AuthController(db, TestDb.Hasher()).WithSession(TestDb.ParticipantThabo, RoleNames.Participant);

        var result = controller.Logout();

        Assert.Equal(StatusCodes.Status200OK, StatusOf(result));
        Assert.Null(controller.HttpContext.Session.GetInt32(SessionKeys.UserId));
        Assert.Null(controller.HttpContext.Session.GetString(SessionKeys.Role));
    }
}
