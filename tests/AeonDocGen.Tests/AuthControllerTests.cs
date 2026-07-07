using AeonDocGen.Api.Controllers;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace AeonDocGen.Tests;

public class AuthControllerTests
{
    [Fact]
    public async Task Refresh_NullBody_Returns400InvalidRequestBody()
    {
        var authService = new Mock<IAuthService>();
        var logger = new Mock<ILogger<AuthController>>();
        var controller = new AuthController(authService.Object, logger.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Refresh(null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(badRequest.Value);
        Assert.Equal("INVALID_REQUEST_BODY", error.Code);
        Assert.Equal(StatusCodes.Status400BadRequest, error.Status);
    }

    [Fact]
    public async Task Refresh_EmptyRefreshToken_Returns400InvalidRequestBody()
    {
        var authService = new Mock<IAuthService>();
        var logger = new Mock<ILogger<AuthController>>();
        var controller = new AuthController(authService.Object, logger.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Refresh(new RefreshTokenRequestDto { RefreshToken = " " }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(badRequest.Value);
        Assert.Equal("INVALID_REQUEST_BODY", error.Code);
    }

    [Fact]
    public async Task Refresh_InvalidRefreshToken_Returns401Unauthenticated()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(s => s.RefreshTokenAsync(It.IsAny<RefreshTokenRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Refresh token is invalid or expired."));

        var logger = new Mock<ILogger<AuthController>>();
        var controller = new AuthController(authService.Object, logger.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Refresh(new RefreshTokenRequestDto { RefreshToken = "bad" }, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(unauthorized.Value);
        Assert.Equal("UNAUTHENTICATED", error.Code);
    }
}
