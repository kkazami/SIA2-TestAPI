using System.ComponentModel.DataAnnotations;

namespace TestAPI.Models;

public class AvailabilityRequest
{
    [Range(1, int.MaxValue)]
    public int TutorId { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
}
