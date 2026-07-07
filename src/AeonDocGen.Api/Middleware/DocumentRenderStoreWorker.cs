using System.Data;
using System.Data.Common;
using System.Text.Json;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Services;
using Microsoft.Extensions.Options;

namespace AeonDocGen.Api.Middleware;

public sealed class DocumentRenderStoreWorker : BackgroundService
{
    private const string JobType = "document.render-store";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentRenderStoreWorker> _logger;

    public DocumentRenderStoreWorker(IServiceScopeFactory scopeFactory, ILogger<DocumentRenderStoreWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
                var storageClient = scope.ServiceProvider.GetRequiredService<IDocumentStorageClient>();
                var settings = scope.ServiceProvider.GetRequiredService<IOptions<DocumentGenerationSettings>>().Value;

                var control = await GetWorkerControlAsync(connectionFactory, stoppingToken);
                if (!control.IsEnabled || (control.IsPaused && !control.ManualTriggerRequested))
                {
                    await Task.Delay(settings.RenderQueuePollIntervalMilliseconds, stoppingToken);
                    continue;
                }

                var processedCount = await ProcessQueuedJobsBatchAsync(connectionFactory, storageClient, settings, stoppingToken);
                if (processedCount > 0 && control.ManualTriggerRequested)
                {
                    await ResetManualTriggerAsync(connectionFactory, stoppingToken);
                }

                if (processedCount == 0)
                {
                    await Task.Delay(settings.RenderQueuePollIntervalMilliseconds, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Durable document render worker loop failed. RequestId={RequestId}", "background-worker");
                await Task.Delay(500, stoppingToken);
            }
        }
    }

    private async Task<int> ProcessQueuedJobsBatchAsync(
        IDbConnectionFactory connectionFactory,
        IDocumentStorageClient storageClient,
        DocumentGenerationSettings settings,
        CancellationToken cancellationToken)
    {
        var batchSize = Math.Max(1, settings.RenderQueueBatchSize);
        var processedCount = 0;
        while (processedCount < batchSize)
        {
            var processed = await ProcessSingleQueuedJobAsync(connectionFactory, storageClient, settings, cancellationToken);
            if (!processed)
            {
                break;
            }

            processedCount++;
        }

        return processedCount;
    }

    private async Task<bool> ProcessSingleQueuedJobAsync(
        IDbConnectionFactory connectionFactory,
        IDocumentStorageClient storageClient,
        DocumentGenerationSettings settings,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        await OpenConnectionAsync(connection, cancellationToken);

        var claim = await TryClaimNextJobAsync(connection, settings, cancellationToken);
        if (claim == null)
        {
            return false;
        }

        var startedAt = DateTime.UtcNow;
        var (jobId, payloadJson, attemptCount, maxAttempts, timeoutSeconds) = claim.Value;

        var payload = JsonSerializer.Deserialize<DocumentRenderStoreJobPayload>(payloadJson);
        if (payload == null)
        {
            await HandleFailedAttemptAsync(
                connection,
                settings,
                jobId,
                JobType,
                attemptCount,
                maxAttempts,
                startedAt,
                DateTime.UtcNow,
                "Failed to deserialize render job payload.",
                cancellationToken);
            return true;
        }

        _logger.LogInformation(
            "DocumentRenderStoreWorker job started. JobQueueId={JobQueueId}, StartTimeUtc={StartTimeUtc}, Attempt={Attempt}, MaxAttempts={MaxAttempts}, TenantId={TenantId}, ProjectId={ProjectId}, CorrelationId={CorrelationId}",
            jobId,
            startedAt,
            attemptCount + 1,
            maxAttempts,
            payload.TenantId,
            payload.ProjectId,
            payload.CorrelationId);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            var renderedContent = DocumentRenderRuntime.RenderDocument(payload);
            var checksumSha256 = DocumentRenderRuntime.ComputeChecksumSha256(renderedContent);
            var watermarkApplied = !string.IsNullOrWhiteSpace(payload.WatermarkText);
            var footerVersionText = DocumentRenderRuntime.BuildFooterVersionText(
                payload.FooterVersionPrefix,
                payload.TemplateVersion,
                payload.Format);

            var storagePath = $"{payload.TenantId}/projects/{payload.ProjectId}/generated/{payload.DocumentId}/{payload.DocumentType}.{payload.Format}";
            var storageUri = await storageClient.StoreFileAsync(storagePath, renderedContent, timeoutCts.Token);

            var resultJson = JsonSerializer.Serialize(new DocumentRenderStoreJobResult
            {
                StorageUri = storageUri,
                ChecksumSha256 = checksumSha256,
                WatermarkApplied = watermarkApplied,
                FooterVersionText = footerVersionText
            });

            var endedAt = DateTime.UtcNow;
            await MarkJobCompletedAsync(connection, jobId, resultJson, attemptCount + 1, endedAt, cancellationToken);
            await InsertAttemptHistoryAsync(connection, jobId, attemptCount + 1, startedAt, endedAt, "completed", null, cancellationToken);

            _logger.LogInformation(
                "DocumentRenderStoreWorker job completed. JobQueueId={JobQueueId}, EndTimeUtc={EndTimeUtc}, DurationMs={DurationMs}, Outcome={Outcome}, TenantId={TenantId}, ProjectId={ProjectId}, CorrelationId={CorrelationId}",
                jobId,
                endedAt,
                (long)(endedAt - startedAt).TotalMilliseconds,
                "completed",
                payload.TenantId,
                payload.ProjectId,
                payload.CorrelationId);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            var endedAt = DateTime.UtcNow;
            await HandleFailedAttemptAsync(
                connection,
                settings,
                jobId,
                JobType,
                attemptCount,
                maxAttempts,
                startedAt,
                endedAt,
                $"Job timed out after {timeoutSeconds} seconds.",
                cancellationToken);
            _logger.LogError(ex,
                "DocumentRenderStoreWorker job timed out. JobQueueId={JobQueueId}, EndTimeUtc={EndTimeUtc}, DurationMs={DurationMs}, Outcome={Outcome}",
                jobId,
                endedAt,
                (long)(endedAt - startedAt).TotalMilliseconds,
                "timed_out");
        }
        catch (Exception ex)
        {
            var endedAt = DateTime.UtcNow;
            await HandleFailedAttemptAsync(
                connection,
                settings,
                jobId,
                JobType,
                attemptCount,
                maxAttempts,
                startedAt,
                endedAt,
                ex.Message,
                cancellationToken);
            _logger.LogError(ex,
                "DocumentRenderStoreWorker job failed. JobQueueId={JobQueueId}, EndTimeUtc={EndTimeUtc}, DurationMs={DurationMs}, Outcome={Outcome}",
                jobId,
                endedAt,
                (long)(endedAt - startedAt).TotalMilliseconds,
                "failed");
        }

