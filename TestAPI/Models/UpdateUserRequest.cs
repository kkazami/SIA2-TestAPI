using System.ComponentModel.DataAnnotations;

namespace TestAPI.Models;

public class UpdateUserRequest
{
    [StringLength(100, MinimumLength = 1)]
    public string? Name { get; set; }
    [EmailAddress, StringLength(254, MinimumLength = 1)]
    public string? Email { get; set; }
    [MinLength(8), StringLength(128)]
    public string? Password { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}
