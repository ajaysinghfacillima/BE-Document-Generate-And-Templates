IF OBJECT_ID('RefreshToken', 'U') IS NULL
BEGIN
    CREATE TABLE RefreshToken (
        Token NVARCHAR(512) NOT NULL PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Role NVARCHAR(100) NOT NULL,
        IssuedAtUtc DATETIME2 NOT NULL,
        ExpiresAtUtc DATETIME2 NOT NULL,
        IsRevoked BIT NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RefreshToken_TenantId_ExpiresAtUtc' AND object_id = OBJECT_ID('RefreshToken'))
BEGIN
    CREATE INDEX IX_RefreshToken_TenantId_ExpiresAtUtc ON RefreshToken (TenantId, ExpiresAtUtc);
END;
