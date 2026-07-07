// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
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
[Route("api/v1/projects/{projectId}/documents/generate")]
public sealed class DocumentsGenerationController : ControllerBase
{
    private readonly IDocumentGenerationService _documentGenerationService;
    private readonly IRequestAuthorizationService _requestAuthorizationService;
    private readonly ILogger<DocumentsGenerationController> _logger;

    public DocumentsGenerationController(
        IDocumentGenerationService documentGenerationService,
        IRequestAuthorizationService requestAuthorizationService,
        ILogger<DocumentsGenerationController> logger)
    {
        _documentGenerationService = documentGenerationService;
        _requestAuthorizationService = requestAuthorizationService;
        _logger = logger;
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(DocumentArtifactResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GenerateDocument(
        [FromRoute] string projectId,
        [FromBody] GenerateDocumentRequestDto request,
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

        var authz = await _requestAuthorizationService.AuthorizeAsync(
            HttpContext,
            forbiddenCode: "FORBIDDEN",
            forbiddenMessage: "The caller is not authorized to generate documents for this project.",
            requiredPermission: "documents.generate",
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

        try
        {
            var result = await _documentGenerationService.GenerateDocumentAsync(
                tenantId, projectGuid, authenticatedUser.UserId, correlationId, idempotencyKey, request, cancellationToken);

            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation failed for document generation. CorrelationId={CorrelationId}", correlationId);
            return BadRequest(new StandardErrorDto
            {
                Status = StatusCodes.Status400BadRequest,
                TraceId = traceId,
                Code = "INVALID_REQUEST",
                Message = ex.Message
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new StandardErrorDto
            {
                Status = StatusCodes.Status404NotFound,
                TraceId = traceId,
                Code = "NOT_FOUND",
                Message = ex.Message
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
            _logger.LogError(ex, "Unexpected error during document generation. CorrelationId={CorrelationId}", correlationId);
            return StatusCode(StatusCodes.Status500InternalServerError, new StandardErrorDto
            {
                Status = StatusCodes.Status500InternalServerError,
                TraceId = traceId,
                Code = "INTERNAL_SERVER_ERROR",
                Message = "An unexpected error occurred while generating the document."
            });
        }
    }
}
