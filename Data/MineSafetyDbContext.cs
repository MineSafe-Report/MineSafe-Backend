using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MineSafety.Api.Entities;

namespace MineSafety.Api.Data;

// The DbContext is EF Core's "connection" to the database. Each DbSet is one table.
// Writing  _db.Locations.Add(x)  then  SaveChangesAsync()  makes EF write the INSERT for us.
//
// IMPORTANT: Michael's SQL scripts (database/schema) CREATE the tables. This class only
// DESCRIBES them so EF knows how to read and write. The API never creates or changes tables.
public class MineSafetyDbContext : DbContext
{
    public MineSafetyDbContext(DbContextOptions<MineSafetyDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountRole> AccountRoles => Set<AccountRole>();
    public DbSet<AccountLocation> AccountLocations => Set<AccountLocation>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<IncidentType> IncidentTypes => Set<IncidentType>();
    public DbSet<ReportStatus> ReportStatuses => Set<ReportStatus>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ReportAttachment> ReportAttachments => Set<ReportAttachment>();
    public DbSet<ReportReview> ReportReviews => Set<ReportReview>();
    public DbSet<ReportAuditLog> ReportAuditLog => Set<ReportAuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // Here we spell out every table name, primary key and any column whose name differs
    // from the C# property, so EF matches the SQL scripts exactly instead of guessing.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(x => x.RoleId);
        });

        modelBuilder.Entity<Account>(e =>
        {
            e.ToTable("Accounts");
            e.HasKey(x => x.AccountId);
            e.Property(x => x.WorkerId).HasColumnName("WorkerID");
        });

        modelBuilder.Entity<AccountRole>(e =>
        {
            e.ToTable("AccountRoles");
            e.HasKey(x => x.AccountRoleId);
            e.HasOne(x => x.Account).WithMany(a => a.AccountRoles).HasForeignKey(x => x.AccountId);
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<AccountLocation>(e =>
        {
            e.ToTable("AccountLocations");
            e.HasKey(x => x.AccountLocationId);
            e.Property(x => x.AccountLocationId).HasColumnName("AccountLocation");   // SQL name has no "ID"
            e.HasOne(x => x.Account).WithMany(a => a.AccountLocations).HasForeignKey(x => x.AccountId);
            e.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId);
        });

        modelBuilder.Entity<Location>(e =>
        {
            e.ToTable("Locations");
            e.HasKey(x => x.LocationId);
        });

        modelBuilder.Entity<IncidentType>(e =>
        {
            e.ToTable("IncidentTypes");
            e.HasKey(x => x.IncidentTypeId);
        });

        modelBuilder.Entity<ReportStatus>(e =>
        {
            e.ToTable("ReportStatuses");
            e.HasKey(x => x.StatusId);
        });

        modelBuilder.Entity<Report>(e =>
        {
            e.ToTable("Reports");
            e.HasKey(x => x.ReportId);
            e.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterAccountId);
            e.HasOne(x => x.IncidentType).WithMany().HasForeignKey(x => x.IncidentTypeId);
            e.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId);
            e.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId);
        });

        modelBuilder.Entity<ReportAttachment>(e =>
        {
            e.ToTable("ReportAttachments");
            e.HasKey(x => x.AttachmentId);
            e.Property(x => x.UploadedByAccountId).HasColumnName("UploadedByAccount"); // SQL name has no "ID"
            e.HasOne(x => x.Report).WithMany(r => r.Attachments).HasForeignKey(x => x.ReportId);
            e.HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedByAccountId);
        });

        modelBuilder.Entity<ReportReview>(e =>
        {
            e.ToTable("ReportReviews");
            e.HasKey(x => x.ReviewId);
            e.HasOne(x => x.Report).WithMany(r => r.Reviews).HasForeignKey(x => x.ReportId);
            e.HasOne(x => x.Reviewer).WithMany().HasForeignKey(x => x.ReviewerAccountId);
        });

        modelBuilder.Entity<ReportAuditLog>(e =>
        {
            e.ToTable("ReportAuditLog");
            e.HasKey(x => x.AuditLogId);
            e.Property(x => x.OldValueJson).HasColumnName("OldValueJSON");
            e.Property(x => x.NewValueJson).HasColumnName("NewValueJSON");
            e.HasOne(x => x.Report).WithMany().HasForeignKey(x => x.ReportId);
            e.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorAccountId);
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.ToTable("Notifications");
            e.HasKey(x => x.NotificationId);
            e.HasOne(x => x.Recipient).WithMany().HasForeignKey(x => x.RecipientAccountId);
            e.HasOne(x => x.Report).WithMany().HasForeignKey(x => x.ReportId);
        });

        // We never hard-delete anything (everything is soft delete), so switch off EF's
        // cascade deletes everywhere. This matches the SQL scripts, which have no cascades.
        foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
        {
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }

        // Every date in the database is stored in UTC, but SQL's DATETIME2 doesn't remember that.
        // Without this, dates come back "unlabelled" and the JSON has no "Z" on the end, so the
        // phone can't tell UTC from local time. These converters label every date read from the
        // database as UTC, so the JSON always looks like "2026-10-18T09:30:00Z".
        var utc = new ValueConverter<DateTime, DateTime>(
            v => v,
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var utcNullable = new ValueConverter<DateTime?, DateTime?>(
            v => v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
        {
            if (property.ClrType == typeof(DateTime))
                property.SetValueConverter(utc);
            else if (property.ClrType == typeof(DateTime?))
                property.SetValueConverter(utcNullable);
        }
    }
}
