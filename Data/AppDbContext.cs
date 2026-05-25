using Microsoft.EntityFrameworkCore;
using PatientJournalSystem.Models;

namespace PatientJournalSystem.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Journal> Journals => Set<Journal>();
    public DbSet<DoctorPatientConsent> Consents => Set<DoctorPatientConsent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Journal>()
            .HasOne(j => j.Patient)
            .WithMany(u => u.JournalsAsPatient)
            .HasForeignKey(j => j.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Journal>()
            .HasOne(j => j.Doctor)
            .WithMany(u => u.JournalsAsDoctor)
            .HasForeignKey(j => j.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DoctorPatientConsent>()
            .HasOne(c => c.Patient)
            .WithMany(u => u.ConsentsGiven)
            .HasForeignKey(c => c.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DoctorPatientConsent>()
            .HasOne(c => c.Doctor)
            .WithMany(u => u.ConsentsReceived)
            .HasForeignKey(c => c.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
