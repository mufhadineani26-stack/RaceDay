using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Tests.Integration;

/// <summary>
/// Hosts the real API in memory (real routing, session middleware and filters)
/// with an isolated in-memory database, so tests don't need SQL Server.
/// </summary>
public class RaceDayApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"RaceDayIntegration_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<RaceDayDbContext>>();
            services.AddDbContext<RaceDayDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }

    /// <summary>A client with its own cookie jar, already logged in.</summary>
    public async Task<HttpClient> LoggedInClient(string email, string password = SeedData.DemoPassword)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
        response.EnsureSuccessStatusCode();
        return client;
    }

    public const string Organiser = "sipho.ndlovu@raceday.co.za";
    public const string OtherOrganiser = "lerato.mokoena@raceday.co.za";
    public const string Participant = "thabo.khumalo@gmail.com";
}
