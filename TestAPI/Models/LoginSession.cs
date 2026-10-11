namespace TestAPI.Models;

public class LoginSession
{
    public int Id { get; set; }
    public string TokenHash { get; set; } = "";
    public int UserId { get; set; }
    public DateTime ExpiresUtc { get; set; }
}
