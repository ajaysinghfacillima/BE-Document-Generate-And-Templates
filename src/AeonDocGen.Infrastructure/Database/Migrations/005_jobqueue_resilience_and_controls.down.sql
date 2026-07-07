IF OBJECT_ID('JobFailureAlert', 'U') IS NOT NULL
BEGIN
    DROP TABLE JobFailureAlert;
END;

IF OBJECT_ID('JobWorkerControl', 'U') IS NOT NULL
BEGIN
    DROP TABLE JobWorkerControl;
END;

IF OBJECT_ID('JobQueueAttemptHistory', 'U') IS NOT NULL
BEGIN
    DROP TABLE JobQueueAttemptHistory;
END;

IF COL_LENGTH('JobQueue', 'CompletedAt') IS NOT NULL
BEGIN
    ALTER TABLE JobQueue DROP COLUMN CompletedAt;
END;

IF COL_LENGTH('JobQueue', 'StartedAt') IS NOT NULL
BEGIN
    ALTER TABLE JobQueue DROP COLUMN StartedAt;
END;

IF COL_LENGTH('JobQueue', 'JobTimeoutSeconds') IS NOT NULL
BEGIN
    ALTER TABLE JobQueue DROP COLUMN JobTimeoutSeconds;
END;

IF COL_LENGTH('JobQueue', 'MaxAttempts') IS NOT NULL
BEGIN
    IF OBJECT_ID('DF_JobQueue_MaxAttempts', 'D') IS NOT NULL
    BEGIN
        ALTER TABLE JobQueue DROP CONSTRAINT DF_JobQueue_MaxAttempts;
    END;
    ALTER TABLE JobQueue DROP COLUMN MaxAttempts;
END;

IF COL_LENGTH('JobQueue', 'AttemptCount') IS NOT NULL
BEGIN
    IF OBJECT_ID('DF_JobQueue_AttemptCount', 'D') IS NOT NULL
    BEGIN
        ALTER TABLE JobQueue DROP CONSTRAINT DF_JobQueue_AttemptCount;
    END;
    ALTER TABLE JobQueue DROP COLUMN AttemptCount;
END;
