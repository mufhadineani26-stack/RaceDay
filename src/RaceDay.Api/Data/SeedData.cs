using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// Realistic sample data matching the Part 1 INSERT statements.
/// All seeded accounts use the password: RaceDay@123
/// </summary>
public static class SeedData
{
    public const string DemoPassword = "RaceDay@123";

    // Pre-computed with ASP.NET Core's PasswordHasher (PBKDF2, salted).
    // HasData needs fixed values, so the hash cannot be generated at runtime.
    public const string DemoPasswordHash =
        "AQAAAAIAAYagAAAAEDESsOu1bU2ig74hOdIybRXUCbWNUJpLtoqZoymoD4foTsnPnTXAyW8COmrw+2/oBA==";

    private static readonly DateTime Seeded = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    public static void Apply(ModelBuilder mb)
    {
        mb.Entity<Role>().HasData(
            new Role { RoleId = RoleNames.OrganiserId, Name = RoleNames.Organiser },
            new Role { RoleId = RoleNames.ParticipantId, Name = RoleNames.Participant });

        mb.Entity<EventType>().HasData(
            new EventType { EventTypeId = 1, Name = "Run" },
            new EventType { EventTypeId = 2, Name = "Walk" },
            new EventType { EventTypeId = 3, Name = "Cycle" });

        mb.Entity<User>().HasData(
            // Organisers
            new User { UserId = 1, FirstName = "Sipho", LastName = "Ndlovu", Email = "sipho.ndlovu@raceday.co.za",
                PasswordHash = DemoPasswordHash, PhoneNumber = "0821234567", DateOfBirth = new DateTime(1985, 6, 15),
                RoleId = RoleNames.OrganiserId, CreatedAt = Seeded },
            new User { UserId = 2, FirstName = "Lerato", LastName = "Mokoena", Email = "lerato.mokoena@raceday.co.za",
                PasswordHash = DemoPasswordHash, PhoneNumber = "0837654321", DateOfBirth = new DateTime(1990, 2, 3),
                RoleId = RoleNames.OrganiserId, CreatedAt = Seeded },
            // Participants
            new User { UserId = 3, FirstName = "Thabo", LastName = "Khumalo", Email = "thabo.khumalo@gmail.com",
                PasswordHash = DemoPasswordHash, PhoneNumber = "0761112233", DateOfBirth = new DateTime(1995, 4, 12),
                RoleId = RoleNames.ParticipantId, CreatedAt = Seeded },
            new User { UserId = 4, FirstName = "Ayesha", LastName = "Patel", Email = "ayesha.patel@gmail.com",
                PasswordHash = DemoPasswordHash, PhoneNumber = "0794445566", DateOfBirth = new DateTime(2008, 8, 20),
                RoleId = RoleNames.ParticipantId, CreatedAt = Seeded },
            new User { UserId = 5, FirstName = "Johan", LastName = "van der Merwe", Email = "johan.vdmerwe@gmail.com",
                PasswordHash = DemoPasswordHash, PhoneNumber = "0727778899", DateOfBirth = new DateTime(1979, 1, 30),
                RoleId = RoleNames.ParticipantId, CreatedAt = Seeded });

        mb.Entity<Event>().HasData(
            new Event { EventId = 1, Name = "Soweto Marathon 2026",
                Description = "The People's Race through the streets of Soweto, finishing at FNB Stadium.",
                EventDate = new DateTime(2026, 11, 29, 6, 0, 0), Location = "FNB Stadium, Nasrec, Soweto",
                DistanceKm = 42.2m, EventTypeId = 1, OrganiserId = 1, CreatedAt = Seeded },
            new Event { EventId = 2, Name = "Cape Town Cycle Tour 2027",
                Description = "The world's largest individually timed cycle race around the Cape Peninsula.",
                EventDate = new DateTime(2027, 3, 14, 6, 15, 0), Location = "Cape Town Civic Centre, Cape Town",
                DistanceKm = 109m, EventTypeId = 3, OrganiserId = 2, CreatedAt = Seeded },
            new Event { EventId = 3, Name = "Jozi Spring Charity Walk",
                Description = "A relaxed community walk around Zoo Lake raising funds for local schools.",
                EventDate = new DateTime(2026, 9, 19, 8, 0, 0), Location = "Zoo Lake, Johannesburg",
                DistanceKm = 10m, EventTypeId = 2, OrganiserId = 1, CreatedAt = Seeded });

        mb.Entity<Category>().HasData(
            // Soweto Marathon - distance categories
            new Category { CategoryId = 1, EventId = 1, Name = "42.2km Marathon", DistanceKm = 42.2m, MinAge = 18, EntryFee = 250m, MaxParticipants = 5000 },
            new Category { CategoryId = 2, EventId = 1, Name = "21.1km Half Marathon", DistanceKm = 21.1m, MinAge = 16, EntryFee = 180m, MaxParticipants = 5000 },
            new Category { CategoryId = 3, EventId = 1, Name = "10km Fun Run", DistanceKm = 10m, EntryFee = 100m },
            // Cape Town Cycle Tour - age categories
            new Category { CategoryId = 4, EventId = 2, Name = "Senior (20-59)", MinAge = 20, MaxAge = 59, EntryFee = 850m },
            new Category { CategoryId = 5, EventId = 2, Name = "Veteran (60+)", MinAge = 60, EntryFee = 850m },
            new Category { CategoryId = 6, EventId = 2, Name = "Under 20", MinAge = 14, MaxAge = 19, EntryFee = 600m },
            // Jozi Spring Charity Walk - distance categories
            new Category { CategoryId = 7, EventId = 3, Name = "10km Walk", DistanceKm = 10m, EntryFee = 60m },
            new Category { CategoryId = 8, EventId = 3, Name = "5km Family Walk", DistanceKm = 5m, EntryFee = 40m });

        mb.Entity<Enrolment>().HasData(
            new Enrolment { EnrolmentId = 1, ParticipantId = 3, EventId = 1, CategoryId = 1, EnrolmentDate = Seeded, Status = EnrolmentStatus.Registered },
            new Enrolment { EnrolmentId = 2, ParticipantId = 4, EventId = 1, CategoryId = 3, EnrolmentDate = Seeded, Status = EnrolmentStatus.Registered },
            new Enrolment { EnrolmentId = 3, ParticipantId = 5, EventId = 2, CategoryId = 4, EnrolmentDate = Seeded, Status = EnrolmentStatus.Registered },
            new Enrolment { EnrolmentId = 4, ParticipantId = 3, EventId = 3, CategoryId = 7, EnrolmentDate = Seeded, Status = EnrolmentStatus.Completed },
            new Enrolment { EnrolmentId = 5, ParticipantId = 4, EventId = 3, CategoryId = 8, EnrolmentDate = Seeded, Status = EnrolmentStatus.Completed },
            new Enrolment { EnrolmentId = 6, ParticipantId = 5, EventId = 3, CategoryId = 7, EnrolmentDate = Seeded, Status = EnrolmentStatus.Completed });

        mb.Entity<Result>().HasData(
            new Result { ResultId = 1, EnrolmentId = 4, FinishTime = new TimeSpan(1, 42, 10), Position = 2, RecordedAt = new DateTime(2026, 9, 19, 14, 0, 0) },
            new Result { ResultId = 2, EnrolmentId = 5, FinishTime = new TimeSpan(0, 51, 30), Position = 1, RecordedAt = new DateTime(2026, 9, 19, 14, 0, 0) },
            new Result { ResultId = 3, EnrolmentId = 6, FinishTime = new TimeSpan(1, 38, 55), Position = 1, RecordedAt = new DateTime(2026, 9, 19, 14, 0, 0) });
    }
}
