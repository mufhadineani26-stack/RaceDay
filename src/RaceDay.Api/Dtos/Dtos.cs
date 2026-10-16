using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

/// <summary>Simple message body used for errors and confirmations.</summary>
public record ApiMessage(string Message);

// ===================== Authentication =====================

public class RegisterRequest
{
    /// <example>Naledi</example>
    [Required, StringLength(50)] public string FirstName { get; set; } = string.Empty;
    /// <example>Dlamini</example>
    [Required, StringLength(50)] public string LastName { get; set; } = string.Empty;
    /// <example>naledi.dlamini@gmail.com</example>
    [Required, EmailAddress, StringLength(100)] public string Email { get; set; } = string.Empty;
    /// <summary>At least 8 characters.</summary>
    /// <example>RaceDay@123</example>
    [Required, StringLength(100, MinimumLength = 8)] public string Password { get; set; } = string.Empty;
    /// <example>0821234567</example>
    [Phone, StringLength(20)] public string? PhoneNumber { get; set; }
    /// <example>1998-05-21</example>
    [Required] public DateTime DateOfBirth { get; set; }
    /// <summary>Either "Organiser" or "Participant".</summary>
    /// <example>Participant</example>
    [Required] public string Role { get; set; } = string.Empty;
}

public class LoginRequest
{
    /// <example>thabo.khumalo@gmail.com</example>
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    /// <example>RaceDay@123</example>
    [Required] public string Password { get; set; } = string.Empty;
}

/// <summary>Who is logged in for the current session.</summary>
public record SessionUserDto(int UserId, string FullName, string Email, string Role);

// ===================== Profile =====================

public record ProfileDto(
    int UserId, string FirstName, string LastName, string Email,
    string? PhoneNumber, DateTime DateOfBirth, string Role, DateTime CreatedAt);

public class UpdateProfileRequest
{
    [Required, StringLength(50)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(50)] public string LastName { get; set; } = string.Empty;
    [Phone, StringLength(20)] public string? PhoneNumber { get; set; }
    [Required] public DateTime DateOfBirth { get; set; }
}

public class ChangePasswordRequest
{
    [Required] public string CurrentPassword { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 8)] public string NewPassword { get; set; } = string.Empty;
}

// ===================== Events =====================

public record EventTypeDto(int EventTypeId, string Name);

public record EventDto(
    int EventId, string Name, string Description, DateTime EventDate, string Location,
    decimal DistanceKm, string EventType, int OrganiserId, string OrganiserName,
    int CategoryCount, int EnrolmentCount);

public class EventRequest
{
    /// <example>Mandela Day Fun Run</example>
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    /// <example>A 10km community run along the Durban beachfront.</example>
    [Required, StringLength(1000)] public string Description { get; set; } = string.Empty;
    /// <example>2027-07-18T07:00:00</example>
    [Required] public DateTime EventDate { get; set; }
    /// <example>Moses Mabhida Stadium, Durban</example>
    [Required, StringLength(150)] public string Location { get; set; } = string.Empty;
    /// <example>10</example>
    [Range(0.1, 1000)] public decimal DistanceKm { get; set; }
    /// <summary>One of: Run, Walk, Cycle.</summary>
    /// <example>Run</example>
    [Required] public string EventType { get; set; } = string.Empty;
}

// ===================== Categories =====================

public record CategoryDto(
    int CategoryId, int EventId, string Name, string? Description, int? MinAge, int? MaxAge,
    decimal? DistanceKm, decimal EntryFee, int? MaxParticipants, int EnrolledCount);

public class CategoryRequest
{
    /// <example>Under 20</example>
    [Required, StringLength(50)] public string Name { get; set; } = string.Empty;
    [StringLength(250)] public string? Description { get; set; }
    /// <example>14</example>
    [Range(0, 120)] public int? MinAge { get; set; }
    /// <example>19</example>
    [Range(0, 120)] public int? MaxAge { get; set; }
    [Range(0.1, 1000)] public decimal? DistanceKm { get; set; }
    /// <example>150</example>
    [Range(0, 100000)] public decimal EntryFee { get; set; }
    [Range(1, 100000)] public int? MaxParticipants { get; set; }
}

// ===================== Enrolments =====================

public class EnrolRequest
{
    /// <example>2</example>
    [Required] public int CategoryId { get; set; }
}

public record EnrolmentDto(
    int EnrolmentId, int EventId, string EventName, DateTime EventDate, string Location,
    int CategoryId, string CategoryName, int ParticipantId, string ParticipantName,
    string ParticipantEmail, DateTime EnrolmentDate, string Status, bool HasResult);

// ===================== Results =====================

public class ResultRequest
{
    /// <summary>Finish time in hh:mm:ss format.</summary>
    /// <example>01:45:32</example>
    [Required, RegularExpression(@"^\d{1,2}:[0-5]\d:[0-5]\d$", ErrorMessage = "FinishTime must be in hh:mm:ss format.")]
    public string FinishTime { get; set; } = string.Empty;
    /// <example>1</example>
    [Range(1, 100000)] public int Position { get; set; }
}

public record ResultDto(
    int ResultId, int EnrolmentId, int EventId, string EventName, DateTime EventDate,
    string CategoryName, int ParticipantId, string ParticipantName,
    string FinishTime, int Position, DateTime RecordedAt);
