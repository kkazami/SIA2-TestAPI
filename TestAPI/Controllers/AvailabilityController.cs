using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestAPI.Models;
using TestAPI.Services;

namespace TestAPI.Controllers;

[ApiController, Authorize]
[Route("api/v1/availability")]
public class AvailabilityController(BookingService service) : AuthenticatedController
{
    [HttpGet]
    public IActionResult GetAvailability() => Ok(service.Available());

    [HttpPost, Authorize(Roles = "Tutor,Admin")]
    public IActionResult CreateAvailability(AvailabilityRequest request)
    {
        var slot = service.CreateSlot(UserId, UserRole, request);
        return Created("/api/v1/availability", slot);
    }

    [HttpPut("{id:int}"), Authorize(Roles = "Tutor,Admin")]
    public IActionResult UpdateAvailability(int id, AvailabilityRequest request) =>
        Ok(service.UpdateSlot(UserId, UserRole, id, request));

    [HttpDelete("{id:int}"), Authorize(Roles = "Tutor,Admin")]
    public IActionResult DeleteAvailability(int id)
    {
        service.DeleteSlot(UserId, UserRole, id);
        return Ok(new { message = "Slot removed." });
    }
}
