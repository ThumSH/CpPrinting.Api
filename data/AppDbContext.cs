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
        public DbSet<StoreProductionRecord> StoreProductionRecords { get; set; }
        public DbSet<CPIReport> CpiReports { get; set; }
        public DbSet<AuditRecord> AuditRecords { get; set; }
        public DbSet<DeliveryTrackerReport> DeliveryTrackers { get; set; }
        
        // NEW: Gatepass Table
        public DbSet<AdviceNoteRecord> AdviceNotes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. QC Grid JSON mapping
            modelBuilder.Entity<CPIReport>()
                .Property(e => e.InspectionRows)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, CPIRowData>>(v, (JsonSerializerOptions?)null)!
                );

            // 2. Audit Bundles JSON mapping
            modelBuilder.Entity<AuditRecord>()
                .OwnsMany(a => a.Bundles, builder => { builder.ToJson(); });

            // 3. NEW: Gatepass Rows JSON mapping
            modelBuilder.Entity<AdviceNoteRecord>()
                .Property(e => e.Rows)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, AdviceNoteRow>>(v, (JsonSerializerOptions?)null)!
                );

                modelBuilder.Entity<DeliveryTrackerReport>()
                    .Property(e => e.Rows)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<List<DeliveryTrackerRow>>(v, (JsonSerializerOptions?)null)!
                    );
                }
    }
}