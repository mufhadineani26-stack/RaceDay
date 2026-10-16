using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Organisers capture finish times and positions; Participants view their own results.</summary>
[ApiController]
[Route("api")]
[Tags("Results")]
[SessionAuthorize]
public class ResultsController : ApiControllerBase
{
    private static readonly string[] TimeFormats = { @"h\:mm\:ss", @"hh\:mm\:ss" };
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db) => _db = db;

    /// <summary>Captures a participant's finish time and position for an enrolment.</summary>
    /// <remarks>
    /// Only the Organiser of the event can capture results, and only after the event has started.
    /// Positions are unique within a category of an event. The enrolment is marked Completed.
    /// </remarks>
    /// <response code="201">Result captured.</response>
    /// <response code="400">Event has not taken place yet, or the finish time is invalid.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">Enrolment does not exist.</response>
    /// <response code="409">A result already exists for this enrolment, or the position is already taken in this category.</response>
    [HttpPost("enrolments/{enrolmentId:int}/result")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CaptureResult(int enrolmentId, [FromBody] ResultRequest request)
    {
        var en = await _db.Enrolments.Include(e => e.Event).Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentId == enrolmentId);
        if (en is null) return NotFound(new ApiMessage($"Enrolment {enrolmentId} was not found."));
        if (en.Event!.OrganiserId != CurrentUserId) return Forbidden("You can only capture results for events you created.");
        if (en.Event.EventDate > DateTime.Now)
            return BadRequest(new ApiMessage("Results can only be captured after the event has taken place."));
        if (en.Result is not null)
            return Conflict(new ApiMessage("A result has already been captured for this enrolment. Use PUT /api/results/{id} to change it."));

        if (!TryParseTime(request.FinishTime, out var finishTime))
            return BadRequest(new ApiMessage("FinishTime must be a positive time in hh:mm:ss format."));
        if (request.Position < 1)
            return BadRequest(new ApiMessage("Position must be 1 or higher."));
        if (await PositionTaken(en.EventId, en.CategoryId, request.Position, excludeResultId: null))
            return Conflict(new ApiMessage($"Position {request.Position} is already taken in this category."));

        var result = new Result
        {
            EnrolmentId = enrolmentId,
            FinishTime = finishTime,
            Position = request.Position,
            RecordedAt = DateTime.UtcNow
        };
        en.Status = EnrolmentStatus.Completed;
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var saved = await WithDetails().FirstAsync(r => r.ResultId == result.ResultId);
        return CreatedAtAction(nameof(GetResult), new { id = result.ResultId }, ToDto(saved));
    }

    /// <summary>Corrects a captured result. Only the event's Organiser may do this.</summary>
    /// <response code="200">The updated result.</response>
    /// <response code="400">Invalid finish time or position.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of the event.</response>
    /// <response code="404">Result does not exist.</response>
    /// <response code="409">The position is already taken in this category.</response>
    [HttpPut("results/{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateResult(int id, [FromBody] ResultRequest request)
    {
        var result = await WithDetails().FirstOrDefaultAsync(r => r.ResultId == id);
        if (result is null) return NotFound(new ApiMessage($"Result {id} was not found."));
        var en = result.Enrolment!;
        if (en.Event!.OrganiserId != CurrentUserId) return Forbidden("You can only update results for events you created.");

        if (!TryParseTime(request.FinishTime, out var finishTime))
            return BadRequest(new ApiMessage("FinishTime must be a positive time in hh:mm:ss format."));
        if (request.Position < 1)
            return BadRequest(new ApiMessage("Position must be 1 or higher."));
        if (await PositionTaken(en.EventId, en.CategoryId, request.Position, excludeResultId: id))
            return Conflict(new ApiMessage($"Position {request.Position} is already taken in this category."));

        result.FinishTime = finishTime;
        result.Position = request.Position;
        result.RecordedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ToDto(result));
    }

    /// <summary>Removes a captured result (e.g. captured against the wrong participant).</summary>
    /// <response code="204">Result removed and the enrolment set back to Registered.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of the event.</response>
    /// <response code="404">Result does not exist.</response>
    [HttpDelete("results/{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteResult(int id)
    {
        var result = await WithDetails().FirstOrDefaultAsync(r => r.ResultId == id);
        if (result is null) return NotFound(new ApiMessage($"Result {id} was not found."));
        if (result.Enrolment!.Event!.OrganiserId != CurrentUserId) return Forbidden("You can only delete results for events you created.");

        result.Enrolment.Status = EnrolmentStatus.Registered;
        _db.Results.Remove(result);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Lists the logged-in Participant's own results (personal performance history).</summary>
    /// <response code="200">The participant's results, most recent event first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Only Participants have results.</response>
    [HttpGet("results/mine")]
    [SessionAuthorize(RoleNames.Participant)]
    [ProducesResponseType(typeof(IEnumerable<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyResults()
    {
        var participantId = CurrentUserId;
        var list = await WithDetails().Where(r => r.Enrolment!.ParticipantId == participantId)
            .OrderByDescending(r => r.Enrolment!.Event!.EventDate).ToListAsync();
        return Ok(list.Select(ToDto));
    }

    /// <summary>Returns one result. Visible to the Participant it belongs to or the event's Organiser.</summary>
    /// <response code="200">The result.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not your result and not your event.</response>
    /// <response code="404">Result does not exist.</response>
    [HttpGet("results/{id:int}")]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResult(int id)
    {
        var result = await WithDetails().FirstOrDefaultAsync(r => r.ResultId == id);
        if (result is null) return NotFound(new ApiMessage($"Result {id} was not found."));

        var userId = CurrentUserId;
        if (result.Enrolment!.ParticipantId != userId && result.Enrolment.Event!.OrganiserId != userId)
            return Forbidden("You do not have access to this result.");
        return Ok(ToDto(result));
    }

    /// <summary>Returns the results board for an event, grouped by category and ordered by position.</summary>
    /// <response code="200">Results for the event.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("events/{eventId:int}/results")]
    [ProducesResponseType(typeof(IEnumerable<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventResults(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
            return NotFound(new ApiMessage($"Event {eventId} was not found."));

        var list = await WithDetails().Where(r => r.Enrolment!.EventId == eventId)
            .OrderBy(r => r.Enrolment!.Category!.Name).ThenBy(r => r.Position).ToListAsync();
        return Ok(list.Select(ToDto));
    }

    private IQueryable<Result> WithDetails() => _db.Results
        .Include(r => r.Enrolment!).ThenInclude(en => en.Event)
        .Include(r => r.Enrolment!).ThenInclude(en => en.Category)
        .Include(r => r.Enrolment!).ThenInclude(en => en.Participant);

    private Task<bool> PositionTaken(int eventId, int categoryId, int position, int? excludeResultId) =>
        _db.Results.AnyAsync(r =>
            r.Enrolment!.EventId == eventId &&
            r.Enrolment.CategoryId == categoryId &&
            r.Position == position &&
            (excludeResultId == null || r.ResultId != excludeResultId));

    internal static bool TryParseTime(string? value, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!TimeSpan.TryParseExact(value.Trim(), TimeFormats, CultureInfo.InvariantCulture, out time)) return false;
        return time > TimeSpan.Zero;
    }

    private static ResultDto ToDto(Result r)
    {
        var en = r.Enrolment!;
        return new ResultDto(
            r.ResultId, r.EnrolmentId, en.EventId, en.Event!.Name, en.Event.EventDate,
            en.Category!.Name, en.ParticipantId, $"{en.Participant!.FirstName} {en.Participant.LastName}",
            r.FinishTime.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture), r.Position, r.RecordedAt);
    }
}