        return true;
    }

    private static async Task<(Guid JobId, string PayloadJson, int AttemptCount, int MaxAttempts, int TimeoutSeconds)?> TryClaimNextJobAsync(
        IDbConnection connection,
        DocumentGenerationSettings settings,
        CancellationToken cancellationToken)
    {
        using var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = @"
        SELECT TOP 1 JobQueueId, PayloadJson, AttemptCount, MaxAttempts, ISNULL(JobTimeoutSeconds, @DefaultTimeoutSeconds) AS TimeoutSeconds
        FROM JobQueue
        WHERE JobType = @JobType
          AND Status = @Status
          AND AvailableAt <= @Now
        ORDER BY Priority DESC, CreatedAt ASC";
        AddParameter(selectCmd, "@JobType", JobType);
        AddParameter(selectCmd, "@Status", "queued");
        AddParameter(selectCmd, "@Now", DateTime.UtcNow);
        AddParameter(selectCmd, "@DefaultTimeoutSeconds", Math.Max(1, settings.DefaultJobTimeoutSeconds));

        using var reader = await ExecuteReaderAsync(selectCmd, cancellationToken);
        if (!await ReadAsync(reader, cancellationToken))
        {
            return null;
        }

        var jobId = (Guid)reader["JobQueueId"];
        var payloadJson = reader["PayloadJson"]?.ToString() ?? string.Empty;
        var attemptCount = Convert.ToInt32(reader["AttemptCount"]);
        var maxAttempts = Math.Max(1, Convert.ToInt32(reader["MaxAttempts"]));
        var timeoutSeconds = Math.Max(1, Convert.ToInt32(reader["TimeoutSeconds"]));

        using var claimCmd = connection.CreateCommand();
        claimCmd.CommandText = @"
        UPDATE JobQueue
        SET Status = @NextStatus,
            StartedAt = @StartedAt,
            UpdatedAt = @UpdatedAt
        WHERE JobQueueId = @JobQueueId
          AND Status = @CurrentStatus";
        AddParameter(claimCmd, "@NextStatus", "processing");
        AddParameter(claimCmd, "@StartedAt", DateTime.UtcNow);
        AddParameter(claimCmd, "@UpdatedAt", DateTime.UtcNow);
        AddParameter(claimCmd, "@JobQueueId", jobId);
        AddParameter(claimCmd, "@CurrentStatus", "queued");

        var claimed = await ExecuteNonQueryAsync(claimCmd, cancellationToken);
        return claimed > 0 ? (jobId, payloadJson, attemptCount, maxAttempts, timeoutSeconds) : null;
    }

    private static async Task MarkJobCompletedAsync(
        IDbConnection connection,
        Guid jobId,
        string resultJson,
        int attemptCount,
        DateTime endedAt,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
        UPDATE JobQueue
        SET Status = @Status,
            AttemptCount = @AttemptCount,
            LastError = NULL,
            ResultJson = @ResultJson,
            CompletedAt = @CompletedAt,
            UpdatedAt = @UpdatedAt
        WHERE JobQueueId = @JobQueueId";
        AddParameter(command, "@Status", "completed");
        AddParameter(command, "@AttemptCount", attemptCount);
        AddParameter(command, "@ResultJson", resultJson);
        AddParameter(command, "@CompletedAt", endedAt);
        AddParameter(command, "@UpdatedAt", endedAt);
        AddParameter(command, "@JobQueueId", jobId);
        await ExecuteNonQueryAsync(command, cancellationToken);
    }

    private static async Task HandleFailedAttemptAsync(
        IDbConnection connection,
        DocumentGenerationSettings settings,
        Guid jobId,
        string jobType,
        int currentAttemptCount,
        int maxAttempts,
        DateTime startedAt,
        DateTime endedAt,
        string error,
        CancellationToken cancellationToken)
    {
        var nextAttempt = currentAttemptCount + 1;
        var hasMoreRetries = nextAttempt < maxAttempts;
        var status = hasMoreRetries ? "queued" : "failed";
        var delayMs = hasMoreRetries
            ? Math.Max(settings.RetryBaseDelayMilliseconds, 100) * (int)Math.Pow(2, Math.Max(0, nextAttempt - 1))
            : 0;
        var availableAt = hasMoreRetries ? DateTime.UtcNow.AddMilliseconds(delayMs) : DateTime.UtcNow;

        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
            UPDATE JobQueue
            SET Status = @Status,
                AttemptCount = @AttemptCount,
                LastError = @LastError,
                AvailableAt = @AvailableAt,
                CompletedAt = @CompletedAt,
                UpdatedAt = @UpdatedAt
            WHERE JobQueueId = @JobQueueId";
            AddParameter(command, "@Status", status);
            AddParameter(command, "@AttemptCount", nextAttempt);
            AddParameter(command, "@LastError", error);
            AddParameter(command, "@AvailableAt", availableAt);
            AddParameter(command, "@CompletedAt", hasMoreRetries ? DBNull.Value : endedAt);
            AddParameter(command, "@UpdatedAt", endedAt);
            AddParameter(command, "@JobQueueId", jobId);
            await ExecuteNonQueryAsync(command, cancellationToken);
        }

        await InsertAttemptHistoryAsync(connection, jobId, nextAttempt, startedAt, endedAt, hasMoreRetries ? "retry_scheduled" : "failed", error, cancellationToken);

        if (!hasMoreRetries)
        {
            await CreateFailureAlertIfThresholdExceededAsync(connection, settings, jobType, cancellationToken);
        }
    }

    private static async Task InsertAttemptHistoryAsync(
        IDbConnection connection,
        Guid jobId,
        int attemptNumber,
        DateTime startedAt,
        DateTime endedAt,
        string status,
        string? error,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
        INSERT INTO JobQueueAttemptHistory (JobQueueAttemptHistoryId, JobQueueId, AttemptNumber, StartedAt, EndedAt, Status, Error, DurationMilliseconds, CreatedAt)
        VALUES (@JobQueueAttemptHistoryId, @JobQueueId, @AttemptNumber, @StartedAt, @EndedAt, @Status, @Error, @DurationMilliseconds, @CreatedAt)";
        AddParameter(command, "@JobQueueAttemptHistoryId", Guid.NewGuid());
        AddParameter(command, "@JobQueueId", jobId);
        AddParameter(command, "@AttemptNumber", attemptNumber);
        AddParameter(command, "@StartedAt", startedAt);
        AddParameter(command, "@EndedAt", endedAt);
        AddParameter(command, "@Status", status);
        AddParameter(command, "@Error", (object?)error ?? DBNull.Value);
        AddParameter(command, "@DurationMilliseconds", Math.Max(0L, (long)(endedAt - startedAt).TotalMilliseconds));
        AddParameter(command, "@CreatedAt", DateTime.UtcNow);
        await ExecuteNonQueryAsync(command, cancellationToken);
    }

    private static async Task CreateFailureAlertIfThresholdExceededAsync(
        IDbConnection connection,
        DocumentGenerationSettings settings,
        string jobType,
        CancellationToken cancellationToken)
    {
        var threshold = Math.Max(1, settings.FailureAlertThreshold);
        var windowStart = DateTime.UtcNow.AddMinutes(-Math.Max(1, settings.FailureAlertWindowMinutes));

        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = @"
        SELECT COUNT(1)
        FROM JobQueue
        WHERE JobType = @JobType
          AND Status = @Status
          AND UpdatedAt >= @WindowStart";
        AddParameter(countCommand, "@JobType", jobType);
        AddParameter(countCommand, "@Status", "failed");
        AddParameter(countCommand, "@WindowStart", windowStart);

        var count = Convert.ToInt32(await ExecuteScalarAsync(countCommand, cancellationToken) ?? 0);
        if (count < threshold)
        {
            return;
        }

        using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = @"
        IF NOT EXISTS (
            SELECT 1 FROM JobFailureAlert
            WHERE JobType = @JobType
              AND WindowStart = @WindowStart
              AND FailureCount = @FailureCount
        )
        BEGIN
            INSERT INTO JobFailureAlert (JobFailureAlertId, JobType, WindowStart, FailureCount, EscalatedAt, Message, CreatedAt)
            VALUES (@JobFailureAlertId, @JobType, @WindowStart, @FailureCount, @EscalatedAt, @Message, @CreatedAt)
        END";
        AddParameter(insertCommand, "@JobFailureAlertId", Guid.NewGuid());
        AddParameter(insertCommand, "@JobType", jobType);
        AddParameter(insertCommand, "@WindowStart", windowStart);
        AddParameter(insertCommand, "@FailureCount", count);
        AddParameter(insertCommand, "@EscalatedAt", DateTime.UtcNow);
        AddParameter(insertCommand, "@Message", $"Failure threshold reached for {jobType}. Failures in window: {count}.");
        AddParameter(insertCommand, "@CreatedAt", DateTime.UtcNow);
        await ExecuteNonQueryAsync(insertCommand, cancellationToken);
    }

    private static async Task<WorkerControlState> GetWorkerControlAsync(IDbConnectionFactory connectionFactory, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        await OpenConnectionAsync(connection, cancellationToken);

        using var selectCommand = connection.CreateCommand();
        selectCommand.CommandText = @"
        SELECT IsEnabled, IsPaused, ManualTriggerRequested
        FROM JobWorkerControl
        WHERE JobType = @JobType";
        AddParameter(selectCommand, "@JobType", JobType);

        using var reader = await ExecuteReaderAsync(selectCommand, cancellationToken);
        if (await ReadAsync(reader, cancellationToken))
        {
            return new WorkerControlState(
                Convert.ToBoolean(reader["IsEnabled"]),
                Convert.ToBoolean(reader["IsPaused"]),
                Convert.ToBoolean(reader["ManualTriggerRequested"]));
        }

        using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = @"
        INSERT INTO JobWorkerControl (JobType, IsEnabled, IsPaused, ManualTriggerRequested, UpdatedAt)
        VALUES (@JobType, 1, 0, 0, @UpdatedAt)";
        AddParameter(insertCommand, "@JobType", JobType);
        AddParameter(insertCommand, "@UpdatedAt", DateTime.UtcNow);
        await ExecuteNonQueryAsync(insertCommand, cancellationToken);

        return new WorkerControlState(true, false, false);
    }

    private static async Task ResetManualTriggerAsync(IDbConnectionFactory connectionFactory, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        await OpenConnectionAsync(connection, cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = @"
        UPDATE JobWorkerControl
        SET ManualTriggerRequested = 0,
            UpdatedAt = @UpdatedAt
        WHERE JobType = @JobType";
        AddParameter(command, "@UpdatedAt", DateTime.UtcNow);
        AddParameter(command, "@JobType", JobType);
        await ExecuteNonQueryAsync(command, cancellationToken);
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static async Task OpenConnectionAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        if (connection is DbConnection dbConnection)
        {
            await dbConnection.OpenAsync(cancellationToken);
            return;
        }

        connection.Open();
    }

    private static async Task<IDataReader> ExecuteReaderAsync(IDbCommand command, CancellationToken cancellationToken)
    {
        if (command is DbCommand dbCommand)
        {
            return await dbCommand.ExecuteReaderAsync(cancellationToken);
        }

        return command.ExecuteReader();
    }

    private static Task<bool> ReadAsync(IDataReader reader, CancellationToken cancellationToken)
    {
        if (reader is DbDataReader dbReader)
        {
            return dbReader.ReadAsync(cancellationToken);
        }

        return Task.FromResult(reader.Read());
    }

    private static async Task<int> ExecuteNonQueryAsync(IDbCommand command, CancellationToken cancellationToken)
    {
        if (command is DbCommand dbCommand)
        {
            return await dbCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        return command.ExecuteNonQuery();
    }

    private static async Task<object?> ExecuteScalarAsync(IDbCommand command, CancellationToken cancellationToken)
    {
        if (command is DbCommand dbCommand)
        {
            return await dbCommand.ExecuteScalarAsync(cancellationToken);
        }

        return command.ExecuteScalar();
    }

    private readonly record struct WorkerControlState(bool IsEnabled, bool IsPaused, bool ManualTriggerRequested);
}
