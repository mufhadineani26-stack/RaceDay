using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Tests.Helpers;
using static RaceDay.Api.Tests.Helpers.ControllerSetup;

namespace RaceDay.Api.Tests.Unit;

public class ResultsControllerTests
{
    private static ResultsController AsOrganiser(RaceDayDbContext db, int organiserId = TestDb.OrganiserSipho) =>
        new ResultsController(db).WithSession(organiserId, RoleNames.Organiser);

    /// <summary>Adds a past event with Johan enrolled but no result yet.</summary>
    private static Enrolment PastEnrolment(RaceDayDbContext db, int organiserId = TestDb.OrganiserSipho)
    {
        var (ev, cat) = TestDb.AddEvent(db, organiserId, daysFromNow: -3);
        var en = new Enrolment
        {
            ParticipantId = TestDb.ParticipantJohan, EventId = ev.EventId, CategoryId = cat.CategoryId,
            EnrolmentDate = DateTime.UtcNow.AddDays(-10), Status = EnrolmentStatus.Registered
        };
        db.Enrolments.Add(en);
        db.SaveChanges();
        return en;
    }

    [Fact]
    public async Task CaptureResult_AfterEvent_Returns201_AndMarksEnrolmentCompleted()
    {
        using var db = TestDb.Create();
        var en = PastEnrolment(db);

        var result = await AsOrganiser(db).CaptureResult(en.EnrolmentId,
            new ResultRequest { FinishTime = "00:48:15", Position = 1 });

        Assert.Equal(StatusCodes.Status201Created, StatusOf(result));
        var dto = ValueOf<ResultDto>(result);
        Assert.Equal("00:48:15", dto.FinishTime);
        Assert.Equal(1, dto.Position);

        var saved = await db.Results.SingleAsync(r => r.EnrolmentId == en.EnrolmentId);
        Assert.Equal(new TimeSpan(0, 48, 15), saved.FinishTime);
        Assert.Equal(EnrolmentStatus.Completed, (await db.Enrolments.FindAsync(en.EnrolmentId))!.Status);
    }

    [Fact]
    public async Task CaptureResult_BeforeEventHasHappened_Returns400()
    {
        using var db = TestDb.Create();

        // Enrolment 1 is for a seeded future-dated event; use a fresh upcoming event to stay date-independent
        var (ev, cat) = TestDb.AddEvent(db, TestDb.OrganiserSipho, 20);
        var en = new Enrolment { ParticipantId = TestDb.ParticipantThabo, EventId = ev.EventId, CategoryId = cat.CategoryId, EnrolmentDate = DateTime.UtcNow };
        db.Enrolments.Add(en);
        db.SaveChanges();

        var result = await AsOrganiser(db).CaptureResult(en.EnrolmentId, new ResultRequest { FinishTime = "01:00:00", Position = 1 });

        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(result));
    }

    [Fact]
    public async Task CaptureResult_ForAnotherOrganisersEvent_Returns403()
    {
        using var db = TestDb.Create();
        var en = PastEnrolment(db, TestDb.OrganiserLerato);

        var result = await AsOrganiser(db, TestDb.OrganiserSipho)
            .CaptureResult(en.EnrolmentId, new ResultRequest { FinishTime = "00:50:00", Position = 1 });

        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(result));
    }

    [Fact]
    public async Task CaptureResult_WhenResultAlreadyExists_Returns409()
    {
        using var db = TestDb.Create();

        // Seeded enrolment 4 already has a result
        var result = await AsOrganiser(db).CaptureResult(4, new ResultRequest { FinishTime = "01:30:00", Position = 5 });

        Assert.Equal(StatusCodes.Status409Conflict, StatusOf(result));
    }

    [Fact]
    public async Task CaptureResult_PositionAlreadyTakenInCategory_Returns409()
    {
        using var db = TestDb.Create();
        var en = PastEnrolment(db);
        var other = new Enrolment { ParticipantId = TestDb.ParticipantThabo, EventId = en.EventId, CategoryId = en.CategoryId, EnrolmentDate = DateTime.UtcNow };
        db.Enrolments.Add(other);
        db.SaveChanges();
        var controller = AsOrganiser(db);

        await controller.CaptureResult(en.EnrolmentId, new ResultRequest { FinishTime = "00:45:00", Position = 1 });
        var result = await controller.CaptureResult(other.EnrolmentId, new ResultRequest { FinishTime = "00:46:00", Position = 1 });

        Assert.Equal(StatusCodes.Status409Conflict, StatusOf(result));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("00:00:00")]
    [InlineData("1:75:00")]
    public async Task CaptureResult_WithInvalidTime_Returns400(string time)
    {
        using var db = TestDb.Create();
        var en = PastEnrolment(db);

        var result = await AsOrganiser(db).CaptureResult(en.EnrolmentId, new ResultRequest { FinishTime = time, Position = 1 });

        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(result));
    }

    [Fact]
    public async Task GetMyResults_AsParticipant_ReturnsOnlyOwnResults()
    {
        using var db = TestDb.Create();
        var controller = new ResultsController(db).WithSession(TestDb.ParticipantThabo, RoleNames.Participant);

        var result = await controller.GetMyResults();

        var list = ValueOf<IEnumerable<ResultDto>>(result).ToList();
        var only = Assert.Single(list);
        Assert.Equal(TestDb.ParticipantThabo, only.ParticipantId);
        Assert.Equal("01:42:10", only.FinishTime);
        Assert.Equal(2, only.Position);
    }

    [Fact]
    public async Task GetResult_BelongingToAnotherParticipant_Returns403()
    {
        using var db = TestDb.Create();
        var controller = new ResultsController(db).WithSession(TestDb.ParticipantThabo, RoleNames.Participant);

        var result = await controller.GetResult(2); // Ayesha's result

        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(result));
    }

    [Fact]
    public async Task GetEventResults_ReturnsResultsOrderedByPosition()
    {
        using var db = TestDb.Create();
        var controller = new ResultsController(db).WithSession(TestDb.ParticipantJohan, RoleNames.Participant);

        var result = await controller.GetEventResults(TestDb.PastEventId);

        var list = ValueOf<IEnumerable<ResultDto>>(result).ToList();
        Assert.Equal(3, list.Count);
        var walk10 = list.Where(r => r.CategoryName == "10km Walk").ToList();
        Assert.Equal(new[] { 1, 2 }, walk10.Select(r => r.Position));
    }
}
