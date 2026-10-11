using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestAPI.Models;
using TestAPI.Services;

namespace TestAPI.Controllers;

[ApiController, Authorize]
[Route("api/v1/appointments")]
public class AppointmentController(BookingService service) : AuthenticatedController
{
    [HttpGet]
    public IActionResult GetAppointments() => Ok(service.GetAll(UserId, UserRole));

    [HttpGet("{id:int}")]
    public IActionResult GetAppointmentById(int id) =>
        Ok(service.Get(UserId, UserRole, id));

    [HttpPost, Authorize(Roles = "Student")]
    public IActionResult CreateAppointment(BookAppointmentRequest request)
    {
        var appointment = service.Book(UserId, request);
        return CreatedAtAction(nameof(GetAppointmentById),
            new { id = appointment.Id }, appointment);
    }

    [HttpPatch("{id:int}")]
    public IActionResult UpdateAppointment(int id, UpdateAppointmentRequest request) =>
        Ok(service.Update(UserId, UserRole, id, request));
}
