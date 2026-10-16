using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Tests.Helpers;
using static RaceDay.Api.Tests.Helpers.ControllerSetup;

namespace RaceDay.Api.Tests.Unit;

public class ProfileControllerTests
{
    [Theory]
    [InlineData(TestDb.OrganiserSipho, RoleNames.Organiser)]
    [InlineData(TestDb.ParticipantThabo, RoleNames.Participant)]
    public async Task GetProfile_ReturnsOwnProfile_ForBothRoles(int userId, string role)
    {
        using var db = TestDb.Create();
        var controller = new ProfileController(db, TestDb.Hasher()).WithSession(userId, role);

        var result = await controller.GetProfile();

        var dto = ValueOf<ProfileDto>(result);
        Assert.Equal(userId, dto.UserId);
        Assert.Equal(role, dto.Role);
    }

    [Fact]
    public async Task UpdateProfile_ChangesDetails_ButNotEmailOrRole()
    {
        using var db = TestDb.Create();
        var controller = new ProfileController(db, TestDb.Hasher()).WithSession(TestDb.ParticipantThabo, RoleNames.Participant);

        var result = await controller.UpdateProfile(new UpdateProfileRequest
        {
            FirstName = "Thabang", LastName = "Khumalo", PhoneNumber = "0710000000", DateOfBirth = new DateTime(1995, 4, 12)
        });

        Assert.Equal(StatusCodes.Status200OK, StatusOf(result));
        var user = await db.Users.SingleAsync(u => u.UserId == TestDb.ParticipantThabo);
        Assert.Equal("Thabang", user.FirstName);
        Assert.Equal("0710000000", user.PhoneNumber);
        Assert.Equal("thabo.khumalo@gmail.com", user.Email);
        Assert.Equal(RoleNames.ParticipantId, user.RoleId);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_Returns400()
    {
        using var db = TestDb.Create();
        var controller = new ProfileController(db, TestDb.Hasher()).WithSession(TestDb.ParticipantThabo, RoleNames.Participant);

        var result = await controller.ChangePassword(new ChangePasswordRequest { CurrentPassword = "nope", NewPassword = "NewPass123!" });

        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(result));
    }

    [Fact]
    public async Task ChangePassword_WithCorrectCurrentPassword_AllowsLoginWithNewPassword()
    {
        using var db = TestDb.Create();
        var hasher = TestDb.Hasher();
        var profile = new ProfileController(db, hasher).WithSession(TestDb.ParticipantThabo, RoleNames.Participant);

        var change = await profile.ChangePassword(new ChangePasswordRequest { CurrentPassword = SeedData.DemoPassword, NewPassword = "NewPass123!" });
        var login = await new AuthController(db, hasher).WithSession()
            .Login(new LoginRequest { Email = "thabo.khumalo@gmail.com", Password = "NewPass123!" });

        Assert.Equal(StatusCodes.Status200OK, StatusOf(change));
        Assert.Equal(StatusCodes.Status200OK, StatusOf(login));
    }
}
