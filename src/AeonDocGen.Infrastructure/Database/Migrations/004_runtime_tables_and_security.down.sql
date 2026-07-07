IF OBJECT_ID('DocumentReviewEvent', 'U') IS NOT NULL
BEGIN
    DROP TABLE DocumentReviewEvent;
END;

IF OBJECT_ID('DocumentSource', 'U') IS NOT NULL
BEGIN
    DROP TABLE DocumentSource;
END;

IF OBJECT_ID('DocumentArtifact', 'U') IS NOT NULL
BEGIN
    DROP TABLE DocumentArtifact;
END;

IF OBJECT_ID('BrandingAsset', 'U') IS NOT NULL
BEGIN
    DROP TABLE BrandingAsset;
END;

IF OBJECT_ID('AuditLog', 'U') IS NOT NULL
BEGIN
    DROP TABLE AuditLog;
END;

IF OBJECT_ID('IdempotencyRecord', 'U') IS NOT NULL
BEGIN
    DROP TABLE IdempotencyRecord;
END;

IF OBJECT_ID('JobQueue', 'U') IS NOT NULL
BEGIN
    DROP TABLE JobQueue;
END;

IF OBJECT_ID('Recommendation', 'U') IS NOT NULL
BEGIN
    DROP TABLE Recommendation;
END;

IF OBJECT_ID('AuditorQuery', 'U') IS NOT NULL
BEGIN
    DROP TABLE AuditorQuery;
END;

IF OBJECT_ID('PreAssessmentRun', 'U') IS NOT NULL
BEGIN
    DROP TABLE PreAssessmentRun;
END;

IF OBJECT_ID('Scorecard', 'U') IS NOT NULL
BEGIN
    DROP TABLE Scorecard;
END;

IF OBJECT_ID('SimulationJob', 'U') IS NOT NULL
BEGIN
    DROP TABLE SimulationJob;
END;

IF OBJECT_ID('Artifact', 'U') IS NOT NULL
BEGIN
    DROP TABLE Artifact;
END;

IF OBJECT_ID('project.Project', 'U') IS NOT NULL
BEGIN
    DROP TABLE project.Project;
END;

IF COL_LENGTH('content.Template', 'SupportedFormatsCsv') IS NOT NULL
BEGIN
    ALTER TABLE content.Template DROP COLUMN SupportedFormatsCsv;
END;
