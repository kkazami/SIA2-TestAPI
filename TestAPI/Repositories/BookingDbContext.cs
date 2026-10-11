using Microsoft.EntityFrameworkCore;
using TestAPI.Models;

namespace TestAPI.Repositories;

public class BookingDbContext(DbContextOptions<BookingDbContext> options)
    : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<Availability> Slots => Set<Availability>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<LoginSession> Sessions => Set<LoginSession>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<UserAccount>().HasIndex(u => u.Email).IsUnique();
        model.Entity<LoginSession>().HasIndex(s => s.TokenHash).IsUnique();
        // One active booking per slot, even when requests arrive together.
        model.Entity<Appointment>().HasIndex(a => a.AvailabilityId)
            .IsUnique().HasFilter("Status <> 'Cancelled'");

        model.Entity<Availability>().HasOne<UserAccount>().WithMany()
            .HasForeignKey(s => s.TutorId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Appointment>().HasOne<UserAccount>().WithMany()
            .HasForeignKey(a => a.StudentId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Appointment>().HasOne<Availability>().WithMany()
            .HasForeignKey(a => a.AvailabilityId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<LoginSession>().HasOne<UserAccount>().WithMany()
            .HasForeignKey(s => s.UserId);

        // SQLite stores dates as text; restore their UTC kind when reading.
        model.Entity<Availability>().Property(s => s.StartTime)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        model.Entity<Availability>().Property(s => s.EndTime)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        model.Entity<LoginSession>().Property(s => s.ExpiresUtc)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
    }
}
