SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

CREATE TABLE dbo.Tenants
(
    Id nvarchar(64) NOT NULL,
    Name nvarchar(200) NOT NULL,
    CreatedAt datetimeoffset(0) NOT NULL,
    CONSTRAINT PK_Tenants PRIMARY KEY (Id)
);

CREATE TABLE dbo.Memberships
(
    TenantId nvarchar(64) NOT NULL,
    SubjectId nvarchar(128) NOT NULL,
    Role nvarchar(64) NOT NULL,
    CONSTRAINT PK_Memberships PRIMARY KEY (TenantId, SubjectId, Role),
    CONSTRAINT FK_Memberships_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (Id)
);

CREATE TABLE dbo.Campaigns
(
    TenantId nvarchar(64) NOT NULL,
    Id uniqueidentifier NOT NULL,
    Name nvarchar(200) NOT NULL,
    Status nvarchar(32) NOT NULL,
    Currency char(3) NOT NULL,
    Budget decimal(19, 4) NOT NULL,
    StartsOn date NOT NULL,
    EndsOn date NOT NULL,
    CreatedAt datetimeoffset(0) NOT NULL,
    CONSTRAINT PK_Campaigns PRIMARY KEY (TenantId, Id),
    CONSTRAINT FK_Campaigns_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (Id),
    CONSTRAINT CK_Campaigns_Budget CHECK (Budget >= 0),
    CONSTRAINT CK_Campaigns_DateRange CHECK (EndsOn >= StartsOn),
    CONSTRAINT CK_Campaigns_Currency CHECK (Currency COLLATE Latin1_General_100_BIN2 LIKE '[A-Z][A-Z][A-Z]')
);

CREATE TABLE dbo.DailyMetrics
(
    TenantId nvarchar(64) NOT NULL,
    CampaignId uniqueidentifier NOT NULL,
    MetricDate date NOT NULL,
    Spend decimal(19, 4) NOT NULL,
    Revenue decimal(19, 4) NOT NULL,
    Impressions bigint NOT NULL,
    Clicks bigint NOT NULL,
    Conversions bigint NOT NULL,
    CONSTRAINT PK_DailyMetrics PRIMARY KEY (TenantId, CampaignId, MetricDate),
    CONSTRAINT FK_DailyMetrics_Campaigns FOREIGN KEY (TenantId, CampaignId) REFERENCES dbo.Campaigns (TenantId, Id),
    CONSTRAINT CK_DailyMetrics_NonNegative CHECK (Spend >= 0 AND Revenue >= 0 AND Impressions >= 0 AND Clicks >= 0 AND Conversions >= 0),
    CONSTRAINT CK_DailyMetrics_ClicksWithinImpressions CHECK (Clicks <= Impressions),
    CONSTRAINT CK_DailyMetrics_ConversionsWithinClicks CHECK (Conversions <= Clicks)
);

CREATE TABLE dbo.ImportBatches
(
    TenantId nvarchar(64) NOT NULL,
    Id uniqueidentifier NOT NULL,
    UploadedBySubjectId nvarchar(128) NOT NULL,
    UploadedAt datetimeoffset(0) NOT NULL,
    FileName nvarchar(260) NOT NULL,
    AcceptedRows int NOT NULL,
    RejectedRows int NOT NULL,
    Status nvarchar(32) NOT NULL,
    CONSTRAINT PK_ImportBatches PRIMARY KEY (TenantId, Id),
    CONSTRAINT FK_ImportBatches_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (Id),
    CONSTRAINT CK_ImportBatches_RowCounts CHECK (AcceptedRows >= 0 AND RejectedRows >= 0)
);

CREATE TABLE dbo.ImportRowErrors
(
    TenantId nvarchar(64) NOT NULL,
    ImportBatchId uniqueidentifier NOT NULL,
    RowNumber int NOT NULL,
    ErrorCode nvarchar(64) NOT NULL,
    Message nvarchar(500) NOT NULL,
    CONSTRAINT PK_ImportRowErrors PRIMARY KEY (TenantId, ImportBatchId, RowNumber),
    CONSTRAINT FK_ImportRowErrors_ImportBatches FOREIGN KEY (TenantId, ImportBatchId) REFERENCES dbo.ImportBatches (TenantId, Id) ON DELETE CASCADE,
    CONSTRAINT CK_ImportRowErrors_RowNumber CHECK (RowNumber > 0)
);

