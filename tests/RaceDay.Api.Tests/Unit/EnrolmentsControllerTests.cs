using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Tests.Helpers;
using static RaceDay.Api.Tests.Helpers.ControllerSetup;

namespace RaceDay.Api.Tests.Unit;

public class EnrolmentsControllerTests
{
    private static EnrolmentsController AsParticipant(RaceDayDbContext db, int participantId = TestDb.ParticipantThabo) =>
        new EnrolmentsController(db).WithSession(participantId, RoleNames.Participant);

    [Fact]
    public async Task Enrol_InUpcomingEvent_Returns201_AndRecordsParticipantEventAndCategory()
    {
        using var db = TestDb.Create();
        var (ev, cat) = TestDb.AddEvent(db, TestDb.OrganiserSipho, 30);

        var result = await AsParticipant(db).Enrol(ev.EventId, new EnrolRequest { CategoryId = cat.CategoryId });

        Assert.Equal(StatusCodes.Status201Created, StatusOf(result));
        var dto = ValueOf<EnrolmentDto>(result);
        Assert.Equal(TestDb.ParticipantThabo, dto.ParticipantId);
        Assert.Equal(ev.EventId, dto.EventId);
        Assert.Equal(cat.CategoryId, dto.CategoryId);
        Assert.Equal(EnrolmentStatus.Registered, dto.Status);

        // The link is persisted in the database
        var saved = await db.Enrolments.SingleAsync(e => e.EnrolmentId == dto.EnrolmentId);
        Assert.Equal(TestDb.ParticipantThabo, saved.ParticipantId);
        Assert.Equal(ev.EventId, saved.EventId);
        Assert.Equal(cat.CategoryId, saved.CategoryId);
    }

    [Fact]
    public async Task Enrol_TwiceInSameEvent_Returns409()
    {
        using var db = TestDb.Create();
        var (ev, cat) = TestDb.AddEvent(db, TestDb.OrganiserSipho, 30);
        var controller = AsParticipant(db);

        await controller.Enrol(ev.EventId, new EnrolRequest { CategoryId = cat.CategoryId });
        var second = await controller.Enrol(ev.EventId, new EnrolRequest { CategoryId = cat.CategoryId });

        Assert.Equal(StatusCodes.Status409Conflict, StatusOf(second));
        Assert.Equal(1, await db.Enrolments.CountAsync(e => e.EventId == ev.EventId));
    }

    [Fact]
    public async Task Enrol_WithCategoryFromAnotherEvent_Returns400()
    {
        using var db = TestDb.Create();
        var (ev, _) = TestDb.AddEvent(db, TestDb.OrganiserSipho, 30);
        var (_, otherCat) = TestDb.AddEvent(db, TestDb.OrganiserSipho, 40);

        var result = await AsParticipant(db).Enrol(ev.EventId, new EnrolRequest { CategoryId = otherCat.CategoryId });

        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(result));
    }

    [Fact]
    public async Task Enrol_InPastEvent_Returns400()
    {
        using var db = TestDb.Create();

        var result = await AsParticipant(db, TestDb.ParticipantJohan)
            .Enrol(TestDb.PastEventId, new EnrolRequest { CategoryId = 8 });

        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(result));
    }

    [Fact]
    public async Task Enrol_InMissingEvent_Returns404()
    {
        using var db = TestDb.Create();

        var result = await AsParticipant(db).Enrol(9999, new EnrolRequest { CategoryId = 1 });

        Assert.Equal(StatusCodes.Status404NotFound, StatusOf(result));
    }

    [Fact]
    public async Task Enrol_OutsideCategoryAgeLimits_Returns400()
    {
        using var db = TestDb.Create();
        var (ev, cat) = TestDb.AddEvent(db, TestDb.OrganiserSipho, 30, minAge: 60);

        // Thabo (born 1995) is too young for a 60+ category
        var result = await AsParticipant(db).Enrol(ev.EventId, new EnrolRequest { CategoryId = cat.CategoryId });

        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(result));
    }

    [Fact]
    public async Task Enrol_InFullCategory_Returns409()
    {
        using var db = TestDb.Create();
        var (ev, cat) = TestDb.AddEvent(db, TestDb.OrganiserSipho, 30, maxParticipants: 1);

        await AsParticipant(db, TestDb.ParticipantJohan).Enrol(ev.EventId, new EnrolRequest { CategoryId = cat.CategoryId });
        var result = await AsParticipant(db, TestDb.ParticipantThabo).Enrol(ev.EventId, new EnrolRequest { CategoryId = cat.CategoryId });

        Assert.Equal(StatusCodes.Status409Conflict, StatusOf(result));
    }

    [Fact]
    public async Task GetMyEnrolments_ReturnsOnlyOwnEnrolments()
    {
        using var db = TestDb.Create();

        var result = await AsParticipant(db, TestDb.ParticipantThabo).GetMyEnrolments();

        var list = ValueOf<IEnumerable<EnrolmentDto>>(result).ToList();
        Assert.Equal(2, list.Count); // seeded: Soweto Marathon + Jozi Walk
        Assert.All(list, e => Assert.Equal(TestDb.ParticipantThabo, e.ParticipantId));
    }

    [Fact]
    public async Task GetEventEnrolments_AsOwningOrganiser_ReturnsAllEntries()
    {
        using var db = TestDb.Create();
        var controller = new EnrolmentsController(db).WithSession(TestDb.OrganiserSipho, RoleNames.Organiser);

        var result = await controller.GetEventEnrolments(1);

        var list = ValueOf<IEnumerable<EnrolmentDto>>(result).ToList();
        Assert.Equal(2, list.Count);
        Assert.All(list, e => Assert.Equal(1, e.EventId));
    }

    [Fact]
    public async Task GetEventEnrolments_AsDifferentOrganiser_Returns403()
    {
        using var db = TestDb.Create();
        var controller = new EnrolmentsController(db).WithSession(TestDb.OrganiserLerato, RoleNames.Organiser);

        var result = await controller.GetEventEnrolments(1); // event 1 belongs to Sipho

        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(result));
    }

    [Fact]
    public async Task Withdraw_FromUpcomingEvent_Returns204()
    {
        using var db = TestDb.Create();
        var (ev, cat) = TestDb.AddEvent(db, TestDb.OrganiserSipho, 30);
        var controller = AsParticipant(db);
        var created = ValueOf<EnrolmentDto>(await controller.Enrol(ev.EventId, new EnrolRequest { CategoryId = cat.CategoryId }));

        var result = await controller.Withdraw(created.EnrolmentId);

        Assert.Equal(StatusCodes.Status204NoContent, StatusOf(result));
        Assert.False(await db.Enrolments.AnyAsync(e => e.EnrolmentId == created.EnrolmentId));
    }

    [Fact]
    public async Task Withdraw_SomeoneElsesEnrolment_Returns403()
    {
        using var db = TestDb.Create();

        // Enrolment 2 belongs to Ayesha
        var result = await AsParticipant(db, TestDb.ParticipantThabo).Withdraw(2);

        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(result));
    }

    [Theory]
    [InlineData(1995, 4, 12, 2026, 4, 11, 30)]
    [InlineData(1995, 4, 12, 2026, 4, 12, 31)]
    [InlineData(2008, 8, 20, 2026, 11, 29, 18)]
    public void AgeOn_CalculatesAgeOnRaceDay(int by, int bm, int bd, int ry, int rm, int rd, int expected)
    {
        Assert.Equal(expected, EnrolmentsController.AgeOn(new DateTime(by, bm, bd), new DateTime(ry, rm, rd)));
    }
}
