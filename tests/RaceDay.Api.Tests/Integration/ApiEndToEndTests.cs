using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Tests.Integration;

/// <summary>
/// End-to-end tests over HTTP: authenticated vs unauthenticated requests,
/// session-based login, and role enforcement on real routes.
/// </summary>
public class ApiEndToEndTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public ApiEndToEndTests(RaceDayApiFactory factory) => _factory = factory;

    private static EventRequest NewEvent(string name) => new()
    {
        Name = name,
        Description = "Integration test event",
        EventDate = DateTime.Now.AddDays(45),
        Location = "Durban",
        DistanceKm = 21.1m,
        EventType = "Run"
    };

    // ---------- Unauthenticated access ----------

    [Theory]
    [InlineData("/api/events")]
    [InlineData("/api/profile")]
    [InlineData("/api/auth/me")]
    [InlineData("/api/enrolments/mine")]
    [InlineData("/api/results/mine")]
    [InlineData("/api/events/1/categories")]
    public async Task ProtectedEndpoints_WithoutLogin_Return401(string url)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithoutLogin_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/events", NewEvent("Anonymous Run"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Authentication + session ----------

    [Fact]
    public async Task Login_StartsSession_AndMeReturnsTheUserAndRole()
    {
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = RaceDayApiFactory.Participant, Password = SeedDataPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var me = await client.GetFromJsonAsync<SessionUserDto>("/api/auth/me");
        Assert.NotNull(me);
        Assert.Equal(RoleNames.Participant, me!.Role);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = RaceDayApiFactory.Participant, Password = "WrongPassword1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithMissingFields_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = "bad" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var client = await _factory.LoggedInClient(RaceDayApiFactory.Participant);

        var logout = await client.PostAsync("/api/auth/logout", null);
        var me = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    // ---------- Role enforcement ----------

    [Fact]
    public async Task Participant_CannotCreateEvents_Returns403()
    {
        var client = await _factory.LoggedInClient(RaceDayApiFactory.Participant);

        var response = await client.PostAsJsonAsync("/api/events", NewEvent("Sneaky Event"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewEventEnrolments_Returns403()
    {
        var client = await _factory.LoggedInClient(RaceDayApiFactory.Participant);

        var response = await client.GetAsync("/api/events/1/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEnterEvents_Returns403()
    {
        var client = await _factory.LoggedInClient(RaceDayApiFactory.Organiser);

        var response = await client.PostAsJsonAsync("/api/events/2/enrolments", new EnrolRequest { CategoryId = 4 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotViewParticipantResults_Returns403()
    {
        var client = await _factory.LoggedInClient(RaceDayApiFactory.Organiser);

        var response = await client.GetAsync("/api/results/mine");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- Full workflow ----------

    [Fact]
    public async Task FullFlow_OrganiserCreatesEvent_NewParticipantRegistersAndEnrols_OrganiserSeesEnrolment()
    {
        // Organiser creates an event and a category
        var organiser = await _factory.LoggedInClient(RaceDayApiFactory.Organiser);
        var createEvent = await organiser.PostAsJsonAsync("/api/events", NewEvent("Comrades Training Half"));
        Assert.Equal(HttpStatusCode.Created, createEvent.StatusCode);
        var ev = (await createEvent.Content.ReadFromJsonAsync<EventDto>())!;

        var createCat = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new CategoryRequest { Name = "21.1km Open", DistanceKm = 21.1m, EntryFee = 150 });
        Assert.Equal(HttpStatusCode.Created, createCat.StatusCode);
        var cat = (await createCat.Content.ReadFromJsonAsync<CategoryDto>())!;

        // A new participant registers, logs in and enters
        var email = $"runner{Guid.NewGuid():N}@gmail.com";
        var anon = _factory.CreateClient();
        var register = await anon.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FirstName = "Zinhle", LastName = "Mthembu", Email = email, Password = "Runner2026!",
            DateOfBirth = new DateTime(1999, 3, 3), Role = "Participant"
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var participant = await _factory.LoggedInClient(email, "Runner2026!");
        var enrol = await participant.PostAsJsonAsync($"/api/events/{ev.EventId}/enrolments", new EnrolRequest { CategoryId = cat.CategoryId });
        Assert.Equal(HttpStatusCode.Created, enrol.StatusCode);

        var mine = await participant.GetFromJsonAsync<List<EnrolmentDto>>("/api/enrolments/mine");
        Assert.Contains(mine!, e => e.EventId == ev.EventId && e.CategoryId == cat.CategoryId);

        // Organiser sees the entry for their event
        var entries = await organiser.GetFromJsonAsync<List<EnrolmentDto>>($"/api/events/{ev.EventId}/enrolments");
        var entry = Assert.Single(entries!);
        Assert.Equal(email, entry.ParticipantEmail);
        Assert.Equal("21.1km Open", entry.CategoryName);

        // A different organiser cannot see them
        var otherOrganiser = await _factory.LoggedInClient(RaceDayApiFactory.OtherOrganiser);
        var denied = await otherOrganiser.GetAsync($"/api/events/{ev.EventId}/enrolments");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Participant_CanViewOwnSeededResults()
    {
        var client = await _factory.LoggedInClient(RaceDayApiFactory.Participant);

        var results = await client.GetFromJsonAsync<List<ResultDto>>("/api/results/mine");

        Assert.NotNull(results);
        Assert.NotEmpty(results!);
        Assert.All(results!, r => Assert.Equal("Thabo Khumalo", r.ParticipantName));
    }

    [Fact]
    public async Task Swagger_DocumentListsTheApiEndpoints()
    {
        var client = _factory.CreateClient();

        var json = await client.GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("/api/auth/register", json);
        Assert.Contains("/api/auth/login", json);
        Assert.Contains("/api/profile", json);
        Assert.Contains("/api/events/{eventId}/categories", json);
        Assert.Contains("/api/events/{eventId}/enrolments", json);
        Assert.Contains("/api/enrolments/{enrolmentId}/result", json);
        Assert.Contains("/api/results/mine", json);
    }

    private const string SeedDataPassword = SeedData.DemoPassword;
}
