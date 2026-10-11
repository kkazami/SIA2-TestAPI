using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using TestAPI.Models;
using TestAPI.Repositories;
using TestAPI.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer",
        Description = "Paste the login token."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme, Id = "Bearer"
            }
        }] = Array.Empty<string>()
    });
});
builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Booking")
        ?? $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "midterm-v2.db")}"));
builder.Services.AddScoped<BookingRepository>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddAuthentication("Session")
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(
        "Session", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Initializes a new database without deleting any existing database.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
    db.Database.EnsureCreated();
    if (app.Environment.IsDevelopment() && !db.Users.Any())
    {
        foreach (var role in new[] { "Admin", "Tutor", "Student" })
        {
            var user = new UserAccount
            {
                Name = $"Demo {role}",
                Email = $"{role.ToLowerInvariant()}@example.com",
                Role = role
            };
            user.PasswordHash = AuthService.HashPassword(user, "DemoPass123!");
            db.Users.Add(user);
        }
        db.SaveChanges();
    }
}
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (ApiException ex)
    {
        await Results.Problem(statusCode: ex.Status, title: ex.Message)
            .ExecuteAsync(context);
    }
    catch (DbUpdateException ex)
    {
        app.Logger.LogWarning(ex, "Database update rejected.");
        await Results.Problem(statusCode: 409,
            title: "Update conflicts with existing records.").ExecuteAsync(context);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Unexpected API error.");
        await Results.Problem(statusCode: 500,
            title: "An unexpected server error occurred.").ExecuteAsync(context);
    }
});
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
// Local HTTP avoids the redirect that affected your Postman token.
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
