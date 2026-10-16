using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Controllers;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Tests.Helpers;
using static RaceDay.Api.Tests.Helpers.ControllerSetup;

namespace RaceDay.Api.Tests.Unit;

public class CategoriesControllerTests
{
    [Fact]
    public async Task CreateCategory_OnOwnEvent_Returns201()
    {
        using var db = TestDb.Create();
        var (ev, _) = TestDb.AddEvent(db, TestDb.OrganiserSipho, 30);
        var controller = new CategoriesController(db).WithSession(TestDb.OrganiserSipho, RoleNames.Organiser);

        var result = await controller.CreateCategory(ev.EventId,
            new CategoryRequest { Name = "Under 20", MinAge = 14, MaxAge = 19, EntryFee = 80 });

        Assert.Equal(StatusCodes.Status201Created, StatusOf(result));
        var dto = ValueOf<CategoryDto>(result);
        Assert.Equal(ev.EventId, dto.EventId);
        Assert.Equal(2, await db.Categories.CountAsync(c => c.EventId == ev.EventId));
    }

    [Fact]
    public async Task CreateCategory_WithMinAgeAboveMaxAge_Returns400()
    {
        using var db = TestDb.Create();
        var (ev, _) = TestDb.AddEvent(db, TestDb.OrganiserSipho, 30);
        var controller = new CategoriesController(db).WithSession(TestDb.OrganiserSipho, RoleNames.Organiser);

        var result = await controller.CreateCategory(ev.EventId,
            new CategoryRequest { Name = "Broken", MinAge = 40, MaxAge = 20 });

        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(result));
    }

    [Fact]
    public async Task CreateCategory_DuplicateName_Returns409()
    {
        using var db = TestDb.Create();
        var (ev, _) = TestDb.AddEvent(db, TestDb.OrganiserSipho, 30); // already has "10km"
        var controller = new CategoriesController(db).WithSession(TestDb.OrganiserSipho, RoleNames.Organiser);

        var result = await controller.CreateCategory(ev.EventId, new CategoryRequest { Name = "10KM" });

        Assert.Equal(StatusCodes.Status409Conflict, StatusOf(result));
    }

    [Fact]
    public async Task CreateCategory_OnAnotherOrganisersEvent_Returns403()
    {
        using var db = TestDb.Create();
        var (ev, _) = TestDb.AddEvent(db, TestDb.OrganiserLerato, 30);
        var controller = new CategoriesController(db).WithSession(TestDb.OrganiserSipho, RoleNames.Organiser);

        var result = await controller.CreateCategory(ev.EventId, new CategoryRequest { Name = "Senior" });

        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(result));
    }

    [Fact]
    public async Task DeleteCategory_WithEnrolments_Returns409()
    {
        using var db = TestDb.Create();
        var controller = new CategoriesController(db).WithSession(TestDb.OrganiserSipho, RoleNames.Organiser);

        // Seeded category 1 (Soweto Marathon 42.2km) has an enrolment
        var result = await controller.DeleteCategory(1);

        Assert.Equal(StatusCodes.Status409Conflict, StatusOf(result));
    }

    [Fact]
    public async Task GetCategoriesForEvent_AsParticipant_ReturnsCategories()
    {
        using var db = TestDb.Create();
        var controller = new CategoriesController(db).WithSession(TestDb.ParticipantThabo, RoleNames.Participant);

        var result = await controller.GetCategoriesForEvent(1);

        var list = ValueOf<IEnumerable<CategoryDto>>(result).ToList();
        Assert.Equal(3, list.Count);
        Assert.All(list, c => Assert.Equal(1, c.EventId));
    }

    [Fact]
    public async Task GetCategoriesForEvent_MissingEvent_Returns404()
    {
        using var db = TestDb.Create();
        var controller = new CategoriesController(db).WithSession(TestDb.ParticipantThabo, RoleNames.Participant);

        var result = await controller.GetCategoriesForEvent(999);

        Assert.Equal(StatusCodes.Status404NotFound, StatusOf(result));
    }
}
