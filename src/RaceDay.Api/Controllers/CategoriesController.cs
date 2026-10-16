using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Age or distance categories within an event, e.g. "Under 20", "Senior", "10km", "21km".</summary>
[ApiController]
[Route("api")]
[Tags("Categories")]
[SessionAuthorize]
public class CategoriesController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public CategoriesController(RaceDayDbContext db) => _db = db;

    private static readonly Expression<Func<Category, CategoryDto>> ToDto = c => new CategoryDto(
        c.CategoryId, c.EventId, c.Name, c.Description, c.MinAge, c.MaxAge,
        c.DistanceKm, c.EntryFee, c.MaxParticipants, c.Enrolments.Count);

    /// <summary>Lists the categories available for an event.</summary>
    /// <response code="200">The event's categories.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategoriesForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
            return NotFound(new ApiMessage($"Event {eventId} was not found."));

        var categories = await _db.Categories.Where(c => c.EventId == eventId)
            .OrderBy(c => c.CategoryId).Select(ToDto).ToListAsync();
        return Ok(categories);
    }

    /// <summary>Returns a single category by id.</summary>
    /// <response code="200">The category.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">Category does not exist.</response>
    [HttpGet("categories/{id:int}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategory(int id)
    {
        var dto = await _db.Categories.Where(c => c.CategoryId == id).Select(ToDto).FirstOrDefaultAsync();
        return dto is null ? NotFound(new ApiMessage($"Category {id} was not found.")) : Ok(dto);
    }

    /// <summary>Adds a category to an event. Only the event's Organiser may do this.</summary>
    /// <response code="201">Category created.</response>
    /// <response code="400">Validation failed (e.g. MinAge greater than MaxAge).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">Event does not exist.</response>
    /// <response code="409">A category with this name already exists for the event.</response>
    [HttpPost("events/{eventId:int}/categories")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCategory(int eventId, [FromBody] CategoryRequest request)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFound(new ApiMessage($"Event {eventId} was not found."));
        if (ev.OrganiserId != CurrentUserId) return Forbidden("You can only add categories to events you created.");

        var error = Validate(request);
        if (error is not null) return BadRequest(new ApiMessage(error));

        var name = request.Name.Trim();
        if (await NameTaken(eventId, name, excludeCategoryId: null))
            return Conflict(new ApiMessage($"Category '{name}' already exists for this event."));

        var category = new Category { EventId = eventId };
        Apply(category, request);
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        var dto = await _db.Categories.Where(c => c.CategoryId == category.CategoryId).Select(ToDto).FirstAsync();
        return CreatedAtAction(nameof(GetCategory), new { id = category.CategoryId }, dto);
    }

    /// <summary>Updates a category. Only the event's Organiser may do this.</summary>
    /// <response code="200">The updated category.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of the event.</response>
    /// <response code="404">Category does not exist.</response>
    /// <response code="409">Another category with this name already exists for the event.</response>
    [HttpPut("categories/{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFound(new ApiMessage($"Category {id} was not found."));
        if (category.Event!.OrganiserId != CurrentUserId) return Forbidden("You can only update categories for events you created.");

        var error = Validate(request);
        if (error is not null) return BadRequest(new ApiMessage(error));

        var name = request.Name.Trim();
        if (await NameTaken(category.EventId, name, excludeCategoryId: id))
            return Conflict(new ApiMessage($"Category '{name}' already exists for this event."));

        Apply(category, request);
        await _db.SaveChangesAsync();

        var dto = await _db.Categories.Where(c => c.CategoryId == id).Select(ToDto).FirstAsync();
        return Ok(dto);
    }

    /// <summary>Deletes a category. Not allowed once participants have enrolled in it.</summary>
    /// <response code="204">Category deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of the event.</response>
    /// <response code="404">Category does not exist.</response>
    /// <response code="409">Participants are enrolled in this category.</response>
    [HttpDelete("categories/{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFound(new ApiMessage($"Category {id} was not found."));
        if (category.Event!.OrganiserId != CurrentUserId) return Forbidden("You can only delete categories for events you created.");

        if (await _db.Enrolments.AnyAsync(en => en.CategoryId == id))
            return Conflict(new ApiMessage("Participants are enrolled in this category, so it cannot be deleted."));

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static string? Validate(CategoryRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "Name is required.";
        if (r.MinAge.HasValue && r.MaxAge.HasValue && r.MinAge > r.MaxAge)
            return "MinAge cannot be greater than MaxAge.";
        return null;
    }

    private async Task<bool> NameTaken(int eventId, string name, int? excludeCategoryId)
    {
        var lower = name.ToLower();
        return await _db.Categories.AnyAsync(c =>
            c.EventId == eventId && c.Name.ToLower() == lower &&
            (excludeCategoryId == null || c.CategoryId != excludeCategoryId));
    }

    private static void Apply(Category c, CategoryRequest r)
    {
        c.Name = r.Name.Trim();
        c.Description = r.Description?.Trim();
        c.MinAge = r.MinAge;
        c.MaxAge = r.MaxAge;
        c.DistanceKm = r.DistanceKm;
        c.EntryFee = r.EntryFee;
        c.MaxParticipants = r.MaxParticipants;
    }
}
