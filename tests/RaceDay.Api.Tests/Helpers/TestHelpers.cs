using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Tests.Helpers;

/// <summary>In-memory ISession so controllers can be unit tested without the session middleware.</summary>
public class FakeSession : ISession
{
    private readonly Dictionary<string, byte[]> _store = new();

    public bool IsAvailable => true;
    public string Id { get; } = Guid.NewGuid().ToString();
    public IEnumerable<string> Keys => _store.Keys;

    public void Clear() => _store.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _store.Remove(key);
    public void Set(string key, byte[] value) => _store[key] = value;
    public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) => _store.TryGetValue(key, out value);
}

public static class TestDb
{
    /// <summary>Fresh, isolated in-memory database including the seed data.</summary>
    public static RaceDayDbContext Create()
    {
        var options = new DbContextOptionsBuilder<RaceDayDbContext>()
            .UseInMemoryDatabase($"RaceDayUnit_{Guid.NewGuid()}")
            .Options;
        var db = new RaceDayDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    // Seeded ids (see SeedData)
    public const int OrganiserSipho = 1;
    public const int OrganiserLerato = 2;
    public const int ParticipantThabo = 3;   // born 1995
    public const int ParticipantAyesha = 4;  // born 2008
    public const int ParticipantJohan = 5;   // born 1979
    public const int PastEventId = 3;        // Jozi Spring Charity Walk (organiser Sipho)

    /// <summary>Adds an event a set number of days from now, with one open category.</summary>
    public static (Event ev, Category cat) AddEvent(RaceDayDbContext db, int organiserId, int daysFromNow,
        int? minAge = null, int? maxAge = null, int? maxParticipants = null)
    {
        var ev = new Event
        {
            Name = $"Test Event {Guid.NewGuid():N}".Substring(0, 30),
            Description = "Test event",
            EventDate = DateTime.Now.AddDays(daysFromNow),
            Location = "Johannesburg",
            DistanceKm = 10,
            EventTypeId = 1,
            OrganiserId = organiserId,
            CreatedAt = DateTime.UtcNow
        };
        db.Events.Add(ev);
        db.SaveChanges();

        var cat = new Category
        {
            EventId = ev.EventId, Name = "10km", DistanceKm = 10, EntryFee = 100,
            MinAge = minAge, MaxAge = maxAge, MaxParticipants = maxParticipants
        };
        db.Categories.Add(cat);
        db.SaveChanges();
        return (ev, cat);
    }

    public static IPasswordHasher<User> Hasher() => new PasswordHasher<User>();
}

public static class ControllerSetup
{
    /// <summary>Attaches an HttpContext with a session. Pass a userId/role to simulate a logged-in user.</summary>
    public static T WithSession<T>(this T controller, int? userId = null, string? role = null) where T : ControllerBase
    {
        var http = new DefaultHttpContext { Session = new FakeSession() };
        if (userId.HasValue) http.Session.SetInt32(SessionKeys.UserId, userId.Value);
        if (role is not null) http.Session.SetString(SessionKeys.Role, role);
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    public static int? StatusOf(IActionResult result) => result switch
    {
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        _ => null
    };

    public static T ValueOf<T>(IActionResult result) =>
        Assert.IsAssignableFrom<T>(Assert.IsAssignableFrom<ObjectResult>(result).Value);
}
