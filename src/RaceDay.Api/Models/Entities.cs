namespace RaceDay.Api.Models;

/// <summary>Lookup table for the two system roles (Organiser, Participant).</summary>
public class Role
{
    public int RoleId { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<User> Users { get; set; } = new List<User>();
}

/// <summary>A registered person. Their role decides what they can do in the system.</summary>
public class User
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTime DateOfBirth { get; set; }
    public int RoleId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Role? Role { get; set; }
    public ICollection<Event> OrganisedEvents { get; set; } = new List<Event>();
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

/// <summary>Lookup table for event types: Run, Walk, Cycle.</summary>
public class EventType
{
    public int EventTypeId { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Event> Events { get; set; } = new List<Event>();
}

/// <summary>A road event (race, walk or ride) created by an Organiser.</summary>
public class Event
{
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public int EventTypeId { get; set; }
    public int OrganiserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public EventType? EventType { get; set; }
    public User? Organiser { get; set; }
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

/// <summary>An age or distance category within an event, e.g. "Under 20" or "21km".</summary>
public class Category
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public decimal? DistanceKm { get; set; }
    public decimal EntryFee { get; set; }
    public int? MaxParticipants { get; set; }

    public Event? Event { get; set; }
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

/// <summary>Links a Participant to an event and the category they selected.</summary>
public class Enrolment
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public int EventId { get; set; }
    public int CategoryId { get; set; }
    public DateTime EnrolmentDate { get; set; }
    public string Status { get; set; } = EnrolmentStatus.Registered;

    public User? Participant { get; set; }
    public Event? Event { get; set; }
    public Category? Category { get; set; }
    public Result? Result { get; set; }
}

/// <summary>The finish time and position captured by an Organiser for one enrolment.</summary>
public class Result
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public TimeSpan FinishTime { get; set; }
    public int Position { get; set; }
    public DateTime RecordedAt { get; set; }

    public Enrolment? Enrolment { get; set; }
}

public static class RoleNames
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";

    public const int OrganiserId = 1;
    public const int ParticipantId = 2;
}

public static class EnrolmentStatus
{
    public const string Registered = "Registered";
    public const string Completed = "Completed";
}
