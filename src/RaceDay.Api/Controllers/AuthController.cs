using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Registration, login and logout. Login state is kept in a server-side session.</summary>
[ApiController]
[Route("api/auth")]
[Tags("Authentication")]
public class AuthController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public AuthController(RaceDayDbContext db, IPasswordHasher<User> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    /// <summary>Registers a new Organiser or Participant account.</summary>
    /// <remarks>
    /// The role is chosen once at registration and is looked up in the Roles table.
    /// It cannot be changed afterwards through the profile endpoints.
    /// The password is hashed (PBKDF2 with a random salt) and never stored in plain text.
    /// </remarks>
    /// <response code="201">Account created. Returns the new user's summary.</response>
    /// <response code="400">Validation failed, or the role is not Organiser/Participant.</response>
    /// <response code="409">An account with this email already exists.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(SessionUserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // Only the two known roles may be self-selected (case-insensitive), then resolved from the Roles table.
        var roleName = new[] { RoleNames.Organiser, RoleNames.Participant }
            .FirstOrDefault(r => r.Equals(request.Role?.Trim(), StringComparison.OrdinalIgnoreCase));
        var role = roleName is null ? null : await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
        if (role is null)
            return BadRequest(new ApiMessage("Role must be either 'Organiser' or 'Participant'."));

        if (request.DateOfBirth.Date >= DateTime.Today)
            return BadRequest(new ApiMessage("Date of birth must be in the past."));

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new ApiMessage("An account with this email already exists."));

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PhoneNumber = request.PhoneNumber?.Trim(),
            DateOfBirth = request.DateOfBirth.Date,
            RoleId = role.RoleId,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created,
            new SessionUserDto(user.UserId, $"{user.FirstName} {user.LastName}", user.Email, role.Name));
    }

    /// <summary>Logs in with email and password and starts a session.</summary>
    /// <remarks>
    /// On success the server stores the user's id and role in the session and returns a
    /// session cookie (.RaceDay.Session). The browser (and Swagger UI) sends it automatically
    /// on every following request.
    /// </remarks>
    /// <response code="200">Logged in. Returns the user's id, name, email and role.</response>
    /// <response code="400">Email or password missing.</response>
    /// <response code="401">Email or password is incorrect.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(SessionUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email);

        if (user is null)
            return Unauthorized(new ApiMessage("Invalid email or password."));

        var check = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (check == PasswordVerificationResult.Failed)
            return Unauthorized(new ApiMessage("Invalid email or password."));

        if (check == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
            await _db.SaveChangesAsync();
        }

        var roleName = user.Role!.Name;
        var fullName = $"{user.FirstName} {user.LastName}";

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, roleName);
        HttpContext.Session.SetString(SessionKeys.FullName, fullName);

        return Ok(new SessionUserDto(user.UserId, fullName, user.Email, roleName));
    }

    /// <summary>Ends the current session.</summary>
    /// <response code="200">Logged out.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPost("logout")]
    [SessionAuthorize]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Ok(new ApiMessage("Logged out successfully."));
    }

    /// <summary>Returns who is logged in for the current session.</summary>
    /// <response code="200">The logged-in user's id, name, email and role.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet("me")]
    [SessionAuthorize]
    [ProducesResponseType(typeof(SessionUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me()
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null)
        {
            HttpContext.Session.Clear();
            return Unauthorized(new ApiMessage("Your account no longer exists. Please log in again."));
        }
        return Ok(new SessionUserDto(user.UserId, $"{user.FirstName} {user.LastName}", user.Email, user.Role!.Name));
    }
}
