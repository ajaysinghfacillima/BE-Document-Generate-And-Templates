IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'project')
BEGIN
    EXEC('CREATE SCHEMA project');
END;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'ops')
BEGIN
    EXEC('CREATE SCHEMA ops');
END;

IF COL_LENGTH('content.Template', 'SupportedFormatsCsv') IS NULL
BEGIN
    ALTER TABLE content.Template
    ADD SupportedFormatsCsv NVARCHAR(200) NULL;
END;

IF OBJECT_ID('project.Project', 'U') IS NULL
BEGIN
    CREATE TABLE project.Project (
        ProjectId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(200) NOT NULL,
        IsActive BIT NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Project_TenantId_ProjectId' AND object_id = OBJECT_ID('project.Project'))
BEGIN
    CREATE INDEX IX_Project_TenantId_ProjectId ON project.Project (TenantId, ProjectId);
END;

IF OBJECT_ID('BrandingAsset', 'U') IS NULL
BEGIN
    CREATE TABLE BrandingAsset (
        BrandingAssetId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        LogoStorageUri NVARCHAR(1000) NULL,
        FontsStorageUri NVARCHAR(1000) NULL,
        ColorsJson NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NOT NULL,
        Version INT NOT NULL,
        Etag NVARCHAR(100) NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_BrandingAsset_TenantId' AND object_id = OBJECT_ID('BrandingAsset'))
BEGIN
    CREATE UNIQUE INDEX UX_BrandingAsset_TenantId ON BrandingAsset (TenantId);
END;

IF OBJECT_ID('DocumentArtifact', 'U') IS NULL
BEGIN
    CREATE TABLE DocumentArtifact (
        DocumentArtifactId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        ProjectId UNIQUEIDENTIFIER NOT NULL,
        DocumentType NVARCHAR(100) NOT NULL,
        Format NVARCHAR(50) NOT NULL,
        TemplateId UNIQUEIDENTIFIER NOT NULL,
        TemplateVersion NVARCHAR(50) NOT NULL,
        BrandingApplied BIT NOT NULL,
        WatermarkApplied BIT NOT NULL,
        FooterVersionText NVARCHAR(200) NOT NULL,
        StorageUri NVARCHAR(1000) NOT NULL,
        ChecksumSha256 NVARCHAR(128) NOT NULL,
        ReviewStatus NVARCHAR(50) NOT NULL,
        ReviewedByUserId UNIQUEIDENTIFIER NULL,
        ReviewedAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NOT NULL,
        Version INT NOT NULL,
        Etag NVARCHAR(100) NOT NULL,
        CONSTRAINT FK_DocumentArtifact_Project FOREIGN KEY (ProjectId) REFERENCES project.Project(ProjectId),
        CONSTRAINT FK_DocumentArtifact_Template FOREIGN KEY (TemplateId) REFERENCES content.Template(TemplateId)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DocumentArtifact_Project_Tenant' AND object_id = OBJECT_ID('DocumentArtifact'))
BEGIN
    CREATE INDEX IX_DocumentArtifact_Project_Tenant ON DocumentArtifact (ProjectId, TenantId, DocumentArtifactId);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DocumentArtifact_Project_Etag' AND object_id = OBJECT_ID('DocumentArtifact'))
BEGIN
    CREATE INDEX IX_DocumentArtifact_Project_Etag ON DocumentArtifact (ProjectId, Etag);
END;

IF OBJECT_ID('DocumentSource', 'U') IS NULL
BEGIN
    CREATE TABLE DocumentSource (
        DocumentSourceId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        DocumentArtifactId UNIQUEIDENTIFIER NOT NULL,
        SourceEntityType NVARCHAR(100) NOT NULL,
        SourceEntityId UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT FK_DocumentSource_DocumentArtifact FOREIGN KEY (DocumentArtifactId) REFERENCES DocumentArtifact(DocumentArtifactId)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DocumentSource_DocumentArtifactId' AND object_id = OBJECT_ID('DocumentSource'))
BEGIN
    CREATE INDEX IX_DocumentSource_DocumentArtifactId ON DocumentSource (DocumentArtifactId);
END;

IF OBJECT_ID('DocumentReviewEvent', 'U') IS NULL
BEGIN
    CREATE TABLE DocumentReviewEvent (
        DocumentReviewEventId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        DocumentArtifactId UNIQUEIDENTIFIER NOT NULL,
        Action NVARCHAR(50) NOT NULL,
        ActorUserId UNIQUEIDENTIFIER NOT NULL,
        Comments NVARCHAR(2000) NULL,
        CreatedAt DATETIME2 NOT NULL,
        CONSTRAINT FK_DocumentReviewEvent_DocumentArtifact FOREIGN KEY (DocumentArtifactId) REFERENCES DocumentArtifact(DocumentArtifactId)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DocumentReviewEvent_DocumentArtifactId_CreatedAt' AND object_id = OBJECT_ID('DocumentReviewEvent'))
BEGIN
    CREATE INDEX IX_DocumentReviewEvent_DocumentArtifactId_CreatedAt ON DocumentReviewEvent (DocumentArtifactId, CreatedAt DESC);
END;

IF OBJECT_ID('AuditLog', 'U') IS NULL
BEGIN
    CREATE TABLE AuditLog (
        AuditLogId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NOT NULL,
        Version INT NOT NULL,
        ActorUserId UNIQUEIDENTIFIER NULL,
        ActorType NVARCHAR(50) NOT NULL,
        Action NVARCHAR(200) NOT NULL,
        ResourceType NVARCHAR(100) NOT NULL,
        ResourceId UNIQUEIDENTIFIER NOT NULL,
        ScopeType NVARCHAR(100) NOT NULL,
        ScopeId UNIQUEIDENTIFIER NOT NULL,
        Outcome NVARCHAR(50) NOT NULL,
        IpAddress NVARCHAR(64) NULL,
        UserAgent NVARCHAR(512) NULL,
        CorrelationId NVARCHAR(128) NOT NULL,
        BeforeJson NVARCHAR(MAX) NULL,
        AfterJson NVARCHAR(MAX) NULL,
        Reason NVARCHAR(2000) NULL,
        ImmutableHash NVARCHAR(128) NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLog_TenantId_CreatedAt' AND object_id = OBJECT_ID('AuditLog'))
BEGIN
    CREATE INDEX IX_AuditLog_TenantId_CreatedAt ON AuditLog (TenantId, CreatedAt DESC);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLog_CorrelationId' AND object_id = OBJECT_ID('AuditLog'))
BEGIN
    CREATE INDEX IX_AuditLog_CorrelationId ON AuditLog (CorrelationId);
END;

IF OBJECT_ID('IdempotencyRecord', 'U') IS NULL
BEGIN
    CREATE TABLE IdempotencyRecord (
        IdempotencyKey NVARCHAR(256) NOT NULL,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        RequestHash NVARCHAR(128) NOT NULL,
        ResponseJson NVARCHAR(MAX) NOT NULL,
        StatusCode INT NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        CONSTRAINT PK_IdempotencyRecord PRIMARY KEY CLUSTERED (IdempotencyKey, TenantId)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_IdempotencyRecord_CreatedAt' AND object_id = OBJECT_ID('IdempotencyRecord'))
BEGIN
    CREATE INDEX IX_IdempotencyRecord_CreatedAt ON IdempotencyRecord (CreatedAt);
END;

IF OBJECT_ID('JobQueue', 'U') IS NULL
BEGIN
    CREATE TABLE JobQueue (
        JobQueueId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        JobType NVARCHAR(100) NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        Priority INT NOT NULL,
        PayloadJson NVARCHAR(MAX) NOT NULL,
        ResultJson NVARCHAR(MAX) NULL,
        LastError NVARCHAR(2000) NULL,
        AvailableAt DATETIME2 NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NOT NULL
    );
END;

IF COL_LENGTH('JobQueue', 'ResultJson') IS NULL
BEGIN
    ALTER TABLE JobQueue ADD ResultJson NVARCHAR(MAX) NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JobQueue_Status_AvailableAt' AND object_id = OBJECT_ID('JobQueue'))
BEGIN
    CREATE INDEX IX_JobQueue_Status_AvailableAt ON JobQueue (Status, AvailableAt);
END;

IF OBJECT_ID('Artifact', 'U') IS NULL
BEGIN
    CREATE TABLE Artifact (
        ArtifactId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        ProjectId UNIQUEIDENTIFIER NOT NULL
    );
END;

IF OBJECT_ID('SimulationJob', 'U') IS NULL
BEGIN
    CREATE TABLE SimulationJob (
        SimulationJobId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        ProjectId UNIQUEIDENTIFIER NOT NULL
    );
END;

IF OBJECT_ID('Scorecard', 'U') IS NULL
BEGIN
    CREATE TABLE Scorecard (
        ScorecardId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        ProjectId UNIQUEIDENTIFIER NOT NULL
    );
END;

IF OBJECT_ID('PreAssessmentRun', 'U') IS NULL
BEGIN
    CREATE TABLE PreAssessmentRun (
        PreAssessmentRunId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        ProjectId UNIQUEIDENTIFIER NOT NULL
    );
END;

IF OBJECT_ID('AuditorQuery', 'U') IS NULL
BEGIN
    CREATE TABLE AuditorQuery (
        AuditorQueryId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        ProjectId UNIQUEIDENTIFIER NOT NULL
    );
END;

IF OBJECT_ID('Recommendation', 'U') IS NULL
BEGIN
    CREATE TABLE Recommendation (
        RecommendationId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        ProjectId UNIQUEIDENTIFIER NOT NULL
    );
END;

IF COL_LENGTH('RefreshToken', 'TokenHash') IS NULL
BEGIN
    IF COL_LENGTH('RefreshToken', 'Token') IS NOT NULL
    BEGIN
        EXEC sp_rename 'RefreshToken.Token', 'TokenHash', 'COLUMN';
    END
    ELSE
    BEGIN
        ALTER TABLE RefreshToken ADD TokenHash NVARCHAR(128) NULL;
    END
END;

IF OBJECT_ID('RefreshToken', 'U') IS NULL
BEGIN
    CREATE TABLE RefreshToken (
        TokenHash NVARCHAR(128) NOT NULL PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Role NVARCHAR(100) NOT NULL,
        IssuedAtUtc DATETIME2 NOT NULL,
        ExpiresAtUtc DATETIME2 NOT NULL,
        IsRevoked BIT NOT NULL
    );
END;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('RefreshToken') AND name = 'TokenHash')
AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('RefreshToken') AND name = 'Token')
BEGIN
    UPDATE RefreshToken
    SET TokenHash = Token
    WHERE TokenHash IS NULL;
END;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('RefreshToken') AND name = 'TokenHash')
BEGIN
    ALTER TABLE RefreshToken ALTER COLUMN TokenHash NVARCHAR(128) NOT NULL;
END;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('RefreshToken') AND name = 'Token')
BEGIN
    ALTER TABLE RefreshToken DROP COLUMN Token;
END;

IF OBJECT_ID('PK_RefreshToken', 'PK') IS NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE [type] = 'PK' AND parent_object_id = OBJECT_ID('RefreshToken'))
    BEGIN
        DECLARE @pkName NVARCHAR(200);
        SELECT TOP 1 @pkName = [name]
        FROM sys.key_constraints
        WHERE [type] = 'PK' AND parent_object_id = OBJECT_ID('RefreshToken');
        EXEC('ALTER TABLE RefreshToken DROP CONSTRAINT ' + QUOTENAME(@pkName));
    END;
    ALTER TABLE RefreshToken ADD CONSTRAINT PK_RefreshToken PRIMARY KEY CLUSTERED (TokenHash);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RefreshToken_TenantId_ExpiresAtUtc' AND object_id = OBJECT_ID('RefreshToken'))
BEGIN
    CREATE INDEX IX_RefreshToken_TenantId_ExpiresAtUtc ON RefreshToken (TenantId, ExpiresAtUtc);
END;
