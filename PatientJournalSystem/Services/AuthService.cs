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
}

public record AuthResult(bool IsSuccess, LoginResponse? Response)
{
    public static AuthResult Success(LoginResponse response) => new(true, response);

    public static AuthResult Fail() => new(false, null);
}
