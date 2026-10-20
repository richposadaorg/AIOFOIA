using FoiaProcessor.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FoiaProcessor.Data;

public class FoiaDbContext : DbContext
{
    public FoiaDbContext(DbContextOptions<FoiaDbContext> options) : base(options) { }

    public DbSet<FoiaRequest> FoiaRequests => Set<FoiaRequest>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Redaction> Redactions => Set<Redaction>();
    public DbSet<ReviewTask> ReviewTasks => Set<ReviewTask>();
    public DbSet<ReleasePackage> ReleasePackages => Set<ReleasePackage>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // -------- enums as strings --------
        modelBuilder.Entity<FoiaRequest>().Property(e => e.Status).HasConversion<string>();
        modelBuilder.Entity<Document>().Property(e => e.RedactionStatus).HasConversion<string>();
        modelBuilder.Entity<Document>().Property(e => e.ReviewStatus).HasConversion<string>();
        modelBuilder.Entity<Redaction>().Property(e => e.PiiType).HasConversion<string>();
        modelBuilder.Entity<Redaction>().Property(e => e.DetectionSource).HasConversion<string>();
        modelBuilder.Entity<ReviewTask>().Property(e => e.Status).HasConversion<string>();
        modelBuilder.Entity<ReviewTask>().Property(e => e.ReviewerDecision).HasConversion<string?>();
        modelBuilder.Entity<AuditEvent>().Property(e => e.EventType).HasConversion<string>();

        // SQL Server has a native `date` type for DateOnly (EF 8+); no converter needed.

        // -------- relationships --------
        modelBuilder.Entity<FoiaRequest>()
            .HasMany(r => r.Documents)
            .WithOne(d => d.FoiaRequest!)
            .HasForeignKey(d => d.FoiaRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FoiaRequest>()
            .HasMany(r => r.ReviewTasks)
            .WithOne(t => t.FoiaRequest!)
            .HasForeignKey(t => t.FoiaRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FoiaRequest>()
            .HasMany(r => r.ReleasePackages)
            .WithOne(p => p.FoiaRequest!)
            .HasForeignKey(p => p.FoiaRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReleasePackage>()
            .HasMany(p => p.Documents)
            .WithOne(d => d.ReleasePackage!)
            .HasForeignKey(d => d.ReleasePackageId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FoiaRequest>()
            .HasMany(r => r.AuditEvents)
            .WithOne(a => a.FoiaRequest!)
            .HasForeignKey(a => a.FoiaRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Document>()
            .HasMany(d => d.Redactions)
            .WithOne(r => r.Document!)
            .HasForeignKey(r => r.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // -------- indexes --------
        modelBuilder.Entity<FoiaRequest>().HasIndex(r => r.Status);
        modelBuilder.Entity<Document>().HasIndex(d => d.FoiaRequestId);
        modelBuilder.Entity<AuditEvent>().HasIndex(a => new { a.FoiaRequestId, a.Timestamp });
        modelBuilder.Entity<ReleasePackage>().HasIndex(p => new { p.FoiaRequestId, p.Version }).IsUnique();

        // -------- string lengths (per data-model.md) --------
        modelBuilder.Entity<FoiaRequest>().Property(e => e.Subject).HasMaxLength(200).IsRequired();
        modelBuilder.Entity<FoiaRequest>().Property(e => e.Description).HasMaxLength(2000);
        modelBuilder.Entity<FoiaRequest>().Property(e => e.RequestorFullName).HasMaxLength(200).IsRequired();
        modelBuilder.Entity<FoiaRequest>().Property(e => e.RequestorOrganization).HasMaxLength(200);
        modelBuilder.Entity<FoiaRequest>().Property(e => e.RequestorEmail).HasMaxLength(320).IsRequired();
        modelBuilder.Entity<FoiaRequest>().Property(e => e.RequestorPhone).HasMaxLength(40);
        modelBuilder.Entity<FoiaRequest>().Property(e => e.RequestorMailingAddress).HasMaxLength(500);

        modelBuilder.Entity<Document>().Property(e => e.SourceDocumentId).HasMaxLength(200);
        modelBuilder.Entity<Document>().Property(e => e.FileName).HasMaxLength(300);
        modelBuilder.Entity<Document>().Property(e => e.FileType).HasMaxLength(50);
        modelBuilder.Entity<Document>().Property(e => e.SourceUri).HasMaxLength(2000);

        modelBuilder.Entity<Redaction>().Property(e => e.OriginalText).HasMaxLength(500);
        modelBuilder.Entity<Redaction>().Property(e => e.ReplacementText).HasMaxLength(60);
        modelBuilder.Entity<Redaction>().Property(e => e.ReviewerComments).HasMaxLength(1000);

        modelBuilder.Entity<ReviewTask>().Property(e => e.AssignedReviewer).HasMaxLength(200);
        modelBuilder.Entity<ReviewTask>().Property(e => e.ReviewerComments).HasMaxLength(1000);

        modelBuilder.Entity<ReleasePackage>().Property(e => e.ZipBlobName).HasMaxLength(300);
        modelBuilder.Entity<ReleasePackage>().Property(e => e.BlobContainerName).HasMaxLength(100);
        modelBuilder.Entity<ReleasePackage>().Property(e => e.SasUrl).HasMaxLength(2000);

        modelBuilder.Entity<ReleasePackageDocument>().Property(e => e.FileName).HasMaxLength(300);
        modelBuilder.Entity<ReleasePackageDocument>().Property(e => e.PackageFileName).HasMaxLength(300);
        modelBuilder.Entity<ReleasePackageDocument>().Property(e => e.ContentSha256).HasMaxLength(64);

        modelBuilder.Entity<AuditEvent>().Property(e => e.Message).HasMaxLength(1000);
    }
}
