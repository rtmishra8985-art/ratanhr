using HRMS.API.Controllers.Authentication;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.Tests;

public sealed class AuthenticationControllerSecurityTests
{
    [Fact]
    public async Task Logout_DoesNotAcceptRefreshTokenFromRequestBody()
    {
        var auth = new Mock<IAuthService>();
        var controller = CreateController(auth.Object);

        var result = await controller.Logout();

        result.Should().BeOfType<OkObjectResult>();
        auth.Verify(service => service.LogoutAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Logout_RevokesOnlyRefreshTokenFromHttpOnlyCookie()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(service => service.LogoutAsync("cookie-refresh-token"))
            .ReturnsAsync(true);

        var controller = CreateController(auth.Object);
        controller.HttpContext.Request.Headers.Cookie =
            "hrms_refresh_token=cookie-refresh-token";

        await controller.Logout();

        auth.Verify(service => service.LogoutAsync("cookie-refresh-token"), Times.Once);
        auth.Verify(service => service.LogoutAsync(It.Is<string>(
            token => token != "cookie-refresh-token")), Times.Never);
    }

    private static AuthController CreateController(IAuthService auth)
    {
        var controller = new AuthController(auth)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }
}