using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Participants enter events by choosing a category; Organisers view entries for their events.</summary>
[ApiController]
[Route("api")]
[Tags("Event Enrolments")]
[SessionAuthorize]
public class EnrolmentsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db) => _db = db;

    /// <summary>Enters the logged-in Participant into an event in the selected category.</summary>
    /// <remarks>
    /// Rules: the event must still be upcoming, the category must belong to the event,
    /// the participant must meet the category's age limits on the event date,
    /// the category must not be full, and a participant may only enter an event once.
    /// </remarks>
    /// <response code="201">Enrolment recorded (participant + event + category).</response>
    /// <response code="400">Event already happened, category not part of this event, or age limits not met.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Only Participants can enter events.</response>
    /// <response code="404">Event does not exist.</response>
    /// <response code="409">Already entered in this event, or the category is full.</response>
    [HttpPost("events/{eventId:int}/enrolments")]
    [SessionAuthorize(RoleNames.Participant)]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enrol(int eventId, [FromBody] EnrolRequest request)
    {
        var participantId = CurrentUserId;

        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFound(new ApiMessage($"Event {eventId} was not found."));
        if (ev.EventDate <= DateTime.Now)
            return BadRequest(new ApiMessage("This event has already taken place, so entries are closed."));

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId);
        if (category is null || category.EventId != eventId)
            return BadRequest(new ApiMessage("The selected category does not belong to this event."));

        if (await _db.Enrolments.AnyAsync(en => en.EventId == eventId && en.ParticipantId == participantId))
            return Conflict(new ApiMessage("You have already entered this event."));

        var participant = await _db.Users.FirstAsync(u => u.UserId == participantId);
        var age = AgeOn(participant.DateOfBirth, ev.EventDate);
        if (category.MinAge.HasValue && age < category.MinAge)
            return BadRequest(new ApiMessage($"You must be at least {category.MinAge} on race day to enter '{category.Name}'."));
        if (category.MaxAge.HasValue && age > category.MaxAge)
            return BadRequest(new ApiMessage($"You must be {category.MaxAge} or younger on race day to enter '{category.Name}'."));

        if (category.MaxParticipants.HasValue &&
            await _db.Enrolments.CountAsync(en => en.CategoryId == category.CategoryId) >= category.MaxParticipants)
            return Conflict(new ApiMessage($"Category '{category.Name}' is full."));

        var enrolment = new Enrolment
        {
            ParticipantId = participantId,
            EventId = eventId,
            CategoryId = category.CategoryId,
            EnrolmentDate = DateTime.UtcNow,
            Status = EnrolmentStatus.Registered
        };
        _db.Enrolments.Add(enrolment);
        await _db.SaveChangesAsync();

        var saved = await WithDetails().FirstAsync(en => en.EnrolmentId == enrolment.EnrolmentId);
        return CreatedAtAction(nameof(GetEnrolment), new { id = enrolment.EnrolmentId }, ToDto(saved));
    }

    /// <summary>Lists the logged-in Participant's own enrolments.</summary>
    /// <response code="200">The participant's enrolments, soonest event first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Only Participants have enrolments.</response>
    [HttpGet("enrolments/mine")]
    [SessionAuthorize(RoleNames.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyEnrolments()
    {
        var participantId = CurrentUserId;
        var list = await WithDetails().Where(en => en.ParticipantId == participantId)
            .OrderBy(en => en.Event!.EventDate).ToListAsync();
        return Ok(list.Select(ToDto));
    }

    /// <summary>Returns one enrolment. Visible to the Participant who owns it or the event's Organiser.</summary>
    /// <response code="200">The enrolment.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not your enrolment and not your event.</response>
    /// <response code="404">Enrolment does not exist.</response>
    [HttpGet("enrolments/{id:int}")]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEnrolment(int id)
    {
        var en = await WithDetails().FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (en is null) return NotFound(new ApiMessage($"Enrolment {id} was not found."));

        var userId = CurrentUserId;
        if (en.ParticipantId != userId && en.Event!.OrganiserId != userId)
            return Forbidden("You do not have access to this enrolment.");

        return Ok(ToDto(en));
    }

    /// <summary>Withdraws the logged-in Participant from an upcoming event.</summary>
    /// <response code="204">Enrolment withdrawn.</response>
    /// <response code="400">The event has already taken place.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not a Participant, or not your enrolment.</response>
    /// <response code="404">Enrolment does not exist.</response>
    [HttpDelete("enrolments/{id:int}")]
    [SessionAuthorize(RoleNames.Participant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Withdraw(int id)
    {
        var en = await _db.Enrolments.Include(e => e.Event).FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (en is null) return NotFound(new ApiMessage($"Enrolment {id} was not found."));
        if (en.ParticipantId != CurrentUserId) return Forbidden("You can only withdraw your own enrolments.");
        if (en.Event!.EventDate <= DateTime.Now)
            return BadRequest(new ApiMessage("You cannot withdraw from an event that has already taken place."));

        _db.Enrolments.Remove(en);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Lists all enrolments for an event. Only the event's Organiser may view them.</summary>
    /// <param name="eventId">The event.</param>
    /// <param name="categoryId">Optional: only show one category.</param>
    /// <response code="200">Enrolments for the event.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("events/{eventId:int}/enrolments")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventEnrolments(int eventId, [FromQuery] int? categoryId = null)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFound(new ApiMessage($"Event {eventId} was not found."));
        if (ev.OrganiserId != CurrentUserId) return Forbidden("You can only view enrolments for events you created.");

        var query = WithDetails().Where(en => en.EventId == eventId);
        if (categoryId.HasValue) query = query.Where(en => en.CategoryId == categoryId);

        var list = await query.OrderBy(en => en.Category!.Name)
            .ThenBy(en => en.Participant!.LastName).ToListAsync();
        return Ok(list.Select(ToDto));
    }

    private IQueryable<Enrolment> WithDetails() => _db.Enrolments
        .Include(en => en.Event)
        .Include(en => en.Category)
        .Include(en => en.Participant)
        .Include(en => en.Result);

    internal static int AgeOn(DateTime dateOfBirth, DateTime onDate)
    {
        var age = onDate.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > onDate.Date.AddYears(-age)) age--;
        return age;
    }

    private static EnrolmentDto ToDto(Enrolment en) => new(
        en.EnrolmentId, en.EventId, en.Event!.Name, en.Event.EventDate, en.Event.Location,
        en.CategoryId, en.Category!.Name, en.ParticipantId,
        $"{en.Participant!.FirstName} {en.Participant.LastName}", en.Participant.Email,
        en.EnrolmentDate, en.Status, en.Result is not null);
}
