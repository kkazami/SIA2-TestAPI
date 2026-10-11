using System.ComponentModel.DataAnnotations;

namespace TestAPI.Models;

public class UpdateAppointmentRequest
{
    [Range(1, int.MaxValue)]
    public int? AvailabilityId { get; set; }
    public string? Status { get; set; }
}
