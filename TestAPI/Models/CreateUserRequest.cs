using System.ComponentModel.DataAnnotations;

namespace TestAPI.Models;

public class CreateUserRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = "";
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";
    [Required, MinLength(8), StringLength(128)]
    public string Password { get; set; } = "";
    [Required]
    public string Role { get; set; } = "Student";
}
