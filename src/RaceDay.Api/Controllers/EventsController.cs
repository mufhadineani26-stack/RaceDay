using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Browse events (both roles) and manage your own events (Organiser).</summary>
[ApiController]
[Route("api")]
[Tags("Events")]
[SessionAuthorize]
public class EventsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public EventsController(RaceDayDbContext db) => _db = db;

    internal static readonly Expression<Func<Event, EventDto>> ToDto = e => new EventDto(
        e.EventId, e.Name, e.Description, e.EventDate, e.Location, e.DistanceKm,
        e.EventType!.Name, e.OrganiserId, e.Organiser!.FirstName + " " + e.Organiser.LastName,
        e.Categories.Count, e.Enrolments.Count);

    /// <summary>Lists the available event types (Run, Walk, Cycle).</summary>
    /// <response code="200">All event types.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet("event-types")]
    [ProducesResponseType(typeof(IEnumerable<EventTypeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetEventTypes()
    {
        var types = await _db.EventTypes.OrderBy(t => t.EventTypeId)
            .Select(t => new EventTypeDto(t.EventTypeId, t.Name)).ToListAsync();
        return Ok(types);
    }

    /// <summary>Lists events, soonest first. Optionally filter by type, upcoming only, or a search term.</summary>
    /// <param name="type">Run, Walk or Cycle.</param>
    /// <param name="upcomingOnly">If true, only events that have not happened yet.</param>
    /// <param name="search">Matches event name or location.</param>
    /// <response code="200">Matching events.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet("events")]
    [ProducesResponseType(typeof(IEnumerable<EventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetEvents([FromQuery] string? type, [FromQuery] bool upcomingOnly = false, [FromQuery] string? search = null)
    {
        var query = _db.Events.AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
        {
            var typeName = type.Trim().ToLower();
            query = query.Where(e => e.EventType!.Name.ToLower() == typeName);
        }
        if (upcomingOnly)
            query = query.Where(e => e.EventDate >= DateTime.Now);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(e => e.Name.ToLower().Contains(term) || e.Location.ToLower().Contains(term));
        }

        var events = await query.OrderBy(e => e.EventDate).Select(ToDto).ToListAsync();
        return Ok(events);
    }

    /// <summary>Lists the events created by the logged-in Organiser.</summary>
    /// <response code="200">The organiser's events.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Only Organisers can call this.</response>
    [HttpGet("events/mine")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyEvents()
    {
        var organiserId = CurrentUserId;
        var events = await _db.Events.Where(e => e.OrganiserId == organiserId)
            .OrderBy(e => e.EventDate).Select(ToDto).ToListAsync();
        return Ok(events);
    }

    /// <summary>Returns a single event by id.</summary>
    /// <response code="200">The event.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("events/{id:int}")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvent(int id)
    {
        var dto = await _db.Events.Where(e => e.EventId == id).Select(ToDto).FirstOrDefaultAsync();
        return dto is null ? NotFound(new ApiMessage($"Event {id} was not found.")) : Ok(dto);
    }

    /// <summary>Creates a new event owned by the logged-in Organiser.</summary>
    /// <response code="201">Event created.</response>
    /// <response code="400">Validation failed, unknown event type, or date is in the past.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Only Organisers can create events.</response>
    [HttpPost("events")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateEvent([FromBody] EventRequest request)
    {
        if (request.EventDate <= DateTime.Now)
            return BadRequest(new ApiMessage("Event date must be in the future."));

        var eventType = await FindEventType(request.EventType);
        if (eventType is null)
            return BadRequest(new ApiMessage("EventType must be one of: Run, Walk, Cycle."));

        var entity = new Event
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            EventDate = request.EventDate,
            Location = request.Location.Trim(),
            DistanceKm = request.DistanceKm,
            EventTypeId = eventType.EventTypeId,
            OrganiserId = CurrentUserId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Events.Add(entity);
        await _db.SaveChangesAsync();

        var dto = await _db.Events.Where(e => e.EventId == entity.EventId).Select(ToDto).FirstAsync();
        return CreatedAtAction(nameof(GetEvent), new { id = entity.EventId }, dto);
    }

    /// <summary>Updates an event. Only the Organiser who created it may update it.</summary>
    /// <response code="200">The updated event.</response>
    /// <response code="400">Validation failed or unknown event type.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpPut("events/{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEvent(int id, [FromBody] EventRequest request)
    {
        var entity = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (entity is null) return NotFound(new ApiMessage($"Event {id} was not found."));
        if (entity.OrganiserId != CurrentUserId) return Forbidden("You can only update events you created.");

        var eventType = await FindEventType(request.EventType);
        if (eventType is null)
            return BadRequest(new ApiMessage("EventType must be one of: Run, Walk, Cycle."));

        entity.Name = request.Name.Trim();
        entity.Description = request.Description.Trim();
        entity.EventDate = request.EventDate;
        entity.Location = request.Location.Trim();
        entity.DistanceKm = request.DistanceKm;
        entity.EventTypeId = eventType.EventTypeId;
        await _db.SaveChangesAsync();

        var dto = await _db.Events.Where(e => e.EventId == id).Select(ToDto).FirstAsync();
        return Ok(dto);
    }

    /// <summary>Deletes an event and its categories. Only the owner may delete it.</summary>
    /// <remarks>An event that already has participant enrolments cannot be deleted, to protect their entries and results.</remarks>
    /// <response code="204">Event deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">Event does not exist.</response>
    /// <response code="409">Event has enrolments and cannot be deleted.</response>
    [HttpDelete("events/{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var entity = await _db.Events.Include(e => e.Categories).FirstOrDefaultAsync(e => e.EventId == id);
        if (entity is null) return NotFound(new ApiMessage($"Event {id} was not found."));
        if (entity.OrganiserId != CurrentUserId) return Forbidden("You can only delete events you created.");

        if (await _db.Enrolments.AnyAsync(en => en.EventId == id))
            return Conflict(new ApiMessage("This event has enrolments and cannot be deleted."));

        _db.Categories.RemoveRange(entity.Categories);
        _db.Events.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<EventType?> FindEventType(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var types = await _db.EventTypes.ToListAsync();
        return types.FirstOrDefault(t => t.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
