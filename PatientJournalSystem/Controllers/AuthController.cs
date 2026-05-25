using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatientJournalSystem.Data;
using PatientJournalSystem.DTOs;
using PatientJournalSystem.Models;
using PatientJournalSystem.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace PatientJournalSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Tags("Authentication")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuthService _authService;

    public AuthController(AppDbContext db, AuthService authService)
    {
        _db = db;
        _authService = authService;
    }

    /// <summary>Login and receive a JWT token</summary>
    /// <remarks>
    /// Seeded test accounts:
    /// - doctor@klinik.dk / Doctor123!
    /// - patient@example.dk / Patient123!
    /// - secretary@klinik.dk / Secretary123!
    /// - admin@klinik.dk / Admin123!
    /// </remarks>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request.Email, request.Password);

        if (!result.IsSuccess)
            return Unauthorized(new { message = "Invalid email or password." });

        return Ok(result.Response);
    }

    /// <summary>Register a new user (Admin only in production - open for demo)</summary>
    [HttpPost("register")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return BadRequest(new { message = "Email already in use." });

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
            return BadRequest(new { message = "Invalid role. Use: Patient, Doctor, Secretary, Admin." });

        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Login), new { message = "User created.", userId = user.Id });
    }
}
