SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    CREATE TABLE dbo.TenantSettings
    (
        TenantId nvarchar(64) NOT NULL,
        ReportingTimeZone nvarchar(128) NOT NULL,
        DefaultCurrency char(3) NULL,
        UpdatedAtUtc datetimeoffset(0) NOT NULL,
        CONSTRAINT PK_TenantSettings PRIMARY KEY (TenantId),
        CONSTRAINT FK_TenantSettings_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (Id),
        CONSTRAINT CK_TenantSettings_Currency CHECK (
            DefaultCurrency IS NULL
            OR DefaultCurrency COLLATE Latin1_General_100_BIN2 LIKE '[A-Z][A-Z][A-Z]')
    );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
