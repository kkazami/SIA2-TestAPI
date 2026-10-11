using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using TestAPI.Models;
using TestAPI.Repositories;

namespace TestAPI.Services;

public class AuthService(BookingRepository repository)
{
    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static string HashPassword(UserAccount user, string password) =>
        new PasswordHasher<UserAccount>().HashPassword(user, password);

    public object Login(LoginRequest request) => repository.Write<object>(() =>
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = repository.Users.SingleOrDefault(u => u.Email == email && u.IsActive);
        if (user == null || new PasswordHasher<UserAccount>()
            .VerifyHashedPassword(user, user.PasswordHash, request.Password)
            == PasswordVerificationResult.Failed)
            throw new ApiException(401, "Invalid email or password.");

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var session = new LoginSession
        {
            UserId = user.Id,
            TokenHash = HashToken(token),
            ExpiresUtc = DateTime.UtcNow.AddHours(2)
        };
        repository.Add(session);
        return new { message = "Login successful.", token,
            expiresUtc = session.ExpiresUtc, user };
    });

    public UserAccount? GetUser(string token)
    {
        var hash = HashToken(token);
        var session = repository.Sessions.SingleOrDefault(s =>
            s.TokenHash == hash && s.ExpiresUtc > DateTime.UtcNow);
        return session == null ? null : repository.Users.SingleOrDefault(u =>
            u.Id == session.UserId && u.IsActive);
    }

    public void Logout(string token) => repository.Write(() =>
    {
        var hash = HashToken(token);
        var session = repository.Sessions.SingleOrDefault(s => s.TokenHash == hash);
        if (session != null) repository.Remove(session);
        return true;
    });
}
