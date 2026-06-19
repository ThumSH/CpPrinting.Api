using Microsoft.EntityFrameworkCore;
using CpPrinting.Api.Models;
using System.Text.Json;

namespace CpPrinting.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<DevelopmentJob> DevelopmentJobs { get; set; }
        public DbSet<SubmissionForm> Submissions { get; set; }
        public DbSet<ApprovalRecord> Approvals { get; set; }

        public DbSet<StoreInRecord> StoreInRecords { get; set; }
        public DbSet<CutRecord> CutRecords { get; set; }
        public DbSet<BundleRecord> BundleRecords { get; set; }
        public DbSet<StoreProductionRecord> StoreProductionRecords { get; set; }

        public DbSet<CPIReport> CpiReports { get; set; }
        public DbSet<AuditRecord> AuditRecords { get; set; }
        public DbSet<DeliveryTrackerReport> DeliveryTrackers { get; set; }
        public DbSet<AdviceNoteRecord> AdviceNotes { get; set; }
        public DbSet<DailyOutputRecord> DailyOutputRecords { get; set; }
        public DbSet<DowntimeRecord> DowntimeRecords { get; set; }

        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<Operator> Operators { get; set; }
        public DbSet<ColourMaster> ColourMasters { get; set; }

        public DbSet<SampleStyle> SampleStyles { get; set; }
        public DbSet<ReconciliationReportRecord> ReconciliationReports { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // INVENTORY: StoreIn -> Cuts -> Bundles
            modelBuilder.Entity<StoreInRecord>()
                .HasMany(s => s.Cuts)
                .WithOne(c => c.StoreInRecord)
                .HasForeignKey(c => c.StoreInRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CutRecord>()
                .HasMany(c => c.Bundles)
                .WithOne(b => b.CutRecord)
                .HasForeignKey(b => b.CutRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            // QC: CutInspections JSON
            modelBuilder.Entity<CPIReport>()
                .Property(e => e.CutInspections)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<CpiCutInspection>>(v, (JsonSerializerOptions?)null)!
                );

            // AUDIT: Bundles JSON
            modelBuilder.Entity<AuditRecord>()
                .Property(e => e.Bundles)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<AuditBundleSelection>>(v, (JsonSerializerOptions?)null)!
                );

            // GATEPASS: Advice Note Rows JSON
            modelBuilder.Entity<AdviceNoteRecord>()
                .Property(e => e.Rows)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, AdviceNoteRow>>(v, (JsonSerializerOptions?)null)!
                );

            // DELIVERY TRACKER: Rows JSON
            modelBuilder.Entity<DeliveryTrackerReport>()
                .Property(e => e.Rows)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<DeliveryTrackerRow>>(v, (JsonSerializerOptions?)null)!
                );

            // WORKER: TimeSlots JSON
            modelBuilder.Entity<DailyOutputRecord>()
                .Property(e => e.TimeSlots)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<TimeSlotEntry>>(v, (JsonSerializerOptions?)null)!
                );

            // DOWNTIME: Entries JSON
            modelBuilder.Entity<DowntimeRecord>()
                .Property(e => e.Entries)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<DowntimeEntry>>(v, (JsonSerializerOptions?)null)!
                );

            // SAMPLE STYLE: Revisions JSON
            // NOTE: SampleStyleRevision now includes PreviousArtworkUrl — no schema change needed
            // since it's serialized as JSON within the Revisions column.
            modelBuilder.Entity<SampleStyle>()
                .Property(e => e.Revisions)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => string.IsNullOrWhiteSpace(v)
                        ? new List<SampleStyleRevision>()
                        : JsonSerializer.Deserialize<List<SampleStyleRevision>>(v,
                                (JsonSerializerOptions?)null) ?? new()
                );
        }
    }
}