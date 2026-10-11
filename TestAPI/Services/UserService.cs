using TestAPI.Models;
using TestAPI.Repositories;

namespace TestAPI.Services;

public class UserService(BookingRepository repository)
{
    private UserAccount Find(int id) =>
        repository.Users.SingleOrDefault(u => u.Id == id)
        ?? throw new ApiException(404, "User not found.");

    private static string ValidRole(string role) =>
        role is "Student" or "Tutor" or "Admin" ? role
        : throw new ApiException(400, "Role must be Student, Tutor or Admin.");

    private static string ValidName(string name) =>
        !string.IsNullOrWhiteSpace(name) ? name.Trim()
        : throw new ApiException(400, "Name is required.");

    private string UniqueEmail(string email, int excludingId = 0)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (repository.Users.Any(u => u.Email == normalized && u.Id != excludingId))
            throw new ApiException(409, "Email is already registered.");
        return normalized;
    }

    public List<UserAccount> GetAll() => repository.Users.OrderBy(u => u.Id).ToList();

    public UserAccount Create(CreateUserRequest request) => repository.Write(() =>
    {
        var user = new UserAccount
        {
            Name = ValidName(request.Name),
            Email = UniqueEmail(request.Email),
            Role = ValidRole(request.Role)
        };
        user.PasswordHash = AuthService.HashPassword(user, request.Password);
        repository.Add(user);
        return user;
    });

    private void ProtectLastAdmin(UserAccount user, string role, bool active)
    {
        if (user.IsActive && user.Role == "Admin" && (!active || role != "Admin")
            && !repository.Users.Any(u => u.Id != user.Id && u.IsActive && u.Role == "Admin"))
            throw new ApiException(409, "Keep at least one active admin.");
    }

    private void RevokeSessions(int userId)
    {
        foreach (var session in repository.Sessions.Where(s => s.UserId == userId).ToList())
            repository.Remove(session);
    }

    public UserAccount Update(int id, UpdateUserRequest request) => repository.Write(() =>
    {
        var user = Find(id);
        if (request.Name == null && request.Email == null && request.Password == null
            && request.Role == null && request.IsActive == null)
            throw new ApiException(400, "Provide at least one account field.");

        var role = request.Role == null ? user.Role : ValidRole(request.Role);
        var active = request.IsActive ?? user.IsActive;
        ProtectLastAdmin(user, role, active);

        if (role != user.Role && (repository.Slots.Any(s => s.TutorId == id)
            || repository.Appointments.Any(a => a.StudentId == id)))
            throw new ApiException(409, "Cannot change roles after booking history exists.");

        if (request.Name != null) user.Name = ValidName(request.Name);
        if (request.Email != null) user.Email = UniqueEmail(request.Email, id);
        if (request.Password != null)
            user.PasswordHash = AuthService.HashPassword(user, request.Password);
        if (request.Password != null || request.Email != null || role != user.Role || !active)
            RevokeSessions(id);
        user.Role = role;
        user.IsActive = active;
        return user;
    });

    public void Deactivate(int id) => repository.Write(() =>
    {
        var user = Find(id);
        ProtectLastAdmin(user, user.Role, false);
        user.IsActive = false;
        RevokeSessions(id);
        return true;
    });
}
