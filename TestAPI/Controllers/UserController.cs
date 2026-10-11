using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestAPI.Models;
using TestAPI.Services;

namespace TestAPI.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/users")]
public class UserController(UserService service) : ControllerBase
{
    [HttpGet]
    public IActionResult GetUsers() => Ok(service.GetAll());

    [HttpPost]
    public IActionResult CreateUser(CreateUserRequest request)
    {
        var user = service.Create(request);
        return Created("/api/v1/users", user);
    }

    [HttpPatch("{id:int}")]
    public IActionResult UpdateUser(int id, UpdateUserRequest request) =>
        Ok(service.Update(id, request));

    [HttpDelete("{id:int}")]
    public IActionResult DeactivateUser(int id)
    {
        service.Deactivate(id);
        return Ok(new { message = "User deactivated." });
    }
}
