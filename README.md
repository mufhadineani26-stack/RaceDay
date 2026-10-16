# RaceDay – Part 2: RESTful API

RaceDay is a web-based event management system for the South African road running, walking and cycling community. Organisers create and manage events, categories and results; Participants browse events, enter them by choosing a category, and track their personal results.

This part is the **ASP.NET Core Web API** (C#, .NET 8) that powers the system. It uses **Entity Framework Core (Code-First)** with **SQL Server**, **session-based role authentication**, **Swagger UI**, and **xUnit tests** run by **GitHub Actions**.

## 📹 Video presentation





## ✅ CI/CD green build

<img width="1917" height="695" alt="image" src="https://github.com/user-attachments/assets/28e8704e-a319-4dd7-aa31-5add25aa45f7" />


```markdown
![CI build passing](docs/ci-green-build.png)
```

---

## User roles

| Role | What they can do |
|---|---|
| **Organiser** | Create, update and delete their own events · define age/distance categories for their events · view all enrolments for their events · capture, correct and remove finish times and positions · view and update their own profile |
| **Participant** | Register and log in · browse events and categories · enter an event by selecting a category · view and withdraw their own enrolments · view their own results · view and update their own profile |

**How roles are managed:** roles are stored in a `Roles` lookup table. A user picks *Organiser* or *Participant* once at registration; it is validated against that table and saved as a foreign key on the user. The role cannot be changed through the profile endpoints. At login the role is written into the server-side session, and every protected endpoint checks it with the `[SessionAuthorize]` filter:

- not logged in → **401 Unauthorized**
- logged in with the wrong role → **403 Forbidden**
- Organisers can only change *their own* events, categories and results (otherwise **403**).

Passwords are hashed with ASP.NET Core's `PasswordHasher` (PBKDF2 + random salt) and never stored in plain text.

---

## Project structure

```
RaceDay.sln
├── .github/workflows/part2-api-ci.yml   # build + test on every push
├── docs/API-Endpoints.md                # endpoints as implemented
├── src/RaceDay.Api/
│   ├── Auth/SessionAuthorizeAttribute.cs  # session + role checks (401/403)
│   ├── Controllers/                       # Auth, Profile, Events, Categories, Enrolments, Results
│   ├── Data/RaceDayDbContext.cs           # EF Core model: keys, FKs, constraints
│   ├── Data/SeedData.cs                   # realistic sample data
│   ├── Dtos/Dtos.cs                       # request/response models + validation
│   ├── Models/Entities.cs                 # Role, User, EventType, Event, Category, Enrolment, Result
│   └── Program.cs                         # DI, session, Swagger, DB startup
└── tests/RaceDay.Api.Tests/
    ├── Unit/          # controller + filter tests (in-memory DB)
    └── Integration/   # real HTTP pipeline: login, sessions, 401/403
```

### Data model (7 tables)

`Roles` 1—* `Users` · `EventTypes` 1—* `Events` · `Users (Organiser)` 1—* `Events` · `Events` 1—* `Categories` · `Users (Participant)` 1—* `Enrolments` *—1 `Events` · `Categories` 1—* `Enrolments` · `Enrolments` 1—1 `Results`

Key constraints: unique `Users.Email`, unique `(EventId, Name)` per category, unique `(ParticipantId, EventId)` so a participant enters an event once, one result per enrolment. Enrolment foreign keys use `RESTRICT` to avoid SQL Server multiple-cascade-path errors.

---

## Setup and run

### Prerequisites
- Visual Studio 2022 (ASP.NET and web development workload) **or** the .NET 8 SDK
- SQL Server LocalDB (installed with Visual Studio) or any SQL Server instance
- EF Core CLI tools: `dotnet tool install --global dotnet-ef`

### 1. Connection string
`src/RaceDay.Api/appsettings.json` points at LocalDB by default:

```json
"ConnectionStrings": {
  "RaceDayDb": "Server=(localdb)\\MSSQLLocalDB;Database=RaceDayDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```
Change it if you use a different SQL Server instance.

### 2. Create the database (Code-First migration)
From the repository root:

```bash
dotnet restore
dotnet ef migrations add InitialCreate --project src/RaceDay.Api
dotnet ef database update --project src/RaceDay.Api
```
(In Visual Studio's Package Manager Console: `Add-Migration InitialCreate` then `Update-Database`.)

The migration creates all tables, keys and constraints and inserts the seed data. The app also applies pending migrations automatically on startup.

### 3. Run the API
```bash
dotnet run --project src/RaceDay.Api --launch-profile https
```
or press **F5** in Visual Studio. Swagger UI opens at `https://localhost:7284/swagger`.

### 4. Try it in Swagger
1. Expand **POST /api/auth/login** → *Try it out* and log in with a seeded account.
2. The session cookie is now stored by the browser, so every other endpoint works from Swagger without extra tooling.
3. Use **POST /api/auth/logout** and log in as the other role to see 403 responses.

| Role | Email | Password |
|---|---|---|
| Organiser | `sipho.ndlovu@raceday.co.za` | `RaceDay@123` |
| Organiser | `lerato.mokoena@raceday.co.za` | `RaceDay@123` |
| Participant | `thabo.khumalo@gmail.com` | `RaceDay@123` |
| Participant | `ayesha.patel@gmail.com` | `RaceDay@123` |
| Participant | `johan.vdmerwe@gmail.com` | `RaceDay@123` |

### 5. Run the tests
```bash
dotnet test
```
Tests use EF Core's in-memory provider, so no SQL Server is needed for testing (locally or in CI).

**Coverage (success and failure cases):**
- Registration and login – valid/duplicate/invalid role/wrong password, hashing, session contents, logout
- Event management – create/update/delete by owner, past date, unknown type, other organiser's event (403), delete with enrolments (409)
- Role rejection – participant on organiser endpoints and organiser on participant endpoints (403), unauthenticated requests (401), both as unit tests on the filter and over real HTTP
- Enrolments – enrol and verify the participant/event/category link is saved, duplicate, wrong category, past event, age limits, full category, organiser view of entries
- Results, categories and profile – capture after event, before event, duplicate position, own-results only

---

## CI/CD
`.github/workflows/part2-api-ci.yml` restores, builds and runs all tests on every push and pull request using .NET 8 on Ubuntu, and uploads the test results as an artifact.

