SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    CREATE TABLE dbo.ImportPendingMetrics
    (
        TenantId nvarchar(64) NOT NULL,
        ImportBatchId uniqueidentifier NOT NULL,
        RowNumber int NOT NULL,
        CampaignId uniqueidentifier NOT NULL,
        MetricDate date NOT NULL,
        Spend decimal(19, 4) NOT NULL,
        Revenue decimal(19, 4) NOT NULL,
        Impressions bigint NOT NULL,
        Clicks bigint NOT NULL,
        Conversions bigint NOT NULL,
        CONSTRAINT PK_ImportPendingMetrics PRIMARY KEY (TenantId, ImportBatchId, RowNumber),
        CONSTRAINT FK_ImportPendingMetrics_ImportBatches FOREIGN KEY (TenantId, ImportBatchId)
            REFERENCES dbo.ImportBatches (TenantId, Id) ON DELETE CASCADE,
        CONSTRAINT FK_ImportPendingMetrics_Campaigns FOREIGN KEY (TenantId, CampaignId)
            REFERENCES dbo.Campaigns (TenantId, Id),
        CONSTRAINT CK_ImportPendingMetrics_NonNegative CHECK (
            Spend >= 0 AND Revenue >= 0 AND Impressions >= 0 AND Clicks >= 0 AND Conversions >= 0),
        CONSTRAINT CK_ImportPendingMetrics_ClicksWithinImpressions CHECK (Clicks <= Impressions),
        CONSTRAINT CK_ImportPendingMetrics_ConversionsWithinClicks CHECK (Conversions <= Clicks),
        CONSTRAINT CK_ImportPendingMetrics_RowNumber CHECK (RowNumber > 0)
    );

    CREATE INDEX IX_ImportPendingMetrics_Tenant_Batch
        ON dbo.ImportPendingMetrics (TenantId, ImportBatchId, RowNumber);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
