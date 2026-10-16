using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// EF Core Code-First context. Table names, keys, constraints and seed data
/// mirror the Part 1 ERD and SQL script.
/// </summary>
public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<EventType> EventTypes => Set<EventType>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---------- Role ----------
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(r => r.RoleId);
            e.Property(r => r.Name).IsRequired().HasMaxLength(20);
            e.HasIndex(r => r.Name).IsUnique();
        });

        // ---------- User ----------
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(u => u.UserId);
            e.Property(u => u.FirstName).IsRequired().HasMaxLength(50);
            e.Property(u => u.LastName).IsRequired().HasMaxLength(50);
            e.Property(u => u.Email).IsRequired().HasMaxLength(100);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.PasswordHash).IsRequired().HasMaxLength(256);
            e.Property(u => u.PhoneNumber).HasMaxLength(20);
            e.Property(u => u.DateOfBirth).HasColumnType("date");
            e.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            e.HasOne(u => u.Role)
             .WithMany(r => r.Users)
             .HasForeignKey(u => u.RoleId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- EventType ----------
        modelBuilder.Entity<EventType>(e =>
        {
            e.ToTable("EventTypes");
            e.HasKey(t => t.EventTypeId);
            e.Property(t => t.Name).IsRequired().HasMaxLength(20);
            e.HasIndex(t => t.Name).IsUnique();
        });

        // ---------- Event ----------
        modelBuilder.Entity<Event>(e =>
        {
            e.ToTable("Events");
            e.HasKey(ev => ev.EventId);
            e.Property(ev => ev.Name).IsRequired().HasMaxLength(100);
            e.Property(ev => ev.Description).IsRequired().HasMaxLength(1000);
            e.Property(ev => ev.Location).IsRequired().HasMaxLength(150);
            e.Property(ev => ev.DistanceKm).HasPrecision(6, 2);
            e.Property(ev => ev.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            e.HasOne(ev => ev.EventType)
             .WithMany(t => t.Events)
             .HasForeignKey(ev => ev.EventTypeId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(ev => ev.Organiser)
             .WithMany(u => u.OrganisedEvents)
             .HasForeignKey(ev => ev.OrganiserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Category ----------
        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.HasKey(c => c.CategoryId);
            e.Property(c => c.Name).IsRequired().HasMaxLength(50);
            e.Property(c => c.Description).HasMaxLength(250);
            e.Property(c => c.DistanceKm).HasPrecision(6, 2);
            e.Property(c => c.EntryFee).HasPrecision(8, 2).HasDefaultValue(0m);
            e.HasIndex(c => new { c.EventId, c.Name }).IsUnique();

            // Deleting an event removes its categories.
            e.HasOne(c => c.Event)
             .WithMany(ev => ev.Categories)
             .HasForeignKey(c => c.EventId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Enrolment ----------
        modelBuilder.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolments");
            e.HasKey(en => en.EnrolmentId);
            e.Property(en => en.Status).IsRequired().HasMaxLength(20).HasDefaultValue(EnrolmentStatus.Registered);
            e.Property(en => en.EnrolmentDate).HasDefaultValueSql("GETUTCDATE()");

            // A participant can only enter a specific event once.
            e.HasIndex(en => new { en.ParticipantId, en.EventId }).IsUnique();

            // Restrict avoids SQL Server "multiple cascade paths" errors
            // (Event -> Category -> Enrolment and Event -> Enrolment).
            e.HasOne(en => en.Participant)
             .WithMany(u => u.Enrolments)
             .HasForeignKey(en => en.ParticipantId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(en => en.Event)
             .WithMany(ev => ev.Enrolments)
             .HasForeignKey(en => en.EventId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(en => en.Category)
             .WithMany(c => c.Enrolments)
             .HasForeignKey(en => en.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Result ----------
        modelBuilder.Entity<Result>(e =>
        {
            e.ToTable("Results");
            e.HasKey(r => r.ResultId);
            e.Property(r => r.FinishTime).HasColumnType("time");
            e.Property(r => r.RecordedAt).HasDefaultValueSql("GETUTCDATE()");

            // One result per enrolment (1:1). The unique index is created automatically.
            e.HasOne(r => r.Enrolment)
             .WithOne(en => en.Result)
             .HasForeignKey<Result>(r => r.EnrolmentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        SeedData.Apply(modelBuilder);
    }
}
