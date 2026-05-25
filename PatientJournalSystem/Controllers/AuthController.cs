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
    private readonly TokenService _tokenService;

    public AuthController(AppDbContext db, TokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
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
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        var token = _tokenService.GenerateToken(user);
        return Ok(new LoginResponse(token, user.Role.ToString(), user.FullName));
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
