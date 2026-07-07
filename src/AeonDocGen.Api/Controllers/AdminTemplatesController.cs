// TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
using System.Diagnostics;
using AeonDocGen.Api.Policies;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AeonDocGen.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/admin/templates")]
public sealed class AdminTemplatesController : ControllerBase
{
    private readonly IAdminTemplateService _adminTemplateService;
    private readonly IRequestAuthorizationService _requestAuthorizationService;
    private readonly ILogger<AdminTemplatesController> _logger;

    public AdminTemplatesController(
        IAdminTemplateService adminTemplateService,
        IRequestAuthorizationService requestAuthorizationService,
        ILogger<AdminTemplatesController> logger)
    {
        _adminTemplateService = adminTemplateService;
        _requestAuthorizationService = requestAuthorizationService;
        _logger = logger;
    }

    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(AdminTemplateListResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ListTemplates(CancellationToken cancellationToken = default)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var correlationId = RequestPolicyUtilities.GetCorrelationId(HttpContext);

        var authz = await _requestAuthorizationService.AuthorizeAsync(
            HttpContext,
            forbiddenCode: "FORBIDDEN",
            forbiddenMessage: "The caller is not authorized to access this resource.",
            requiredRole: "Admin",
            cancellationToken: cancellationToken);
        if (!authz.IsAuthorized)
        {
            return authz.ErrorResult!;
        }

        var tenantId = authz.TenantId;
        var authenticatedUser = authz.User!;

        try
        {
            var result = await _adminTemplateService.ListTemplatesAsync(
                tenantId,
                authenticatedUser.UserId,
                correlationId,
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
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
            _logger.LogWarning(ex, "Template listing not found. CorrelationId={CorrelationId}", correlationId);
            return NotFound(new StandardErrorDto
            {
                Status = StatusCodes.Status404NotFound,
                TraceId = traceId,
                Code = "NOT_FOUND",
                Message = "The requested resource was not found."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error retrieving templates. TenantId={TenantId}, ActorUserId={ActorUserId}, CorrelationId={CorrelationId}, TraceId={TraceId}",
                tenantId, authenticatedUser.UserId, correlationId, traceId);

            return StatusCode(StatusCodes.Status500InternalServerError, new StandardErrorDto
            {
                Status = StatusCodes.Status500InternalServerError,
                TraceId = traceId,
                Code = "INTERNAL_SERVER_ERROR",
                Message = "An unexpected error occurred while retrieving template metadata."
            });
        }
    }
}
