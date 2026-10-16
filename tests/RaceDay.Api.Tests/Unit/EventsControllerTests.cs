using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Tests.Helpers;
using static RaceDay.Api.Tests.Helpers.ControllerSetup;

namespace RaceDay.Api.Tests.Unit;

public class EventsControllerTests
{
    private static EventRequest NewEvent(string type = "Run", int daysFromNow = 60) => new()
    {
        Name = "Durban Beachfront 10km",
        Description = "A flat and fast 10km along the Golden Mile.",
        EventDate = DateTime.Now.AddDays(daysFromNow),
        Location = "Moses Mabhida Stadium, Durban",
        DistanceKm = 10,
        EventType = type
    };

    private static EventsController AsOrganiser(RaceDayDbContext db, int organiserId = TestDb.OrganiserSipho) =>
        new EventsController(db).WithSession(organiserId, RoleNames.Organiser);

    [Fact]
    public async Task CreateEvent_AsOrganiser_Returns201_AndCapturesAllFields()
    {
        using var db = TestDb.Create();
        var controller = AsOrganiser(db);

        var result = await controller.CreateEvent(NewEvent("Cycle"));

        Assert.Equal(StatusCodes.Status201Created, StatusOf(result));
        var dto = ValueOf<EventDto>(result);
        Assert.Equal("Durban Beachfront 10km", dto.Name);
        Assert.Equal("Cycle", dto.EventType);
        Assert.Equal(10m, dto.DistanceKm);
        Assert.Equal(TestDb.OrganiserSipho, dto.OrganiserId);

        var saved = await db.Events.SingleAsync(e => e.EventId == dto.EventId);
        Assert.Equal("Moses Mabhida Stadium, Durban", saved.Location);
        Assert.Equal("A flat and fast 10km along the Golden Mile.", saved.Description);
    }

    [Theory]
    [InlineData("Swim")]
    [InlineData("")]
    public async Task CreateEvent_WithUnknownEventType_Returns400(string type)
    {
        using var db = TestDb.Create();

        var result = await AsOrganiser(db).CreateEvent(NewEvent(type));

        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(result));
    }

    [Fact]
    public async Task CreateEvent_WithPastDate_Returns400()
    {
        using var db = TestDb.Create();
        var before = await db.Events.CountAsync();

        var result = await AsOrganiser(db).CreateEvent(NewEvent(daysFromNow: -1));

        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(result));
        Assert.Equal(before, await db.Events.CountAsync());
    }

    [Fact]
    public async Task UpdateEvent_OwnEvent_Returns200_AndSavesChanges()
    {
        using var db = TestDb.Create();
        var (ev, _) = TestDb.AddEvent(db, TestDb.OrganiserSipho, 30);
        var request = NewEvent("Walk");
        request.Name = "Renamed Walk";

        var result = await AsOrganiser(db).UpdateEvent(ev.EventId, request);

        Assert.Equal(StatusCodes.Status200OK, StatusOf(result));
        Assert.Equal("Renamed Walk", ValueOf<EventDto>(result).Name);
        Assert.Equal("Walk", ValueOf<EventDto>(result).EventType);
    }

    [Fact]
    public async Task UpdateEvent_AnotherOrganisersEvent_Returns403()
    {
        using var db = TestDb.Create();
        var (ev, _) = TestDb.AddEvent(db, TestDb.OrganiserLerato, 30);

        var result = await AsOrganiser(db, TestDb.OrganiserSipho).UpdateEvent(ev.EventId, NewEvent());

        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(result));
    }

    [Fact]
    public async Task UpdateEvent_Missing_Returns404()
    {
        using var db = TestDb.Create();

        var result = await AsOrganiser(db).UpdateEvent(9999, NewEvent());

        Assert.Equal(StatusCodes.Status404NotFound, StatusOf(result));
    }

    [Fact]
    public async Task DeleteEvent_OwnEventWithoutEnrolments_Returns204_AndRemovesCategories()
    {
        using var db = TestDb.Create();
        var (ev, cat) = TestDb.AddEvent(db, TestDb.OrganiserSipho, 30);

        var result = await AsOrganiser(db).DeleteEvent(ev.EventId);

        Assert.Equal(StatusCodes.Status204NoContent, StatusOf(result));
        Assert.False(await db.Events.AnyAsync(e => e.EventId == ev.EventId));
        Assert.False(await db.Categories.AnyAsync(c => c.CategoryId == cat.CategoryId));
    }

    [Fact]
    public async Task DeleteEvent_WithEnrolments_Returns409()
    {
        using var db = TestDb.Create();

        // Seeded event 1 (Sipho) already has enrolments
        var result = await AsOrganiser(db).DeleteEvent(1);

        Assert.Equal(StatusCodes.Status409Conflict, StatusOf(result));
        Assert.True(await db.Events.AnyAsync(e => e.EventId == 1));
    }

    [Fact]
    public async Task DeleteEvent_AnotherOrganisersEvent_Returns403()
    {
        using var db = TestDb.Create();
        var (ev, _) = TestDb.AddEvent(db, TestDb.OrganiserLerato, 30);

        var result = await AsOrganiser(db, TestDb.OrganiserSipho).DeleteEvent(ev.EventId);

        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(result));
        Assert.True(await db.Events.AnyAsync(e => e.EventId == ev.EventId));
    }

    [Fact]
    public async Task GetEvents_AsParticipant_ReturnsAllEvents()
    {
        using var db = TestDb.Create();
        var controller = new EventsController(db).WithSession(TestDb.ParticipantThabo, RoleNames.Participant);

        var result = await controller.GetEvents(type: null);

        var events = ValueOf<IEnumerable<EventDto>>(result).ToList();
        Assert.Equal(await db.Events.CountAsync(), events.Count);
    }

    [Fact]
    public async Task GetEvents_FilteredByType_ReturnsOnlyThatType()
    {
        using var db = TestDb.Create();
        var controller = new EventsController(db).WithSession(TestDb.ParticipantThabo, RoleNames.Participant);

        var result = await controller.GetEvents(type: "cycle");

        var events = ValueOf<IEnumerable<EventDto>>(result).ToList();
        Assert.NotEmpty(events);
        Assert.All(events, e => Assert.Equal("Cycle", e.EventType));
    }

    [Fact]
    public async Task GetMyEvents_ReturnsOnlyTheOrganisersEvents()
    {
        using var db = TestDb.Create();

        var result = await AsOrganiser(db, TestDb.OrganiserLerato).GetMyEvents();

        var events = ValueOf<IEnumerable<EventDto>>(result).ToList();
        Assert.NotEmpty(events);
        Assert.All(events, e => Assert.Equal(TestDb.OrganiserLerato, e.OrganiserId));
    }

    [Fact]
    public async Task GetEvent_Missing_Returns404()
    {
        using var db = TestDb.Create();
        var controller = new EventsController(db).WithSession(TestDb.ParticipantThabo, RoleNames.Participant);

        var result = await controller.GetEvent(12345);

        Assert.Equal(StatusCodes.Status404NotFound, StatusOf(result));
    }
}