CREATE TABLE dbo.Alerts
(
    TenantId nvarchar(64) NOT NULL,
    Id uniqueidentifier NOT NULL,
    CampaignId uniqueidentifier NULL,
    Fingerprint char(64) NOT NULL,
    MetricName nvarchar(64) NOT NULL,
    Score decimal(9, 4) NOT NULL,
    Severity nvarchar(16) NOT NULL,
    Status nvarchar(16) NOT NULL,
    Observed decimal(19, 4) NOT NULL,
    Baseline decimal(19, 4) NOT NULL,
    Confidence decimal(9, 4) NOT NULL,
    DataSufficiency nvarchar(32) NOT NULL,
    ReasonCodesJson nvarchar(2000) NOT NULL,
    ScoringVersion nvarchar(32) NOT NULL,
    EvidenceWindowStart date NOT NULL,
    EvidenceWindowEnd date NOT NULL,
    DetectedAt datetimeoffset(0) NOT NULL,
    CONSTRAINT PK_Alerts PRIMARY KEY (TenantId, Id),
    CONSTRAINT FK_Alerts_Campaigns FOREIGN KEY (TenantId, CampaignId) REFERENCES dbo.Campaigns (TenantId, Id),
    CONSTRAINT UQ_Alerts_TenantFingerprint UNIQUE (TenantId, Fingerprint),
    CONSTRAINT CK_Alerts_Score CHECK (Score >= 0 AND Score <= 100),
    CONSTRAINT CK_Alerts_Confidence CHECK (Confidence >= 0 AND Confidence <= 1),
    CONSTRAINT CK_Alerts_ObservedBaseline CHECK (Observed >= 0 AND Baseline >= 0),
    CONSTRAINT CK_Alerts_DateRange CHECK (EvidenceWindowEnd >= EvidenceWindowStart),
    CONSTRAINT CK_Alerts_ReasonCodesJson CHECK (ISJSON(ReasonCodesJson) = 1)
);

CREATE TABLE dbo.AuditEvents
(
    TenantId nvarchar(64) NOT NULL,
    Id uniqueidentifier NOT NULL,
    OccurredAt datetimeoffset(0) NOT NULL,
    ActorSubjectId nvarchar(128) NOT NULL,
    Action nvarchar(64) NOT NULL,
    EntityType nvarchar(64) NOT NULL,
    EntityId nvarchar(128) NOT NULL,
    PayloadJson nvarchar(max) NOT NULL,
    CONSTRAINT PK_AuditEvents PRIMARY KEY (TenantId, Id),
    CONSTRAINT FK_AuditEvents_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (Id),
    CONSTRAINT CK_AuditEvents_PayloadJson CHECK (ISJSON(PayloadJson) = 1)
);

CREATE INDEX IX_Campaigns_Tenant_Status ON dbo.Campaigns (TenantId, Status);
CREATE INDEX IX_DailyMetrics_Tenant_Campaign_Date ON dbo.DailyMetrics (TenantId, CampaignId, MetricDate DESC);
CREATE INDEX IX_ImportBatches_Tenant_UploadedAt ON dbo.ImportBatches (TenantId, UploadedAt DESC);
CREATE INDEX IX_Alerts_Tenant_Status_DetectedAt ON dbo.Alerts (TenantId, Status, DetectedAt DESC);
CREATE INDEX IX_AuditEvents_Tenant_OccurredAt ON dbo.AuditEvents (TenantId, OccurredAt DESC);

EXEC(N'CREATE TRIGGER dbo.TR_AuditEvents_AppendOnly
ON dbo.AuditEvents
AFTER UPDATE, DELETE
AS
BEGIN
    IF ROWCOUNT_BIG() = 0
        RETURN;
    SET NOCOUNT ON;
    THROW 51001, ''Audit events are append-only.'', 1;
END;');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
