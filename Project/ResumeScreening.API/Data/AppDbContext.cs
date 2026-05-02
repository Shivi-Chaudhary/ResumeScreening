using Microsoft.EntityFrameworkCore;
using ResumeScreening.API.Models;

namespace ResumeScreening.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User>        Users        { get; set; }
        public DbSet<Job>         Jobs         { get; set; }
        public DbSet<Resume>      Resumes      { get; set; }
        public DbSet<ScoreResult> ScoreResults { get; set; }
        public DbSet<Application> Applications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── User ──────────────────────────────────────────────────────────
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Email).IsUnique();
                entity.Property(u => u.Role)
                      .HasDefaultValue("Viewer");
                entity.Property(u => u.CreatedAt)
                      .HasDefaultValueSql("GETUTCDATE()");
            });

            // ── Job ───────────────────────────────────────────────────────────
            modelBuilder.Entity<Job>(entity =>
            {
                entity.Property(j => j.Status)
                      .HasDefaultValue("Active");
                entity.Property(j => j.CreatedAt)
                      .HasDefaultValueSql("GETUTCDATE()");

                // A user can create many jobs; deleting user sets job FK to null
                entity.HasOne(j => j.CreatedBy)
                      .WithMany(u => u.Jobs)
                      .HasForeignKey(j => j.CreatedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Resume ────────────────────────────────────────────────────────
            modelBuilder.Entity<Resume>(entity =>
            {
                entity.Property(r => r.Status)
                      .HasDefaultValue("Pending");
                entity.Property(r => r.UploadedAt)
                      .HasDefaultValueSql("GETUTCDATE()");

                entity.HasIndex(r => new { r.JobId, r.UploadedByUserId })
                      .IsUnique()
                      .HasFilter("[UploadedByUserId] IS NOT NULL");

                entity.HasOne(r => r.Job)
                      .WithMany(j => j.Resumes)
                      .HasForeignKey(r => r.JobId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.UploadedBy)
                      .WithMany(u => u.UploadedResumes)
                      .HasForeignKey(r => r.UploadedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── ScoreResult ───────────────────────────────────────────────────
            modelBuilder.Entity<ScoreResult>(entity =>
            {
                entity.Property(s => s.ScoredAt)
                      .HasDefaultValueSql("GETUTCDATE()");

                // One resume → one score result
                entity.HasOne(s => s.Resume)
                      .WithOne(r => r.ScoreResult)
                      .HasForeignKey<ScoreResult>(s => s.ResumeId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(s => s.Job)
                      .WithMany()
                      .HasForeignKey(s => s.JobId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // ── Application ───────────────────────────────────────────────────
            modelBuilder.Entity<Application>(entity =>
            {
                entity.Property(a => a.HRStatus)
                      .HasDefaultValue("Pending");
                entity.Property(a => a.UpdatedAt)
                      .HasDefaultValueSql("GETUTCDATE()");

                // One resume → one application record
                entity.HasOne(a => a.Resume)
                      .WithOne(r => r.Application)
                      .HasForeignKey<Application>(a => a.ResumeId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.Job)
                      .WithMany()
                      .HasForeignKey(a => a.JobId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // ── Seed Data ─────────────────────────────────────────────────────
            // Default HRAdmin user (password: Admin@123 — hashed below)
            // Re-generate hash using BCrypt before production use
            modelBuilder.Entity<User>().HasData(new User
            {
                Id           = 1,
                FullName     = "HR Administrator",
                Email        = "admin@resumescreening.com",
                // BCrypt hash of "Admin@123" — replace with your own hash
                PasswordHash = "$2a$11$ExampleHashReplaceThisBeforeUse.PlaceholderOnly",
                Role         = "HRAdmin",
                CreatedAt    = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive     = true
            });
        }
    }
}
