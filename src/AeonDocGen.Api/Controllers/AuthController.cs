using System.Diagnostics;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AeonDocGen.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RefreshTokenResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(StandardErrorDto), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto? request, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        if (request == null || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            _logger.LogWarning("Auth refresh request validation failed. TraceId={TraceId}", traceId);
            return BadRequest(new StandardErrorDto
            {
                Status = StatusCodes.Status400BadRequest,
                TraceId = traceId,
                Code = "INVALID_REQUEST_BODY",
                Message = "refreshToken is required."
            });
        }

        try
        {
            var response = await _authService.RefreshTokenAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new StandardErrorDto
            {
                Status = StatusCodes.Status401Unauthorized,
                TraceId = HttpContext.TraceIdentifier,
                Code = "UNAUTHENTICATED",
                Message = ex.Message
            });
        }
    }
}
