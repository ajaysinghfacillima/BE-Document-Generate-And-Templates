// TR: LLD-0021 | ORIGIN: MID_LEVEL_DESIGN-0021
using System.Diagnostics;
using AeonDocGen.Api.Policies;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AeonDocGen.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/projects/{projectId}/documents/{documentId}/review")]
public sealed class DocumentsReviewController : ControllerBase
{
    private readonly IDocumentReviewService _documentReviewService;
    private readonly IRequestAuthorizationService _requestAuthorizationService;
    private readonly ILogger<DocumentsReviewController> _logger;

    public DocumentsReviewController(
        IDocumentReviewService documentReviewService,
        IRequestAuthorizationService requestAuthorizationService,
        ILogger<DocumentsReviewController> logger)
    {
        _documentReviewService = documentReviewService;
        _requestAuthorizationService = requestAuthorizationService;
        _logger = logger;
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(DocumentReviewResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ReviewDocument(
        [FromRoute] string projectId,
        [FromRoute] string documentId,
        [FromBody] DocumentReviewRequestDto request,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var correlationId = RequestPolicyUtilities.GetCorrelationId(HttpContext);

        if (request == null)
        {
            return BadRequest(new StandardErrorDto
            {
                Status = StatusCodes.Status400BadRequest,
                TraceId = traceId,
                Code = "INVALID_REQUEST",
                Message = "The request body is required."
            });
        }

        if (!OpaqueIdentifier.TryNormalize(projectId, "project", out var projectGuid))
        {
            return BadRequest(new StandardErrorDto
            {
                Status = StatusCodes.Status400BadRequest,
                TraceId = traceId,
                Code = "INVALID_REQUEST",
                Message = "projectId must be a non-empty identifier."
            });
        }

        if (!OpaqueIdentifier.TryNormalize(documentId, "document", out var documentGuid))
        {
            return BadRequest(new StandardErrorDto
            {
                Status = StatusCodes.Status400BadRequest,
                TraceId = traceId,
                Code = "INVALID_REQUEST",
                Message = "documentId must be a non-empty identifier."
            });
        }

        var authz = await _requestAuthorizationService.AuthorizeAsync(
            HttpContext,
            forbiddenCode: "FORBIDDEN_DOCUMENT_REVIEW",
            forbiddenMessage: "The caller does not have permission to perform the requested review action on this document.",
            requiredAnyRoles: new[] { "Sustainability Consultant", "Admin", "Owner", "PMC" },
            requiredPermission: "documents.review",
            requiredProjectId: projectGuid,
            cancellationToken: cancellationToken);
        if (!authz.IsAuthorized)
        {
            return authz.ErrorResult!;
        }

        var authenticatedUser = authz.User!;
        var tenantId = authz.TenantId;

        if (!RequestPolicyUtilities.TryGetIdempotencyKey(HttpContext, out var idempotencyKey, out var idempotencyError))
        {
            return BadRequest(idempotencyError);
        }

        if (!RequestPolicyUtilities.TryGetIfMatch(HttpContext, out var ifMatchHeader, out var ifMatchError))
        {
            return BadRequest(ifMatchError);
        }

        try
        {
            var result = await _documentReviewService.ReviewDocumentAsync(
                tenantId, projectGuid, documentGuid, authenticatedUser.UserId,
                correlationId, idempotencyKey, ifMatchHeader, request, cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex) when (ex.Message.StartsWith("INVALID_REVIEW_ACTION:"))
        {
            return BadRequest(new StandardErrorDto
            {
                Status = StatusCodes.Status400BadRequest,
                TraceId = traceId,
                Code = "INVALID_REVIEW_ACTION",
                Message = ex.Message["INVALID_REVIEW_ACTION:".Length..]
            });
        }
        catch (ArgumentException ex) when (ex.Message.StartsWith("INVALID_REQUEST_BODY:"))
        {
            return BadRequest(new StandardErrorDto
            {
                Status = StatusCodes.Status400BadRequest,
                TraceId = traceId,
                Code = "INVALID_REQUEST_BODY",
                Message = ex.Message["INVALID_REQUEST_BODY:".Length..]
            });
        }
        catch (KeyNotFoundException ex) when (ex.Message.StartsWith("PROJECT_NOT_FOUND:"))
        {
            return NotFound(new StandardErrorDto
            {
                Status = StatusCodes.Status404NotFound,
                TraceId = traceId,
                Code = "PROJECT_NOT_FOUND",
                Message = ex.Message["PROJECT_NOT_FOUND:".Length..]
            });
        }
        catch (KeyNotFoundException ex) when (ex.Message.StartsWith("DOCUMENT_NOT_FOUND:"))
        {
            return NotFound(new StandardErrorDto
            {
                Status = StatusCodes.Status404NotFound,
                TraceId = traceId,
                Code = "DOCUMENT_NOT_FOUND",
                Message = ex.Message["DOCUMENT_NOT_FOUND:".Length..]
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("ETAG_MISMATCH:"))
        {
            return Conflict(new StandardErrorDto
            {
                Status = StatusCodes.Status409Conflict,
                TraceId = traceId,
                Code = "ETAG_MISMATCH",
                Message = ex.Message["ETAG_MISMATCH:".Length..]
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Idempotency-Key"))
        {
            return Conflict(new StandardErrorDto
            {
                Status = StatusCodes.Status409Conflict,
                TraceId = traceId,
                Code = "IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_PAYLOAD",
                Message = "The provided idempotency key was already used for a different request payload."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during document review. CorrelationId={CorrelationId}", correlationId);
            return StatusCode(StatusCodes.Status500InternalServerError, new StandardErrorDto
            {
                Status = StatusCodes.Status500InternalServerError,
                TraceId = traceId,
                Code = "DOCUMENT_REVIEW_PROCESSING_FAILED",
                Message = "An internal error occurred while recording the document review action."
            });
        }
    }
}
