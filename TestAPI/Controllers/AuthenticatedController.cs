using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace TestAPI.Controllers;

public abstract class AuthenticatedController : ControllerBase
{
    protected int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    protected string UserRole => User.FindFirstValue(ClaimTypes.Role)!;
}
