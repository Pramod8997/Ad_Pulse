SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    ALTER TABLE dbo.Alerts ADD
        IncidentStatus nvarchar(24) NULL,
        OwnerSubjectId nvarchar(128) NULL,
        AssignedAt datetimeoffset(0) NULL,
        ResolutionSummary nvarchar(2000) NULL,
        FalsePositiveReason nvarchar(2000) NULL,
        ResolvedAt datetimeoffset(0) NULL,
        ResolvedBySubjectId nvarchar(128) NULL;

    CREATE INDEX IX_Alerts_Tenant_Owner_IncidentStatus_DetectedAt
        ON dbo.Alerts (TenantId, OwnerSubjectId, IncidentStatus, DetectedAt DESC);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
