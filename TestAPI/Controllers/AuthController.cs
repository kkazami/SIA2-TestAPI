using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestAPI.Models;
using TestAPI.Services;

namespace TestAPI.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(AuthService service) : ControllerBase
{
    [AllowAnonymous, HttpPost("login")]
    public IActionResult Login(LoginRequest request) => Ok(service.Login(request));

    [Authorize, HttpPost("logout")]
    public IActionResult Logout()
    {
        service.Logout(Request.Headers.Authorization.ToString()[7..].Trim());
        return Ok(new { message = "Logout successful." });
    }
}
