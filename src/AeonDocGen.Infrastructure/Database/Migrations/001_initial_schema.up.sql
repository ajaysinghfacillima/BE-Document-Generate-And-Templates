IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'content')
BEGIN
    EXEC('CREATE SCHEMA content');
END;

IF OBJECT_ID('content.Template', 'U') IS NULL
BEGIN
    CREATE TABLE content.Template (
        TemplateId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(200) NOT NULL,
        DocumentType NVARCHAR(100) NOT NULL,
        CurrentVersion NVARCHAR(50) NOT NULL,
        IsActive BIT NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NOT NULL,
        Version INT NOT NULL,
        Etag NVARCHAR(100) NOT NULL
    );
END;

IF OBJECT_ID('content.TemplateVersion', 'U') IS NULL
BEGIN
    CREATE TABLE content.TemplateVersion (
        TemplateVersionId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TemplateId UNIQUEIDENTIFIER NOT NULL,
        TemplateVersion NVARCHAR(50) NOT NULL,
        IsPublished BIT NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        CONSTRAINT FK_TemplateVersion_Template FOREIGN KEY (TemplateId) REFERENCES content.Template(TemplateId)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Template_TenantId_Name' AND object_id = OBJECT_ID('content.Template'))
BEGIN
    CREATE INDEX IX_Template_TenantId_Name ON content.Template (TenantId, Name);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TemplateVersion_TemplateId_CreatedAt' AND object_id = OBJECT_ID('content.TemplateVersion'))
BEGIN
    CREATE INDEX IX_TemplateVersion_TemplateId_CreatedAt ON content.TemplateVersion (TemplateId, CreatedAt DESC);
END;
