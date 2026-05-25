using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatientJournalSystem.Data;
using PatientJournalSystem.Models;
using PatientJournalSystem.Services;

namespace PatientJournalSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Tags("Consent (GDPR Art. 9)")]
public class ConsentController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public ConsentController(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
            ?? "0");

    /// <summary>Patient grants a doctor access to their journals (GDPR explicit consent)</summary>
    [HttpPost("grant/{doctorId}")]
    [Authorize(Roles = "Patient")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GrantConsent(int doctorId)
    {
        var patientId = GetCurrentUserId();

        var doctor = await _db.Users.FindAsync(doctorId);
        if (doctor == null || doctor.Role != UserRole.Doctor)
            return BadRequest(new { message = "Doctor not found." });

        var existing = await _db.Consents
            .FirstOrDefaultAsync(c => c.PatientId == patientId && c.DoctorId == doctorId);

        if (existing != null)
        {
            existing.IsGranted = true;
            existing.RevokedAt = null;
            existing.GrantedAt = DateTime.UtcNow;
        }
        else
        {
            _db.Consents.Add(new DoctorPatientConsent
            {
                PatientId = patientId,
                DoctorId = doctorId,
                IsGranted = true
            });
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync(patientId, "CONSENT_GRANTED", $"Doctor#{doctorId}", true);

        return Ok(new { message = $"Consent granted to {doctor.FullName}." });
    }

    /// <summary>Patient revokes a doctor's access (GDPR right to withdraw consent)</summary>
    [HttpPost("revoke/{doctorId}")]
    [Authorize(Roles = "Patient")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RevokeConsent(int doctorId)
    {
        var patientId = GetCurrentUserId();

        var consent = await _db.Consents
            .FirstOrDefaultAsync(c => c.PatientId == patientId && c.DoctorId == doctorId && c.IsGranted);

        if (consent == null)
            return NotFound(new { message = "No active consent found." });

        consent.IsGranted = false;
        consent.RevokedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _audit.LogAsync(patientId, "CONSENT_REVOKED", $"Doctor#{doctorId}", true);

        return Ok(new { message = "Consent revoked." });
    }

    /// <summary>View all consents for the current patient</summary>
    [HttpGet("my")]
    [Authorize(Roles = "Patient")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetMyConsents()
    {
        var patientId = GetCurrentUserId();

        var consents = await _db.Consents
            .Include(c => c.Doctor)
            .Where(c => c.PatientId == patientId)
            .Select(c => new
            {
                c.Id,
                DoctorId = c.DoctorId,
                DoctorName = c.Doctor.FullName,
                c.IsGranted,
                c.GrantedAt,
                c.RevokedAt
            })
            .ToListAsync();

        return Ok(consents);
    }
}
