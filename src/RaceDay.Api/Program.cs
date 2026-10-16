using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------- Database (EF Core, Code-First) ----------
// Integration tests run in the "Testing" environment and register an in-memory database instead.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<RaceDayDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("RaceDayDb")));
}

// ---------- Password hashing (PBKDF2 + salt, never plain text) ----------
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// ---------- Session management ----------
// After login the user's id and role live in a server-side session. The client only
// holds an opaque, HttpOnly session cookie that it sends with every request.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

builder.Services.AddControllers();

// ---------- Swagger ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description =
            "RESTful API for the RaceDay road running, walking and cycling event platform.\n\n" +
            "**How to test:** call `POST /api/auth/login` first. The API returns a session cookie " +
            "that Swagger UI sends automatically with every following request.\n\n" +
            "Seeded accounts (password `RaceDay@123`):\n" +
            "- Organisers: `sipho.ndlovu@raceday.co.za`, `lerato.mokoena@raceday.co.za`\n" +
            "- Participants: `thabo.khumalo@gmail.com`, `ayesha.patel@gmail.com`, `johan.vdmerwe@gmail.com`"
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// ---------- Create / migrate the database on startup ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
    if (db.Database.IsRelational() && db.Database.GetMigrations().Any())
        db.Database.Migrate();      // normal path: apply EF Core migrations
    else
        db.Database.EnsureCreated(); // in-memory tests, or before the first migration is added
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1");
    options.DocumentTitle = "RaceDay API";
});

if (!app.Environment.IsEnvironment("Testing"))
    app.UseHttpsRedirection();

app.UseSession();
app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

// Makes Program visible to WebApplicationFactory in the integration tests.
public partial class Program { }
