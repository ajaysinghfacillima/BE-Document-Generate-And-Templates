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
[Route("api/v1/admin/branding/assets")]
public sealed class BrandingAssetsController : ControllerBase
{
    private readonly IBrandingAssetService _brandingAssetService;
    private readonly IRequestAuthorizationService _requestAuthorizationService;
    private readonly ILogger<BrandingAssetsController> _logger;

    public BrandingAssetsController(
        IBrandingAssetService brandingAssetService,
        IRequestAuthorizationService requestAuthorizationService,
        ILogger<BrandingAssetsController> logger)
    {
        _brandingAssetService = brandingAssetService;
        _requestAuthorizationService = requestAuthorizationService;
        _logger = logger;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(BrandingAssetResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadBrandingAssets(
        IFormFile? logoFile,
        [FromForm] string? colorsJson,
        IFormFile? fontsZip,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var correlationId = RequestPolicyUtilities.GetCorrelationId(HttpContext);

        var authz = await _requestAuthorizationService.AuthorizeAsync(
            HttpContext,
            forbiddenCode: "FORBIDDEN",
            forbiddenMessage: "The caller is not authorized to manage branding assets.",
            requiredRole: "Admin",
            requiredPermission: "branding.settings.write",
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

        var input = new BrandingUploadInput();

        if (logoFile != null && logoFile.Length > 0)
        {
            using var ms = new MemoryStream();
            await logoFile.CopyToAsync(ms, cancellationToken);
            input.LogoData = ms.ToArray();
            input.LogoFileName = logoFile.FileName;
            input.LogoContentType = logoFile.ContentType;
        }

        input.ColorsJson = colorsJson;

        if (fontsZip != null && fontsZip.Length > 0)
        {
            using var ms = new MemoryStream();
            await fontsZip.CopyToAsync(ms, cancellationToken);
            input.FontsZipData = ms.ToArray();
            input.FontsZipFileName = fontsZip.FileName;
        }

        try
        {
            var result = await _brandingAssetService.UploadBrandingAssetsAsync(
                tenantId, authenticatedUser.UserId, correlationId, idempotencyKey, input, cancellationToken);

            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation failed for branding asset upload. CorrelationId={CorrelationId}", correlationId);
            return BadRequest(new StandardErrorDto
            {
                Status = StatusCodes.Status400BadRequest,
                TraceId = traceId,
                Code = "INVALID_REQUEST",
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
        catch (InvalidOperationException ex) when (ex.Message.Contains("Malware"))
        {
            return BadRequest(new StandardErrorDto
            {
                Status = StatusCodes.Status400BadRequest,
                TraceId = traceId,
                Code = "MALWARE_DETECTED",
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during branding asset upload. CorrelationId={CorrelationId}", correlationId);
            return StatusCode(StatusCodes.Status500InternalServerError, new StandardErrorDto
            {
                Status = StatusCodes.Status500InternalServerError,
                TraceId = traceId,
                Code = "INTERNAL_SERVER_ERROR",
                Message = "An unexpected error occurred while processing the branding asset upload."
            });
        }
    }
}
