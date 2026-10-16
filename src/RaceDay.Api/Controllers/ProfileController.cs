using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>View and update the logged-in user's own profile (both roles).</summary>
[ApiController]
[Route("api/profile")]
[Tags("User Profile")]
[SessionAuthorize]
public class ProfileController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public ProfileController(RaceDayDbContext db, IPasswordHasher<User> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    /// <summary>Returns the logged-in user's profile.</summary>
    /// <response code="200">The user's profile.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">The account no longer exists.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile()
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null) return NotFound(new ApiMessage("User not found."));
        return Ok(ToDto(user));
    }

    /// <summary>Updates the logged-in user's personal details.</summary>
    /// <remarks>Email and role cannot be changed here. Email identifies the account and role is fixed at registration.</remarks>
    /// <response code="200">The updated profile.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">The account no longer exists.</response>
    [HttpPut]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        if (request.DateOfBirth.Date >= DateTime.Today)
            return BadRequest(new ApiMessage("Date of birth must be in the past."));

        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null) return NotFound(new ApiMessage("User not found."));

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = request.PhoneNumber?.Trim();
        user.DateOfBirth = request.DateOfBirth.Date;
        await _db.SaveChangesAsync();

        HttpContext.Session.SetString(SessionKeys.FullName, $"{user.FirstName} {user.LastName}");
        return Ok(ToDto(user));
    }

    /// <summary>Changes the logged-in user's password.</summary>
    /// <response code="200">Password changed.</response>
    /// <response code="400">Current password is wrong or the new one is invalid.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPut("password")]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null) return NotFound(new ApiMessage("User not found."));

        if (_hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            return BadRequest(new ApiMessage("Current password is incorrect."));

        user.PasswordHash = _hasher.HashPassword(user, request.NewPassword);
        await _db.SaveChangesAsync();
        return Ok(new ApiMessage("Password changed successfully."));
    }

    private static ProfileDto ToDto(User u) =>
        new(u.UserId, u.FirstName, u.LastName, u.Email, u.PhoneNumber, u.DateOfBirth, u.Role!.Name, u.CreatedAt);
}
