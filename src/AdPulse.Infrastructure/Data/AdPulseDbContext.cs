using System.Security;
using AdPulse.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace AdPulse.Infrastructure.Data;

public sealed class AdPulseDbContext(DbContextOptions<AdPulseDbContext> options, TenantScope scope) : DbContext(options)
{
    public TenantScope Scope { get; } = scope;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<DailyMetric> DailyMetrics => Set<DailyMetric>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportRowError> ImportRowErrors => Set<ImportRowError>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var isSqlServer = Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer";
        var isSqlite = Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(64);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CreatedAt).HasPrecision(0);
            entity.HasQueryFilter(x => Scope.IsSystemScope || x.Id == Scope.TenantId);
        });

        modelBuilder.Entity<Membership>(entity =>
        {
            entity.HasKey(x => new { x.TenantId, x.SubjectId, x.Role });
            entity.Property(x => x.TenantId).HasMaxLength(64);
            entity.Property(x => x.SubjectId).HasMaxLength(128);
            entity.Property(x => x.Role).HasMaxLength(64);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .HasPrincipalKey(x => x.Id)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => Scope.IsSystemScope || x.TenantId == Scope.TenantId);
        });

        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.HasKey(x => new { x.TenantId, x.Id });
            entity.Property(x => x.TenantId).HasMaxLength(64);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Currency).HasColumnType("char(3)").IsRequired();
            entity.Property(x => x.Budget).HasPrecision(19, 4);
            entity.Property(x => x.CreatedAt).HasPrecision(0);
            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .HasPrincipalKey(x => x.Id)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.TenantId, x.Status })
                .HasDatabaseName("IX_Campaigns_Tenant_Status");
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Campaigns_Budget", isSqlite ? "CAST(Budget AS REAL) >= 0" : "Budget >= 0");
                table.HasCheckConstraint("CK_Campaigns_DateRange", "EndsOn >= StartsOn");
                if (isSqlServer)
                {
                    table.HasCheckConstraint("CK_Campaigns_Currency", "Currency LIKE '[A-Z][A-Z][A-Z]'");
                }
            });
            entity.HasQueryFilter(x => Scope.IsSystemScope || x.TenantId == Scope.TenantId);
        });

        modelBuilder.Entity<DailyMetric>(entity =>
        {
            entity.HasKey(x => new { x.TenantId, x.CampaignId, x.MetricDate });
            entity.Property(x => x.TenantId).HasMaxLength(64);
            entity.Property(x => x.Spend).HasPrecision(19, 4);
            entity.Property(x => x.Revenue).HasPrecision(19, 4);
            entity.HasOne(x => x.Campaign)
                .WithMany(x => x.DailyMetrics)
                .HasForeignKey(x => new { x.TenantId, x.CampaignId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_DailyMetrics_NonNegative",
                    isSqlite
                        ? "CAST(Spend AS REAL) >= 0 AND CAST(Revenue AS REAL) >= 0 AND Impressions >= 0 AND Clicks >= 0 AND Conversions >= 0"
                        : "Spend >= 0 AND Revenue >= 0 AND Impressions >= 0 AND Clicks >= 0 AND Conversions >= 0");
                table.HasCheckConstraint("CK_DailyMetrics_ClicksWithinImpressions", "Clicks <= Impressions");
                table.HasCheckConstraint("CK_DailyMetrics_ConversionsWithinClicks", "Conversions <= Clicks");
            });
            entity.HasIndex(x => new { x.TenantId, x.CampaignId, x.MetricDate })
                .IsDescending(false, false, true)
                .HasDatabaseName("IX_DailyMetrics_Tenant_Campaign_Date");
            entity.HasQueryFilter(x => Scope.IsSystemScope || x.TenantId == Scope.TenantId);
        });

        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.HasKey(x => new { x.TenantId, x.Id });
            entity.Property(x => x.TenantId).HasMaxLength(64);
            entity.Property(x => x.UploadedBySubjectId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.UploadedAt).HasPrecision(0);
            entity.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .HasPrincipalKey(x => x.Id)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("CK_ImportBatches_RowCounts", "AcceptedRows >= 0 AND RejectedRows >= 0"));
            entity.HasIndex(x => new { x.TenantId, x.UploadedAt })
                .IsDescending(false, true)
                .HasDatabaseName("IX_ImportBatches_Tenant_UploadedAt");
            entity.HasQueryFilter(x => Scope.IsSystemScope || x.TenantId == Scope.TenantId);
        });

        modelBuilder.Entity<ImportRowError>(entity =>
        {
            entity.HasKey(x => new { x.TenantId, x.ImportBatchId, x.RowNumber });
            entity.Property(x => x.TenantId).HasMaxLength(64);
            entity.Property(x => x.ErrorCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(500).IsRequired();
            entity.HasOne(x => x.ImportBatch)
                .WithMany(x => x.RowErrors)
                .HasForeignKey(x => new { x.TenantId, x.ImportBatchId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("CK_ImportRowErrors_RowNumber", "RowNumber > 0"));
            entity.HasQueryFilter(x => Scope.IsSystemScope || x.TenantId == Scope.TenantId);
        });

        modelBuilder.Entity<Alert>(entity =>
        {
            entity.HasKey(x => new { x.TenantId, x.Id });
            entity.Property(x => x.TenantId).HasMaxLength(64);
            entity.Property(x => x.Fingerprint).HasMaxLength(64).HasColumnType("char(64)").IsRequired();
            entity.Property(x => x.MetricName).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Score).HasPrecision(9, 4);
            entity.Property(x => x.Severity).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Observed).HasPrecision(19, 4);
            entity.Property(x => x.Baseline).HasPrecision(19, 4);
            entity.Property(x => x.Confidence).HasPrecision(9, 4);
            entity.Property(x => x.DataSufficiency).HasMaxLength(32).IsRequired();
            entity.Property(x => x.ReasonCodesJson).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.ScoringVersion).HasMaxLength(32).IsRequired();
            entity.Property(x => x.DetectedAt).HasPrecision(0);
            entity.HasIndex(x => new { x.TenantId, x.Fingerprint })
                .IsUnique()
                .HasDatabaseName("UQ_Alerts_TenantFingerprint");
            entity.HasIndex(x => new { x.TenantId, x.Status, x.DetectedAt })
                .IsDescending(false, false, true)
                .HasDatabaseName("IX_Alerts_Tenant_Status_DetectedAt");
            entity.HasOne(x => x.Campaign)
                .WithMany(x => x.Alerts)
                .HasForeignKey(x => new { x.TenantId, x.CampaignId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Alerts_Score",
                    isSqlite ? "CAST(Score AS REAL) >= 0 AND CAST(Score AS REAL) <= 100" : "Score >= 0 AND Score <= 100");
                table.HasCheckConstraint(
                    "CK_Alerts_Confidence",
                    isSqlite ? "CAST(Confidence AS REAL) >= 0 AND CAST(Confidence AS REAL) <= 1" : "Confidence >= 0 AND Confidence <= 1");
                table.HasCheckConstraint(
                    "CK_Alerts_ObservedBaseline",
                    isSqlite ? "CAST(Observed AS REAL) >= 0 AND CAST(Baseline AS REAL) >= 0" : "Observed >= 0 AND Baseline >= 0");
                table.HasCheckConstraint("CK_Alerts_DateRange", "EvidenceWindowEnd >= EvidenceWindowStart");
                if (isSqlServer)
                {
                    table.HasCheckConstraint("CK_Alerts_ReasonCodesJson", "ISJSON(ReasonCodesJson) = 1");
                }
            });
            entity.HasQueryFilter(x => Scope.IsSystemScope || x.TenantId == Scope.TenantId);
        });

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.HasKey(x => new { x.TenantId, x.Id });
            entity.Property(x => x.TenantId).HasMaxLength(64);
            entity.Property(x => x.OccurredAt).HasPrecision(0);
            entity.Property(x => x.ActorSubjectId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(64).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.PayloadJson).IsRequired();
            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .HasPrincipalKey(x => x.Id)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.TenantId, x.OccurredAt })
                .IsDescending(false, true)
                .HasDatabaseName("IX_AuditEvents_Tenant_OccurredAt");
            if (isSqlServer)
            {
                entity.ToTable(table => table.HasCheckConstraint("CK_AuditEvents_PayloadJson", "ISJSON(PayloadJson) = 1"));
            }
            entity.HasQueryFilter(x => Scope.IsSystemScope || x.TenantId == Scope.TenantId);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidateChanges()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is AuditEvent && entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("Audit events are append-only.");
            }

            if (Scope.IsSystemScope || entry.State == EntityState.Unchanged)
            {
                continue;
            }

            if (entry.Entity is ITenantOwned tenantOwned && !string.Equals(tenantOwned.TenantId, Scope.TenantId, StringComparison.Ordinal))
            {
                throw new SecurityException("The entity is outside the current tenant scope.");
            }
        }
    }
}
