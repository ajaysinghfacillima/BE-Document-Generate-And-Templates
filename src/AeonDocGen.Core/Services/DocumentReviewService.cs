// TR: LLD-0021 | ORIGIN: MID_LEVEL_DESIGN-0021
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using Microsoft.Extensions.Logging;

namespace AeonDocGen.Core.Services;

/// <summary>
/// Service for document review workflow operations including state transitions,
/// review event persistence, optimistic concurrency, idempotency, and audit logging.
/// </summary>
public sealed class DocumentReviewService : IDocumentReviewService
{
    private static readonly HashSet<string> ValidActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "submit", "startReview", "approve", "reject"
    };

    private static readonly Dictionary<string, (string[] AllowedFrom, string ResultStatus)> TransitionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["submit"] = (new[] { "draft" }, "draft"),
        ["startReview"] = (new[] { "draft" }, "inReview"),
        ["approve"] = (new[] { "inReview" }, "approved"),
        ["reject"] = (new[] { "inReview" }, "rejected")
    };

    private readonly IProjectRepository _projectRepository;
    private readonly IDocumentArtifactRepository _documentArtifactRepository;
    private readonly IDocumentReviewEventRepository _reviewEventRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IIdempotencyRepository _idempotencyRepository;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<DocumentReviewService> _logger;

    public DocumentReviewService(
        IProjectRepository projectRepository,
        IDocumentArtifactRepository documentArtifactRepository,
        IDocumentReviewEventRepository reviewEventRepository,
        IAuditLogRepository auditLogRepository,
        IIdempotencyRepository idempotencyRepository,
        IDbConnectionFactory connectionFactory,
        ILogger<DocumentReviewService> logger)
    {
        _projectRepository = projectRepository;
        _documentArtifactRepository = documentArtifactRepository;
        _reviewEventRepository = reviewEventRepository;
        _auditLogRepository = auditLogRepository;
        _idempotencyRepository = idempotencyRepository;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    // TR: LLD-0021 | ORIGIN: MID_LEVEL_DESIGN-0021
    public async Task<DocumentReviewResponseDto> ReviewDocumentAsync(
        Guid tenantId,
        Guid projectId,
        Guid documentId,
        Guid actorUserId,
        string correlationId,
        string idempotencyKey,
        string ifMatchEtag,
        DocumentReviewRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Document review request received. TenantId={TenantId}, ProjectId={ProjectId}, DocumentId={DocumentId}, ActorUserId={ActorUserId}, Action={Action}, CorrelationId={CorrelationId}, IdempotencyKey={IdempotencyKey}",
            tenantId, projectId, documentId, actorUserId, request.Action, correlationId, idempotencyKey);

        try
        {

        // Check idempotency
        var requestHash = ComputeRequestHash(projectId, documentId, request);
        var existingIdempotency = await _idempotencyRepository.GetByKeyAsync(idempotencyKey, tenantId, cancellationToken);
        if (existingIdempotency != null)
        {
            if (existingIdempotency.RequestHash == requestHash)
            {
                _logger.LogInformation(
                    "Idempotent replay detected for document review. IdempotencyKey={IdempotencyKey}, TenantId={TenantId}, CorrelationId={CorrelationId}",
                    idempotencyKey, tenantId, correlationId);
                return JsonSerializer.Deserialize<DocumentReviewResponseDto>(existingIdempotency.ResponseJson)!;
            }
            throw new InvalidOperationException("Idempotency-Key has been used with a different payload.");
        }

        // Validate request
        ValidateRequest(request);

        // Validate transition intent
        var action = request.Action;
        if (!TransitionMap.TryGetValue(action, out var transition))
        {
            throw new ArgumentException($"INVALID_REVIEW_ACTION:action must be one of: {string.Join(", ", ValidActions)}.");
        }

        DocumentReviewResponseDto? response = null;
        string? responseJson = null;

        // Validate and persist atomically
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            await DbExecutionUtilities.OpenConnectionAsync(connection, cancellationToken);
            using var transaction = connection.BeginTransaction();

            try
            {
                var projectExists = await _projectRepository.ExistsAsync(projectId, tenantId, connection, transaction, cancellationToken);
                if (!projectExists)
                {
                    throw new KeyNotFoundException("PROJECT_NOT_FOUND:The specified project was not found or is not accessible in the tenant scope.");
                }

                var document = await _documentArtifactRepository.GetByIdAsync(documentId, projectId, tenantId, connection, transaction, cancellationToken);
                if (document == null)
                {
                    throw new KeyNotFoundException("DOCUMENT_NOT_FOUND:The specified document artifact was not found in the project scope.");
                }

                if (!string.Equals(document.Etag, ifMatchEtag, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("ETAG_MISMATCH:The document has been modified by another operation and the supplied concurrency token is no longer valid.");
                }

                if (!transition.AllowedFrom.Contains(document.ReviewStatus, StringComparer.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"INVALID_REVIEW_ACTION:The requested review action '{action}' is invalid for the current document review status '{document.ReviewStatus}'.");
                }

                var previousStatus = document.ReviewStatus;
                var now = DateTime.UtcNow;
                var reviewEventId = Guid.NewGuid();

                document.ReviewStatus = transition.ResultStatus;
                document.UpdatedAt = now;
                document.Version += 1;
                document.Etag = $"\"{document.Version}-{Guid.NewGuid():N}\"";

                if (string.Equals(action, "approve", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(action, "reject", StringComparison.OrdinalIgnoreCase))
                {
                    document.ReviewedByUserId = actorUserId;
                    document.ReviewedAt = now;
                }

                var reviewEvent = new DocumentReviewEventEntity
                {
                    DocumentReviewEventId = reviewEventId,
                    DocumentArtifactId = documentId,
                    Action = action,
                    ActorUserId = actorUserId,
                    Comments = request.Comments,
                    CreatedAt = now
                };

                response = BuildResponse(document, reviewEvent);
                responseJson = JsonSerializer.Serialize(response);

                var rowsAffected = await _documentArtifactRepository.UpdateReviewStatusAsync(document, ifMatchEtag, connection, transaction, cancellationToken);
                if (rowsAffected == 0)
                {
                    throw new InvalidOperationException("ETAG_MISMATCH:The document has been modified by another operation and the supplied concurrency token is no longer valid.");
                }

                await _reviewEventRepository.CreateAsync(reviewEvent, connection, transaction, cancellationToken);

                var auditLog = CreateAuditLogEntry(tenantId, actorUserId, correlationId, documentId,
                    $"documents.review.{action}", "DocumentArtifact", "project", projectId,
                    "success",
                    JsonSerializer.Serialize(new { ReviewStatus = previousStatus }),
                    JsonSerializer.Serialize(new { ReviewStatus = document.ReviewStatus }),
                    string.IsNullOrWhiteSpace(request.Comments) ? action : request.Comments, now);
                await _auditLogRepository.InsertAuditLogAsync(auditLog, connection, transaction, cancellationToken);

                var idempotencyRecord = new IdempotencyRecordEntity
                {
                    IdempotencyKey = idempotencyKey,
                    TenantId = tenantId,
                    RequestHash = requestHash,
                    ResponseJson = responseJson!,
                    StatusCode = 200,
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
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not InvalidOperationException && ex is not ArgumentException && ex is not KeyNotFoundException)
        {
            _logger.LogError(ex,
                "Database persistence failed for document review. TenantId={TenantId}, ProjectId={ProjectId}, DocumentId={DocumentId}, CorrelationId={CorrelationId}",
                tenantId, projectId, documentId, correlationId);
            throw new InvalidOperationException("DOCUMENT_REVIEW_PROCESSING_FAILED:An internal error occurred while recording the document review action.", ex);
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "Document review completed. TenantId={TenantId}, ProjectId={ProjectId}, DocumentId={DocumentId}, Action={Action}, ResultingStatus={ResultingStatus}, CorrelationId={CorrelationId}, LatencyMs={LatencyMs}",
            tenantId, projectId, documentId, action, response?.ReviewStatus, correlationId, stopwatch.ElapsedMilliseconds);

            return response!;
        }
        catch (Exception ex) when (ex is ArgumentException || ex is KeyNotFoundException || ex is InvalidOperationException)
        {
            await TryWriteFailureAuditLogAsync(tenantId, actorUserId, correlationId, projectId, documentId, request, ex, cancellationToken);
            throw;
        }
    }

    private static void ValidateRequest(DocumentReviewRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Action))
        {
            throw new ArgumentException("INVALID_REQUEST_BODY:action is required.");
        }

        if (!ValidActions.Contains(request.Action))
        {
            throw new ArgumentException($"INVALID_REVIEW_ACTION:action must be one of: {string.Join(", ", ValidActions)}.");
        }

        if (request.Comments != null && request.Comments.Length > 2000)
        {
            throw new ArgumentException("INVALID_REQUEST_BODY:comments length must not exceed 2000 characters.");
        }
    }

    private static DocumentReviewResponseDto BuildResponse(DocumentArtifactEntity document, DocumentReviewEventEntity reviewEvent)
    {
        return new DocumentReviewResponseDto
        {
            DocumentId = document.DocumentArtifactId.ToString(),
            ProjectId = document.ProjectId.ToString(),
            ReviewStatus = document.ReviewStatus,
            Event = new DocumentReviewEventDto
            {
                ReviewEventId = reviewEvent.DocumentReviewEventId.ToString(),
                Action = reviewEvent.Action,
                ActorUserId = reviewEvent.ActorUserId.ToString(),
                Comments = reviewEvent.Comments,
                CreatedAt = reviewEvent.CreatedAt
            },
            ReviewedByUserId = document.ReviewedByUserId?.ToString(),
            ReviewedAt = document.ReviewedAt,
            Etag = document.Etag
        };
    }

    private static AuditLogEntity CreateAuditLogEntry(
        Guid tenantId, Guid actorUserId, string correlationId, Guid resourceId,
        string action, string resourceType, string scopeType, Guid scopeId,
        string outcome, string? beforeJson, string? afterJson, string? reason, DateTime timestamp)
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
            Reason = reason,
            ImmutableHash = ComputeImmutableHash(tenantId, actorUserId, action, correlationId, timestamp)
        };
    }

    private static string ComputeImmutableHash(Guid tenantId, Guid actorUserId, string action, string correlationId, DateTime timestamp)
    {
        var payload = $"{tenantId}|{actorUserId}|{action}|{correlationId}|{timestamp:O}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hashBytes);
    }

    private static string ComputeRequestHash(Guid projectId, Guid documentId, DocumentReviewRequestDto request)
    {
        var payload = $"{projectId}|{documentId}|{request.Action}|{request.Comments ?? ""}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hashBytes);
    }

    private async Task TryWriteFailureAuditLogAsync(
        Guid tenantId,
        Guid actorUserId,
        string correlationId,
        Guid projectId,
        Guid documentId,
        DocumentReviewRequestDto request,
        Exception exception,
        CancellationToken cancellationToken)
    {
        try
        {
            var persistedDocument = await _documentArtifactRepository.GetByIdAsync(documentId, projectId, tenantId, cancellationToken);
            var persistedStatus = persistedDocument?.ReviewStatus;
            var now = DateTime.UtcNow;
            var audit = CreateAuditLogEntry(
                tenantId,
                actorUserId,
                correlationId,
                documentId,
                $"documents.review.{request.Action}",
                "DocumentArtifact",
                "project",
                projectId,
                "failure",
                JsonSerializer.Serialize(new { ReviewStatus = persistedStatus }),
                JsonSerializer.Serialize(new { ReviewStatus = persistedStatus, error = exception.Message }),
                string.IsNullOrWhiteSpace(request.Comments) ? exception.Message : $"{request.Comments} | {exception.Message}",
                now);
            await _auditLogRepository.InsertAuditLogAsync(audit, cancellationToken);
        }
        catch (Exception auditEx)
        {
            _logger.LogError(auditEx,
                "Failed to write failure audit log. TenantId={TenantId}, ProjectId={ProjectId}, DocumentId={DocumentId}, CorrelationId={CorrelationId}",
                tenantId, projectId, documentId, correlationId);
        }
    }
}
