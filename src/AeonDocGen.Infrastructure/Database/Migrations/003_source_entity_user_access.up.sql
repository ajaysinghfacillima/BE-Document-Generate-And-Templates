IF OBJECT_ID('SourceEntityUserAccess', 'U') IS NULL
BEGIN
    CREATE TABLE SourceEntityUserAccess (
        SourceEntityUserAccessId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        ProjectId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        SourceEntityId UNIQUEIDENTIFIER NOT NULL,
        SourceEntityType NVARCHAR(100) NOT NULL,
        CreatedAt DATETIME2 NOT NULL
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_SourceEntityUserAccess_User_Project_Source'
      AND object_id = OBJECT_ID('SourceEntityUserAccess'))
BEGIN
    CREATE UNIQUE INDEX IX_SourceEntityUserAccess_User_Project_Source
        ON SourceEntityUserAccess (TenantId, ProjectId, UserId, SourceEntityId, SourceEntityType);
END;
