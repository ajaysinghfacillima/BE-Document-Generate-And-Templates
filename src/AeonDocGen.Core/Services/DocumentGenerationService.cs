// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using AeonDocGen.Core.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AeonDocGen.Core.Services;

/// <summary>
/// Service for document generation including validation, template resolution,
/// source resolution, branding, watermark, rendering, checksum, persistence,
/// storage, and audit logging.
/// </summary>
public sealed class DocumentGenerationService : IDocumentGenerationService
{
    private static readonly HashSet<string> SupportedDocumentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "narrative", "calculator", "simulationSummary", "formReadyData", "scorecard", "checklist", "report"
    };

    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "pdf", "docx", "xlsx", "json", "pptx"
    };

    private static readonly HashSet<string> SupportedSourceEntityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "artifact", "simulationJob", "scorecard", "preAssessmentRun", "auditorQuery", "recommendation"
    };

    private readonly IProjectRepository _projectRepository;
    private readonly ITemplateResolutionRepository _templateResolutionRepository;
    private readonly ISourceEntityRepository _sourceEntityRepository;
    private readonly IBrandingAssetRepository _brandingAssetRepository;
    private readonly IDocumentArtifactRepository _documentArtifactRepository;
    private readonly IDocumentSourceRepository _documentSourceRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IIdempotencyRepository _idempotencyRepository;
    private readonly IDocumentStorageClient _fileStorageClient;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly DocumentGenerationSettings _settings;
    private readonly ILogger<DocumentGenerationService> _logger;

    public DocumentGenerationService(
        IProjectRepository projectRepository,
        ITemplateResolutionRepository templateResolutionRepository,
        ISourceEntityRepository sourceEntityRepository,
        IBrandingAssetRepository brandingAssetRepository,
        IDocumentArtifactRepository documentArtifactRepository,
        IDocumentSourceRepository documentSourceRepository,
        IAuditLogRepository auditLogRepository,
        IIdempotencyRepository idempotencyRepository,
        IDocumentStorageClient fileStorageClient,
        IDbConnectionFactory connectionFactory,
        IOptions<DocumentGenerationSettings> settings,
        ILogger<DocumentGenerationService> logger)
    {
        _projectRepository = projectRepository;
        _templateResolutionRepository = templateResolutionRepository;
        _sourceEntityRepository = sourceEntityRepository;
        _brandingAssetRepository = brandingAssetRepository;
        _documentArtifactRepository = documentArtifactRepository;
        _documentSourceRepository = documentSourceRepository;
        _auditLogRepository = auditLogRepository;
        _idempotencyRepository = idempotencyRepository;
        _fileStorageClient = fileStorageClient;
        _connectionFactory = connectionFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    // TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
    public async Task<DocumentArtifactResponseDto> GenerateDocumentAsync(
        Guid tenantId,
        Guid projectId,
        Guid actorUserId,
        string correlationId,
        string idempotencyKey,
        GenerateDocumentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Document generation request received. TenantId={TenantId}, ProjectId={ProjectId}, ActorUserId={ActorUserId}, CorrelationId={CorrelationId}, IdempotencyKey={IdempotencyKey}",
            tenantId, projectId, actorUserId, correlationId, idempotencyKey);

        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("projectId must be a non-empty identifier.");
        }

        // Check idempotency
        var requestHash = ComputeRequestHash(projectId, request);
        var existingIdempotency = await _idempotencyRepository.GetByKeyAsync(idempotencyKey, tenantId, cancellationToken);
        if (existingIdempotency != null)
        {
            if (existingIdempotency.RequestHash == requestHash)
            {
                _logger.LogInformation(
                    "Idempotent replay detected for document generation. IdempotencyKey={IdempotencyKey}, TenantId={TenantId}, CorrelationId={CorrelationId}",
                    idempotencyKey, tenantId, correlationId);
                return JsonSerializer.Deserialize<DocumentArtifactResponseDto>(existingIdempotency.ResponseJson)!;
            }
            throw new InvalidOperationException("Idempotency-Key has been used with a different payload.");
        }

        // Validate request payload
        ValidateRequest(request);

        // Validate project exists
        var projectExists = await _projectRepository.ExistsAsync(projectId, tenantId, cancellationToken);
        if (!projectExists)
        {
            throw new KeyNotFoundException($"Project '{projectId}' not found in tenant scope.");
        }

        // Validate template
        if (!OpaqueIdentifier.TryNormalize(request.TemplateId, "template", out var templateId))
        {
            throw new ArgumentException("templateId must be a non-empty identifier.");
        }

        var template = await _templateResolutionRepository.GetByIdAsync(templateId, tenantId, cancellationToken);
        if (template == null || !template.IsActive)
        {
            throw new ArgumentException($"templateId '{request.TemplateId}' does not exist, is inactive, or is not accessible to the tenant.");
        }

        // Validate template supports requested documentType
        if (!string.Equals(template.DocumentType, request.DocumentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Template '{request.TemplateId}' does not support document type '{request.DocumentType}'.");
        }

        if (!TemplateSupportsRequestedFormat(template, request.DocumentType, request.Format))
        {
            throw new ArgumentException($"Template '{request.TemplateId}' does not support format '{request.Format}' for document type '{request.DocumentType}'.");
        }

        // Resolve published template version
        var templateVersion = await _templateResolutionRepository.GetLatestPublishedVersionAsync(templateId, cancellationToken);
        if (templateVersion == null)
        {
            throw new ArgumentException($"No published version available for template '{request.TemplateId}'.");
        }

        _logger.LogInformation(
            "Template resolved. TemplateId={TemplateId}, TemplateVersion={TemplateVersion}, DocumentType={DocumentType}, CorrelationId={CorrelationId}",
            templateId, templateVersion.TemplateVersion, request.DocumentType, correlationId);

        // Resolve source entities
        var parsedSourceIds = new List<Guid>();
        foreach (var sourceIdStr in request.SourceIds)
        {
            if (!OpaqueIdentifier.TryNormalize(sourceIdStr, "source", out var sourceId))
            {
                throw new ArgumentException($"sourceId '{sourceIdStr}' must be a non-empty identifier.");
            }
            parsedSourceIds.Add(sourceId);
        }

        var sourceTypes = await _sourceEntityRepository.ResolveSourceEntityTypesAsync(parsedSourceIds, projectId, tenantId, actorUserId, cancellationToken);
        var resolvedSources = new List<(Guid Id, string EntityType)>();
        foreach (var sourceId in parsedSourceIds)
        {
            if (!sourceTypes.TryGetValue(sourceId, out var entityType))
            {
                throw new ArgumentException($"sourceId '{sourceId}' is invalid, inaccessible, or outside the target project scope.");
            }

            if (!SupportedSourceEntityTypes.Contains(entityType))
            {
                throw new ArgumentException($"sourceId '{sourceId}' resolved to unsupported source type '{entityType}'.");
            }

            resolvedSources.Add((sourceId, entityType));
        }

        _logger.LogInformation(
            "Source entities resolved. Count={SourceCount}, CorrelationId={CorrelationId}",
            resolvedSources.Count, correlationId);

        // Load branding if requested
        bool brandingApplied = false;
        BrandingAssetEntity? brandingAsset = null;
        if (request.IncludeBranding)
        {
            brandingAsset = await _brandingAssetRepository.GetByTenantIdAsync(tenantId, cancellationToken);
            brandingApplied = brandingAsset != null;
            if (!brandingApplied)
            {
                _logger.LogInformation(
                    "Branding requested but no active branding asset configured. Proceeding without branding. TenantId={TenantId}, CorrelationId={CorrelationId}",
                    tenantId, correlationId);
            }
        }

        var documentId = ComputeDeterministicDocumentId(tenantId, projectId, idempotencyKey);
        var renderPayload = new DocumentRenderStoreJobPayload
        {
            TenantId = tenantId,
            ProjectId = projectId,
            DocumentId = documentId,
            CorrelationId = correlationId,
            FooterVersionPrefix = _settings.FooterVersionFormat,
            DocumentType = request.DocumentType,
            Format = request.Format,
            WatermarkText = request.WatermarkText,
            BrandingApplied = brandingApplied,
            TemplateVersion = templateVersion.TemplateVersion,
            TemplateVersionId = templateVersion.TemplateVersionId,
            BrandingLogoStorageUri = brandingAsset?.LogoStorageUri,
            BrandingColorsJson = brandingAsset?.ColorsJson,
            Sources = resolvedSources.Select(s => new DocumentRenderSourcePayload { Id = s.Id, EntityType = s.EntityType }).ToList()
        };

        DocumentRenderStoreJobResult renderAndStoreResult;
        if (_settings.EnableInlineRenderWorkerFallback)
        {
            renderAndStoreResult = await RenderAndStoreInlineAsync(renderPayload, cancellationToken);
        }
        else
        {
            var renderJobId = await EnqueueRenderStoreJobAsync(tenantId, renderPayload, cancellationToken);
            renderAndStoreResult = await WaitForRenderStoreJobCompletionAsync(renderJobId, correlationId, cancellationToken);
        }
        renderAndStoreResult = EnsureRenderMetadata(renderAndStoreResult, request.Format, templateVersion.TemplateVersion, _settings.FooterVersionFormat);

        _logger.LogInformation(
            "Generated document stored. StorageUri={StorageUri}, CorrelationId={CorrelationId}",
            renderAndStoreResult.StorageUri, correlationId);

        // Build entity
        var now = DateTime.UtcNow;
        var entity = new DocumentArtifactEntity
        {
            DocumentArtifactId = documentId,
            TenantId = tenantId,
            ProjectId = projectId,
            DocumentType = request.DocumentType,
            Format = request.Format,
            TemplateId = templateId,
            TemplateVersion = templateVersion.TemplateVersion,
            BrandingApplied = brandingApplied,
            WatermarkApplied = renderAndStoreResult.WatermarkApplied,
            FooterVersionText = renderAndStoreResult.FooterVersionText,
            StorageUri = renderAndStoreResult.StorageUri,
            ChecksumSha256 = renderAndStoreResult.ChecksumSha256,
            ReviewStatus = "draft",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
            Etag = $"\"1-{Guid.NewGuid():N}\""
        };

        var response = BuildResponse(entity);
        var responseJson = JsonSerializer.Serialize(response);

        // Persist metadata atomically
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            await DbExecutionUtilities.OpenConnectionAsync(connection, cancellationToken);
            using var transaction = connection.BeginTransaction();

            try
            {
                await _documentArtifactRepository.CreateAsync(entity, connection, transaction, cancellationToken);

                foreach (var (sourceId, entityType) in resolvedSources)
                {
                    var docSource = new DocumentSourceEntity
                    {
                        DocumentSourceId = Guid.NewGuid(),
                        DocumentArtifactId = documentId,
                        SourceEntityType = entityType,
                        SourceEntityId = sourceId
                    };
                    await _documentSourceRepository.CreateAsync(docSource, connection, transaction, cancellationToken);
                }

                var auditLog = CreateAuditLogEntry(tenantId, actorUserId, correlationId, documentId,
                    "documents.generate", "DocumentArtifact", "project", projectId, "success", null,
                    JsonSerializer.Serialize(new { entity.DocumentType, entity.Format, entity.TemplateVersion, entity.BrandingApplied, entity.WatermarkApplied }),
                    now);
                await _auditLogRepository.InsertAuditLogAsync(auditLog, connection, transaction, cancellationToken);

                var idempotencyRecord = new IdempotencyRecordEntity
                {
                    IdempotencyKey = idempotencyKey,
                    TenantId = tenantId,
                    RequestHash = requestHash,
                    ResponseJson = responseJson,
                    StatusCode = 201,
                    CreatedAt = now
                };
                await _idempotencyRepository.InsertAsync(idempotencyRecord, connection, transaction, cancellationToken);

                await DbExecutionUtilities.CommitTransactionAsync(transaction, cancellationToken);
            }
            catch
            {
                await DbExecutionUtilities.RollbackTransactionAsync(transaction, cancellationToken);
                throw;
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException && ex is not ArgumentException && ex is not KeyNotFoundException)
        {
            _logger.LogError(ex,
                "Database persistence failed for document artifact. TenantId={TenantId}, ProjectId={ProjectId}, DocumentId={DocumentId}, CorrelationId={CorrelationId}",
                tenantId, projectId, documentId, correlationId);
            throw new InvalidOperationException("Failed to persist document artifact metadata.", ex);
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "Document generation completed. TenantId={TenantId}, ProjectId={ProjectId}, DocumentArtifactId={DocumentArtifactId}, DocumentType={DocumentType}, Format={Format}, TemplateVersion={TemplateVersion}, BrandingApplied={BrandingApplied}, WatermarkApplied={WatermarkApplied}, SourceCount={SourceCount}, CorrelationId={CorrelationId}, LatencyMs={LatencyMs}",
            tenantId, projectId, documentId, request.DocumentType, request.Format, templateVersion.TemplateVersion,
            brandingApplied, renderAndStoreResult.WatermarkApplied, resolvedSources.Count, correlationId, stopwatch.ElapsedMilliseconds);

        return response;
    }

    // TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
    private void ValidateRequest(GenerateDocumentRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.DocumentType))
        {
            throw new ArgumentException("documentType is required.");
        }

        if (!SupportedDocumentTypes.Contains(request.DocumentType))
        {
            throw new ArgumentException($"documentType must be one of: {string.Join(", ", SupportedDocumentTypes)}.");
        }

        if (string.IsNullOrWhiteSpace(request.Format))
        {
            throw new ArgumentException("format is required.");
        }

        if (!SupportedFormats.Contains(request.Format))
        {
            throw new ArgumentException($"format must be one of: {string.Join(", ", SupportedFormats)}.");
        }

        if (string.IsNullOrWhiteSpace(request.TemplateId))
        {
            throw new ArgumentException("templateId is required.");
        }

        if (request.SourceIds == null || request.SourceIds.Count == 0)
        {
            throw new ArgumentException("sourceIds must contain at least one source identifier.");
        }

        if (request.SourceIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("sourceIds must not contain empty identifiers.");
        }

        if (!string.IsNullOrWhiteSpace(request.WatermarkText))
        {
            var trimmed = request.WatermarkText.Trim();
            if (trimmed.Length == 0)
            {
                throw new ArgumentException("watermarkText, if provided, must be a non-empty string after trimming.");
            }
            if (trimmed.Length > _settings.MaxWatermarkLength)
            {
                throw new ArgumentException($"watermarkText exceeds the maximum allowed length of {_settings.MaxWatermarkLength} characters.");
            }
        }
    }

    private static bool TemplateSupportsRequestedFormat(TemplateEntity template, string documentType, string format)
    {
        if (!string.IsNullOrWhiteSpace(template.SupportedFormatsCsv))
        {
            var templateFormats = template.SupportedFormatsCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return templateFormats.Contains(format, StringComparer.OrdinalIgnoreCase);
        }

        return SupportedDocumentTypes.Contains(documentType) && SupportedFormats.Contains(format);
    }

    // TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
    private static DocumentArtifactResponseDto BuildResponse(DocumentArtifactEntity entity)
    {
        return new DocumentArtifactResponseDto
        {
            Id = entity.DocumentArtifactId.ToString(),
            TenantId = entity.TenantId.ToString(),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Version = entity.Version,
            Etag = entity.Etag,
            ProjectId = entity.ProjectId.ToString(),
            DocumentType = entity.DocumentType,
            Format = entity.Format,
            TemplateId = entity.TemplateId.ToString(),
            TemplateVersion = entity.TemplateVersion,
            BrandingApplied = entity.BrandingApplied,
            WatermarkApplied = entity.WatermarkApplied,
            FooterVersionText = entity.FooterVersionText,
            StorageUri = entity.StorageUri,
            ChecksumSha256 = entity.ChecksumSha256,
            ReviewStatus = entity.ReviewStatus
        };
    }

    private static AuditLogEntity CreateAuditLogEntry(
        Guid tenantId, Guid actorUserId, string correlationId, Guid resourceId,
        string action, string resourceType, string scopeType, Guid scopeId,
        string outcome, string? beforeJson, string? afterJson, DateTime timestamp)
    {
        return new AuditLogEntity
        {
            AuditLogId = Guid.NewGuid(),
            TenantId = tenantId,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
            ActorUserId = actorUserId,
            ActorType = "user",
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            ScopeType = scopeType,
            ScopeId = scopeId,
            Outcome = outcome,
            CorrelationId = correlationId,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            ImmutableHash = ComputeImmutableHash(tenantId, actorUserId, action, correlationId, timestamp)
        };
    }

    private static string ComputeImmutableHash(Guid tenantId, Guid actorUserId, string action, string correlationId, DateTime timestamp)
    {
        var payload = $"{tenantId}|{actorUserId}|{action}|{correlationId}|{timestamp:O}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hashBytes);
    }

    private static string ComputeRequestHash(Guid projectId, GenerateDocumentRequestDto request)
    {
        var payload = $"{projectId}|{request.DocumentType}|{request.Format}|{request.TemplateId}|{request.IncludeBranding}|{request.WatermarkText ?? ""}|{string.Join(",", request.SourceIds ?? new List<string>())}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hashBytes);
    }

    private static Guid ComputeDeterministicDocumentId(Guid tenantId, Guid projectId, string idempotencyKey)
    {
        var seed = $"{tenantId:N}|{projectId:N}|{idempotencyKey.Trim()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }

    private async Task<Guid> EnqueueRenderStoreJobAsync(
        Guid tenantId,
        DocumentRenderStoreJobPayload payload,
        CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid();
        var payloadJson = JsonSerializer.Serialize(payload);

        using var connection = _connectionFactory.CreateConnection();
        await DbExecutionUtilities.OpenConnectionAsync(connection, cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = @"
        INSERT INTO JobQueue (
            JobQueueId, TenantId, JobType, Status, Priority, PayloadJson, AvailableAt, CreatedAt, UpdatedAt, AttemptCount, MaxAttempts, JobTimeoutSeconds
        ) VALUES (
            @JobQueueId, @TenantId, @JobType, @Status, @Priority, @PayloadJson, @AvailableAt, @CreatedAt, @UpdatedAt, @AttemptCount, @MaxAttempts, @JobTimeoutSeconds
        )";

        AddParameter(command, "@JobQueueId", jobId);
        AddParameter(command, "@TenantId", tenantId);
        AddParameter(command, "@JobType", "document.render-store");
        AddParameter(command, "@Status", "queued");
        AddParameter(command, "@Priority", 5);
        AddParameter(command, "@PayloadJson", payloadJson);
        AddParameter(command, "@AvailableAt", DateTime.UtcNow);
        AddParameter(command, "@CreatedAt", DateTime.UtcNow);
        AddParameter(command, "@UpdatedAt", DateTime.UtcNow);
        AddParameter(command, "@AttemptCount", 0);
        AddParameter(command, "@MaxAttempts", Math.Max(1, _settings.RenderStoreMaxRetryAttempts));
        AddParameter(command, "@JobTimeoutSeconds", Math.Max(1, _settings.DefaultJobTimeoutSeconds));
        await ExecuteNonQueryAsync(command, cancellationToken);

        return jobId;
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static Task ExecuteNonQueryAsync(IDbCommand command, CancellationToken cancellationToken)
    {
        if (command is DbCommand dbCommand)
        {
            return dbCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        command.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    private async Task<DocumentRenderStoreJobResult> WaitForRenderStoreJobCompletionAsync(
        Guid jobId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(_settings.RenderQueueWaitTimeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var connection = _connectionFactory.CreateConnection();
            await DbExecutionUtilities.OpenConnectionAsync(connection, cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT Status, LastError, ResultJson
            FROM JobQueue
            WHERE JobQueueId = @JobQueueId";
            AddParameter(command, "@JobQueueId", jobId);

            using var reader = await ExecuteReaderAsync(command, cancellationToken);
            if (await ReadAsync(reader, cancellationToken))
            {
                var status = reader["Status"]?.ToString() ?? string.Empty;
                if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
                {
                    var resultJson = reader["ResultJson"]?.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(resultJson))
                    {
                        throw new InvalidOperationException("Failed to render and store generated document. Job completed without result payload.");
                    }

                    return JsonSerializer.Deserialize<DocumentRenderStoreJobResult>(resultJson)
                        ?? throw new InvalidOperationException("Failed to deserialize durable render job result payload.");
                }

                if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    var error = reader["LastError"]?.ToString() ?? "Render/store background job failed.";
                    throw new InvalidOperationException(error);
                }
            }

            await Task.Delay(_settings.RenderQueuePollIntervalMilliseconds, cancellationToken);
        }

        throw new TimeoutException($"Timed out waiting for durable render/store job completion. JobQueueId={jobId}, CorrelationId={correlationId}");
    }

    private async Task<DocumentRenderStoreJobResult> RenderAndStoreInlineAsync(
        DocumentRenderStoreJobPayload payload,
        CancellationToken cancellationToken)
    {
        var renderedContent = DocumentRenderRuntime.RenderDocument(payload);
        var checksumSha256 = DocumentRenderRuntime.ComputeChecksumSha256(renderedContent);
        var watermarkApplied = !string.IsNullOrWhiteSpace(payload.WatermarkText);
        var footerVersionText = DocumentRenderRuntime.BuildFooterVersionText(
            payload.FooterVersionPrefix,
            payload.TemplateVersion,
            payload.Format);

        var storagePath = $"{payload.TenantId}/projects/{payload.ProjectId}/generated/{payload.DocumentId}/{payload.DocumentType}.{payload.Format}";
        var storageUri = await _fileStorageClient.StoreFileAsync(storagePath, renderedContent, cancellationToken);

        return new DocumentRenderStoreJobResult
        {
            StorageUri = storageUri,
            ChecksumSha256 = checksumSha256,
            WatermarkApplied = watermarkApplied,
            FooterVersionText = footerVersionText
        };
    }

    private static DocumentRenderStoreJobResult EnsureRenderMetadata(
        DocumentRenderStoreJobResult result,
        string format,
        string templateVersion,
        string footerVersionPrefix)
    {
        if (string.IsNullOrWhiteSpace(result.StorageUri))
        {
            throw new InvalidOperationException("Failed to render and store generated document. Storage URI is missing.");
        }

        if (!IsValidSha256Hex(result.ChecksumSha256))
        {
            throw new InvalidOperationException("Failed to render and store generated document. checksumSha256 must be a 64-character lowercase SHA-256 hex string.");
        }

        if (DocumentRenderRuntime.SupportsFooterVersion(format) && string.IsNullOrWhiteSpace(result.FooterVersionText))
        {
            result.FooterVersionText = DocumentRenderRuntime.BuildFooterVersionText(
                footerVersionPrefix,
                templateVersion,
                format);
        }

        return result;
    }

    private static bool IsValidSha256Hex(string checksum)
    {
        if (checksum.Length != 64)
        {
            return false;
        }

        foreach (var ch in checksum)
        {
            var isHexDigit = (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f');
            if (!isHexDigit)
            {
                return false;
            }
        }

        return true;
    }

    private async Task UpdateRenderStoreJobStateAsync(
        Guid jobId,
        string status,
        string? error,
        string? resultJson,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await DbExecutionUtilities.OpenConnectionAsync(connection, cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = @"
        UPDATE JobQueue
        SET Status = @Status,
            LastError = @LastError,
            ResultJson = @ResultJson,
            UpdatedAt = @UpdatedAt
        WHERE JobQueueId = @JobQueueId";
        AddParameter(command, "@Status", status);
        AddParameter(command, "@LastError", (object?)error ?? DBNull.Value);
        AddParameter(command, "@ResultJson", (object?)resultJson ?? DBNull.Value);
        AddParameter(command, "@UpdatedAt", DateTime.UtcNow);
        AddParameter(command, "@JobQueueId", jobId);
        await ExecuteNonQueryAsync(command, cancellationToken);
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
}
