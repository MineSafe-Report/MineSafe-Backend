using Microsoft.EntityFrameworkCore;
using MineSafety.Api.Entities;

namespace MineSafety.Api.Data;

// The DbContext is EF Core's "connection" to the database.
// Each DbSet below is one table. Writing  _db.Locations.Add(x)  then  SaveChangesAsync()
// makes EF write the INSERT statement for us.
public class MineSafetyDbContext : DbContext
{
    public MineSafetyDbContext(DbContextOptions<MineSafetyDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<IncidentType> IncidentTypes => Set<IncidentType>();
    public DbSet<ReportStatus> ReportStatuses => Set<ReportStatus>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ReportAttachment> ReportAttachments => Set<ReportAttachment>();
    public DbSet<ReportReview> ReportReviews => Set<ReportReview>();
    public DbSet<ReportAuditLog> ReportAuditLog => Set<ReportAuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // Extra rules EF can't guess from the classes alone: unique values, table names, delete rules.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReportStatus>().HasKey(s => s.StatusId);
        modelBuilder.Entity<ReportAttachment>().HasKey(a => a.AttachmentId);
        modelBuilder.Entity<ReportReview>().HasKey(r => r.ReviewId);
        modelBuilder.Entity<ReportAuditLog>().HasKey(a => a.AuditLogId);
        modelBuilder.Entity<ReportAuditLog>().ToTable("ReportAuditLog");

        // UNIQUE rules: the database itself refuses duplicates, even if our code has a bug.
        modelBuilder.Entity<Role>().HasIndex(r => r.RoleName).IsUnique();
        modelBuilder.Entity<Account>().HasIndex(a => a.WorkerId).IsUnique();
        modelBuilder.Entity<Account>().HasIndex(a => a.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
        modelBuilder.Entity<Location>().HasIndex(l => l.LocationCode).IsUnique();
        modelBuilder.Entity<Report>().HasIndex(r => r.ReportNumber).IsUnique();
        modelBuilder.Entity<Report>().HasIndex(r => r.ClientReportId).IsUnique();

        // Accounts are linked to reports, reviews, audit rows and notifications.
        // SQL Server refuses "cascade delete" when a table can be reached by more than one path,
        // and we soft delete anyway, so we turn cascading off everywhere: nothing auto-deletes.
        foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
}
