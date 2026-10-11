namespace TestAPI.Models;

public class Appointment
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int AvailabilityId { get; set; }
    public string Status { get; set; } = "Booked";
}
