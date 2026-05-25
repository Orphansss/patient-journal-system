using Microsoft.EntityFrameworkCore;
using PatientJournalSystem.Data;
using PatientJournalSystem.DTOs;
using PatientJournalSystem.Models;

namespace PatientJournalSystem.Services;

public class AuthService
{
    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db;
    private readonly TokenService _tokenService;

    public AuthService(AppDbContext db, TokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return AuthResult.Fail();

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

        if (user?.LockedUntil > now)
            return AuthResult.Fail();

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            if (user != null)
            {
                user.FailedLoginAttempts++;

                if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
                    user.LockedUntil = now.Add(LockoutDuration);

                await _db.SaveChangesAsync();
            }

            return AuthResult.Fail();
        }

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        await _db.SaveChangesAsync();

        var token = _tokenService.GenerateToken(user);
        var response = new LoginResponse(token, user.Role.ToString(), user.FullName);

        return AuthResult.Success(response);
    }

    public async Task<RegisterResult> RegisterAsync(RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return RegisterResult.Fail("Email already in use.");

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
            return RegisterResult.Fail("Invalid role. Use: Patient, Doctor, Secretary, Admin.");

        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return RegisterResult.Success(user.Id);
    }
}

public record AuthResult(bool IsSuccess, LoginResponse? Response)
{
    public static AuthResult Success(LoginResponse response) => new(true, response);

    public static AuthResult Fail() => new(false, null);
}

public record RegisterResult(bool IsSuccess, int? UserId, string? ErrorMessage)
{
    public static RegisterResult Success(int userId) => new(true, userId, null);

    public static RegisterResult Fail(string errorMessage) => new(false, null, errorMessage);
}
