using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PatientJournalSystem.Data;
using PatientJournalSystem.Models;

namespace PatientJournalSystem.Services;

public class CurrentUserService
{
    private static readonly HashSet<string> ApplicationRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(UserRole.Patient),
        nameof(UserRole.Doctor),
        nameof(UserRole.Secretary),
        nameof(UserRole.Admin)
    };

    private readonly AppDbContext _db;

    public CurrentUserService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<User> GetRequiredUserAsync(ClaimsPrincipal principal)
    {
        var email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email")
            ?? principal.FindFirstValue("preferred_username");

        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Keycloak-tokenet mangler en email-claim.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            throw new InvalidOperationException(
                $"Ingen lokal databasebruger matcher Keycloak-emailen '{email}'. " +
                "Opret brugeren i både Keycloak og den lokale database, eller brug de seedede testbrugere.");
        }

        return user;
    }

    public async Task<int> GetRequiredUserIdAsync(ClaimsPrincipal principal) =>
        (await GetRequiredUserAsync(principal)).Id;

    public string GetCurrentApplicationRole(ClaimsPrincipal principal) =>
        principal.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "role" || c.Type == "roles")
            .Select(c => c.Value)
            .FirstOrDefault(ApplicationRoles.Contains) ?? string.Empty;
}