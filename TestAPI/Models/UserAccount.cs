using System.Text.Json.Serialization;

namespace TestAPI.Models;

public class UserAccount
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "Student";
    public bool IsActive { get; set; } = true;
    [JsonIgnore]
    public string PasswordHash { get; set; } = "";
}
