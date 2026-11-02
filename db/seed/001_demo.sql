/* [Demo data] Synthetic local-development data only. No secrets or production identifiers. */
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @AcmeTenantId nvarchar(64) = N'demo-acme';
    DECLARE @NorthstarTenantId nvarchar(64) = N'demo-northstar';
    DECLARE @AcmeCampaignId uniqueidentifier = '11111111-1111-4111-8111-111111111111';
    DECLARE @AcmeBrandCampaignId uniqueidentifier = '11111111-1111-4111-8111-111111111112';
    DECLARE @NorthstarCampaignId uniqueidentifier = '22222222-2222-4222-8222-222222222221';
    DECLARE @ImportBatchId uniqueidentifier = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
    DECLARE @AcmeAlertId uniqueidentifier = '33333333-3333-4333-8333-333333333331';
    DECLARE @NorthstarAlertId uniqueidentifier = '33333333-3333-4333-8333-333333333332';
    DECLARE @AcmeAuditId uniqueidentifier = '44444444-4444-4444-8444-444444444441';
    DECLARE @NorthstarAuditId uniqueidentifier = '44444444-4444-4444-8444-444444444442';

    IF NOT EXISTS (SELECT 1 FROM dbo.Tenants WHERE Id = @AcmeTenantId)
    BEGIN
        INSERT dbo.Tenants (Id, Name, CreatedAt)
        VALUES (@AcmeTenantId, N'Acme Retail Demo', CAST('2026-01-01T00:00:00+00:00' AS datetimeoffset(0)));
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.Tenants WHERE Id = @NorthstarTenantId)
    BEGIN
        INSERT dbo.Tenants (Id, Name, CreatedAt)
        VALUES (@NorthstarTenantId, N'Northstar Media Demo', CAST('2026-01-01T00:00:00+00:00' AS datetimeoffset(0)));
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.Memberships
        WHERE TenantId = @AcmeTenantId
          AND SubjectId = N'demo-user-acme'
          AND Role = N'tenant_admin'
    )
    BEGIN
        INSERT dbo.Memberships (TenantId, SubjectId, Role)
        VALUES (@AcmeTenantId, N'demo-user-acme', N'tenant_admin');
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.Memberships
        WHERE TenantId = @AcmeTenantId
          AND SubjectId = N'demo-analyst-acme'
          AND Role = N'analyst'
    )
    BEGIN
        INSERT dbo.Memberships (TenantId, SubjectId, Role)
        VALUES (@AcmeTenantId, N'demo-analyst-acme', N'analyst');
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.Memberships
        WHERE TenantId = @NorthstarTenantId
          AND SubjectId = N'demo-user-northstar'
          AND Role = N'tenant_admin'
    )
    BEGIN
        INSERT dbo.Memberships (TenantId, SubjectId, Role)
        VALUES (@NorthstarTenantId, N'demo-user-northstar', N'tenant_admin');
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.Campaigns WHERE TenantId = @AcmeTenantId AND Id = @AcmeCampaignId)
    BEGIN
        INSERT dbo.Campaigns
            (TenantId, Id, Name, Status, Currency, Budget, StartsOn, EndsOn, CreatedAt)
        VALUES
            (@AcmeTenantId, @AcmeCampaignId, N'Spring Search Demo', N'active', 'USD', 25000.0000,
             '2026-01-01', '2026-03-31', CAST('2026-01-01T00:00:00+00:00' AS datetimeoffset(0)));
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.Campaigns WHERE TenantId = @AcmeTenantId AND Id = @AcmeBrandCampaignId)
    BEGIN
        INSERT dbo.Campaigns
            (TenantId, Id, Name, Status, Currency, Budget, StartsOn, EndsOn, CreatedAt)
        VALUES
            (@AcmeTenantId, @AcmeBrandCampaignId, N'Brand Awareness Demo', N'paused', 'USD', 18000.0000,
             '2026-01-15', '2026-04-30', CAST('2026-01-15T00:00:00+00:00' AS datetimeoffset(0)));
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.Campaigns WHERE TenantId = @NorthstarTenantId AND Id = @NorthstarCampaignId)
    BEGIN
        INSERT dbo.Campaigns
            (TenantId, Id, Name, Status, Currency, Budget, StartsOn, EndsOn, CreatedAt)
        VALUES
            (@NorthstarTenantId, @NorthstarCampaignId, N'Launch Retargeting Demo', N'active', 'EUR', 12000.0000,
             '2026-02-01', '2026-04-30', CAST('2026-02-01T00:00:00+00:00' AS datetimeoffset(0)));
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.DailyMetrics
        WHERE TenantId = @AcmeTenantId AND CampaignId = @AcmeCampaignId AND MetricDate = '2026-02-01'
    )
    BEGIN
        INSERT dbo.DailyMetrics
            (TenantId, CampaignId, MetricDate, Spend, Revenue, Impressions, Clicks, Conversions)
        VALUES
            (@AcmeTenantId, @AcmeCampaignId, '2026-02-01', 1250.5000, 4200.0000, 100000, 2500, 125);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.DailyMetrics
        WHERE TenantId = @AcmeTenantId AND CampaignId = @AcmeCampaignId AND MetricDate = '2026-02-02'
    )
    BEGIN
        INSERT dbo.DailyMetrics
            (TenantId, CampaignId, MetricDate, Spend, Revenue, Impressions, Clicks, Conversions)
        VALUES
            (@AcmeTenantId, @AcmeCampaignId, '2026-02-02', 1400.2500, 3900.0000, 105000, 2300, 110);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.DailyMetrics
        WHERE TenantId = @AcmeTenantId AND CampaignId = @AcmeBrandCampaignId AND MetricDate = '2026-02-01'
    )
    BEGIN
        INSERT dbo.DailyMetrics
            (TenantId, CampaignId, MetricDate, Spend, Revenue, Impressions, Clicks, Conversions)
        VALUES
            (@AcmeTenantId, @AcmeBrandCampaignId, '2026-02-01', 600.0000, 1500.0000, 60000, 1200, 40);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.DailyMetrics
        WHERE TenantId = @NorthstarTenantId AND CampaignId = @NorthstarCampaignId AND MetricDate = '2026-02-01'
    )
    BEGIN
        INSERT dbo.DailyMetrics
            (TenantId, CampaignId, MetricDate, Spend, Revenue, Impressions, Clicks, Conversions)
        VALUES
            (@NorthstarTenantId, @NorthstarCampaignId, '2026-02-01', 450.0000, 1100.0000, 30000, 750, 32);
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.ImportBatches WHERE TenantId = @AcmeTenantId AND Id = @ImportBatchId)
    BEGIN
        INSERT dbo.ImportBatches
            (TenantId, Id, UploadedBySubjectId, UploadedAt, FileName, AcceptedRows, RejectedRows, Status)
        VALUES
            (@AcmeTenantId, @ImportBatchId, N'demo-user-acme',
             CAST('2026-02-03T12:00:00+00:00' AS datetimeoffset(0)), N'demo-metrics.csv', 3, 1, N'completed');
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.ImportRowErrors
        WHERE TenantId = @AcmeTenantId AND ImportBatchId = @ImportBatchId AND RowNumber = 4
    )
    BEGIN
        INSERT dbo.ImportRowErrors
            (TenantId, ImportBatchId, RowNumber, ErrorCode, Message)
        VALUES
            (@AcmeTenantId, @ImportBatchId, 4, N'INVALID_DATE', N'Metric date is outside the accepted ISO date format.');
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.Alerts WHERE TenantId = @AcmeTenantId AND Id = @AcmeAlertId)
    BEGIN
        INSERT dbo.Alerts
            (TenantId, Id, CampaignId, Fingerprint, MetricName, Score, Severity, Status,
             Observed, Baseline, Confidence, DataSufficiency, ReasonCodesJson, ScoringVersion,
             EvidenceWindowStart, EvidenceWindowEnd, DetectedAt)
        VALUES
            (@AcmeTenantId, @AcmeAlertId, @AcmeCampaignId,
             '1111111111111111111111111111111111111111111111111111111111111111',
             N'ctr', 72.5000, N'high', N'open', 0.0230, 0.0350, 0.8750, N'sufficient',
             N'["degraded_vs_baseline"]', N'scoring-v1', '2026-02-01', '2026-02-02',
             CAST('2026-02-03T12:05:00+00:00' AS datetimeoffset(0)));
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.Alerts WHERE TenantId = @NorthstarTenantId AND Id = @NorthstarAlertId)
    BEGIN
        INSERT dbo.Alerts
            (TenantId, Id, CampaignId, Fingerprint, MetricName, Score, Severity, Status,
             Observed, Baseline, Confidence, DataSufficiency, ReasonCodesJson, ScoringVersion,
             EvidenceWindowStart, EvidenceWindowEnd, DetectedAt)
        VALUES
            (@NorthstarTenantId, @NorthstarAlertId, @NorthstarCampaignId,
             '2222222222222222222222222222222222222222222222222222222222222222',
             N'cpc', 18.2500, N'low', N'acknowledged', 0.6000, 0.5000, 0.7500, N'sufficient',
             N'["degraded_vs_baseline"]', N'scoring-v1', '2026-02-01', '2026-02-01',
             CAST('2026-02-02T12:05:00+00:00' AS datetimeoffset(0)));
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.AuditEvents WHERE TenantId = @AcmeTenantId AND Id = @AcmeAuditId)
    BEGIN
        INSERT dbo.AuditEvents
            (TenantId, Id, OccurredAt, ActorSubjectId, Action, EntityType, EntityId, PayloadJson)
        VALUES
            (@AcmeTenantId, @AcmeAuditId, CAST('2026-02-03T12:06:00+00:00' AS datetimeoffset(0)),
             N'demo-user-acme', N'import.completed', N'ImportBatch', CONVERT(nvarchar(128), @ImportBatchId),
             N'{"source":"demo-seed","accepted_rows":3,"rejected_rows":1}');
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.AuditEvents WHERE TenantId = @NorthstarTenantId AND Id = @NorthstarAuditId)
    BEGIN
        INSERT dbo.AuditEvents
            (TenantId, Id, OccurredAt, ActorSubjectId, Action, EntityType, EntityId, PayloadJson)
        VALUES
            (@NorthstarTenantId, @NorthstarAuditId, CAST('2026-02-02T12:06:00+00:00' AS datetimeoffset(0)),
             N'demo-user-northstar', N'alert.acknowledged', N'Alert', CONVERT(nvarchar(128), @NorthstarAlertId),
             N'{"source":"demo-seed","status":"acknowledged"}');
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
