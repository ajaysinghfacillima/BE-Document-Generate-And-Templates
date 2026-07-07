IF COL_LENGTH('JobQueue', 'AttemptCount') IS NULL
BEGIN
    ALTER TABLE JobQueue
    ADD AttemptCount INT NOT NULL CONSTRAINT DF_JobQueue_AttemptCount DEFAULT (0);
END;

IF COL_LENGTH('JobQueue', 'MaxAttempts') IS NULL
BEGIN
    ALTER TABLE JobQueue
    ADD MaxAttempts INT NOT NULL CONSTRAINT DF_JobQueue_MaxAttempts DEFAULT (3);
END;

IF COL_LENGTH('JobQueue', 'JobTimeoutSeconds') IS NULL
BEGIN
    ALTER TABLE JobQueue
    ADD JobTimeoutSeconds INT NULL;
END;

IF COL_LENGTH('JobQueue', 'StartedAt') IS NULL
BEGIN
    ALTER TABLE JobQueue
    ADD StartedAt DATETIME2 NULL;
END;

IF COL_LENGTH('JobQueue', 'CompletedAt') IS NULL
BEGIN
    ALTER TABLE JobQueue
    ADD CompletedAt DATETIME2 NULL;
END;

IF OBJECT_ID('JobQueueAttemptHistory', 'U') IS NULL
BEGIN
    CREATE TABLE JobQueueAttemptHistory (
        JobQueueAttemptHistoryId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        JobQueueId UNIQUEIDENTIFIER NOT NULL,
        AttemptNumber INT NOT NULL,
        StartedAt DATETIME2 NOT NULL,
        EndedAt DATETIME2 NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        Error NVARCHAR(2000) NULL,
        DurationMilliseconds BIGINT NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        CONSTRAINT FK_JobQueueAttemptHistory_JobQueue FOREIGN KEY (JobQueueId) REFERENCES JobQueue(JobQueueId)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JobQueueAttemptHistory_JobQueueId_AttemptNumber' AND object_id = OBJECT_ID('JobQueueAttemptHistory'))
BEGIN
    CREATE INDEX IX_JobQueueAttemptHistory_JobQueueId_AttemptNumber ON JobQueueAttemptHistory (JobQueueId, AttemptNumber DESC);
END;

IF OBJECT_ID('JobWorkerControl', 'U') IS NULL
BEGIN
    CREATE TABLE JobWorkerControl (
        JobType NVARCHAR(100) NOT NULL PRIMARY KEY,
        IsEnabled BIT NOT NULL,
        IsPaused BIT NOT NULL,
        ManualTriggerRequested BIT NOT NULL,
        UpdatedAt DATETIME2 NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM JobWorkerControl WHERE JobType = 'document.render-store')
BEGIN
    INSERT INTO JobWorkerControl (JobType, IsEnabled, IsPaused, ManualTriggerRequested, UpdatedAt)
    VALUES ('document.render-store', 1, 0, 0, SYSUTCDATETIME());
END;

IF OBJECT_ID('JobFailureAlert', 'U') IS NULL
BEGIN
    CREATE TABLE JobFailureAlert (
        JobFailureAlertId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        JobType NVARCHAR(100) NOT NULL,
        WindowStart DATETIME2 NOT NULL,
        FailureCount INT NOT NULL,
        EscalatedAt DATETIME2 NOT NULL,
        Message NVARCHAR(2000) NOT NULL,
        CreatedAt DATETIME2 NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JobFailureAlert_JobType_WindowStart' AND object_id = OBJECT_ID('JobFailureAlert'))
BEGIN
    CREATE INDEX IX_JobFailureAlert_JobType_WindowStart ON JobFailureAlert (JobType, WindowStart DESC);
END;
