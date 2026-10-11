using System.ComponentModel.DataAnnotations;

namespace TestAPI.Models;

public class BookAppointmentRequest
{
    [Range(1, int.MaxValue)]
    public int AvailabilityId { get; set; }
}
