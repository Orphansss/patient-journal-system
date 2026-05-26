using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatientJournalSystem.Data;
using PatientJournalSystem.DTOs;
using PatientJournalSystem.Models;
using PatientJournalSystem.Services;

namespace PatientJournalSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Tags("Journals")]
public class JournalController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly EncryptionService _encryption;
    private readonly AuditService _audit;
    private readonly CurrentUserService _currentUser;

    public JournalController(
    AppDbContext db,
    EncryptionService encryption,
    AuditService audit,
    CurrentUserService currentUser)
    {
        _db = db;
        _encryption = encryption;
        _audit = audit;
        _currentUser = currentUser;
    }

    private async Task<int> GetCurrentUserId() =>
    await _currentUser.GetRequiredUserIdAsync(User);

    private string GetCurrentRole() =>
        User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    /// <summary>Get journals accessible to the current user</summary>
    /// <remarks>
    /// - Patients see only their own journals
    /// - Doctors see journals where they are the assigned doctor AND consent is granted
    /// - Admins and secretaries see no journal content (only metadata in a real system)
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(List<JournalResponse>), 200)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetJournals()
    {
        var userId = await GetCurrentUserId();
        var role = GetCurrentRole();

        if (role == "Secretary" || role == "Admin")
        {
            await _audit.LogAsync(userId, "LIST_JOURNALS_DENIED", "Journals", false);
            return Forbid(); // Secretaries and admins do not read patient data
        }

        IQueryable<Journal> query = _db.Journals
            .Include(j => j.Patient)
            .Include(j => j.Doctor)
            .Where(j => !j.IsArchived);

        if (role == "Patient")
            query = query.Where(j => j.PatientId == userId);
        else if (role == "Doctor")
        {
            // Doctor must have active consent from patient
            var consentedPatientIds = await _db.Consents
                .Where(c => c.DoctorId == userId && c.IsGranted && c.RevokedAt == null)
                .Select(c => c.PatientId)
                .ToListAsync();

            query = query.Where(j => j.DoctorId == userId && consentedPatientIds.Contains(j.PatientId));
        }

        var journals = await query.ToListAsync();
        await _audit.LogAsync(userId, "LIST_JOURNALS", "Journals", true);

        return Ok(journals.Select(j => MapToResponse(j)));
    }

    /// <summary>Get a specific journal by ID</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(JournalResponse), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetJournal(int id)
    {
        var userId = await GetCurrentUserId();
        var role = GetCurrentRole();

        var journal = await _db.Journals
            .Include(j => j.Patient)
            .Include(j => j.Doctor)
            .FirstOrDefaultAsync(j => j.Id == id);

        if (journal == null)
            return NotFound();

        // Access control: patient only sees own journal, doctor needs consent
        var canAccess = role switch
        {
            "Patient" => journal.PatientId == userId,
            "Doctor" => journal.DoctorId == userId && await HasConsent(userId, journal.PatientId),
            _ => false
        };

        if (!canAccess)
        {
            await _audit.LogAsync(userId, "READ_JOURNAL_DENIED", $"Journal#{id}", false);
            return Forbid();
        }

        await _audit.LogAsync(userId, "READ_JOURNAL", $"Journal#{id}", true);
        return Ok(MapToResponse(journal));
    }

    /// <summary>Create a new journal entry (Doctors only)</summary>
    [HttpPost]
    [Authorize(Roles = "Doctor")]
    [ProducesResponseType(typeof(JournalResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> CreateJournal([FromBody] CreateJournalRequest request)
    {
        var doctorId = await GetCurrentUserId();

        if (!await HasConsent(doctorId, request.PatientId))
            return Forbid(); // No consent = no access

        var patient = await _db.Users.FindAsync(request.PatientId);
        if (patient == null || patient.Role != UserRole.Patient)
            return BadRequest(new { message = "Patient not found." });

        var journal = new Journal
        {
            PatientId = request.PatientId,
            DoctorId = doctorId,
            EncryptedDiagnosis = _encryption.Encrypt(request.Diagnosis),
            EncryptedNotes = _encryption.Encrypt(request.Notes),
            EncryptedMedication = _encryption.Encrypt(request.Medication)
        };

        _db.Journals.Add(journal);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(doctorId, "CREATE_JOURNAL", $"Journal#{journal.Id}", true);

        await _db.Entry(journal).Reference(j => j.Patient).LoadAsync();
        await _db.Entry(journal).Reference(j => j.Doctor).LoadAsync();

        return CreatedAtAction(nameof(GetJournal), new { id = journal.Id }, MapToResponse(journal));
    }

    /// <summary>Update a journal entry (Doctors only, with consent)</summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Doctor")]
    [ProducesResponseType(typeof(JournalResponse), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateJournal(int id, [FromBody] UpdateJournalRequest request)
    {
        var doctorId = await GetCurrentUserId();

        var journal = await _db.Journals
            .Include(j => j.Patient)
            .Include(j => j.Doctor)
            .FirstOrDefaultAsync(j => j.Id == id);

        if (journal == null) return NotFound();
        if (journal.DoctorId != doctorId) return Forbid();
        if (!await HasConsent(doctorId, journal.PatientId)) return Forbid();

        if (request.Diagnosis != null)
            journal.EncryptedDiagnosis = _encryption.Encrypt(request.Diagnosis);
        if (request.Notes != null)
            journal.EncryptedNotes = _encryption.Encrypt(request.Notes);
        if (request.Medication != null)
            journal.EncryptedMedication = _encryption.Encrypt(request.Medication);

        journal.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(doctorId, "UPDATE_JOURNAL", $"Journal#{id}", true);

        return Ok(MapToResponse(journal));
    }

    /// <summary>Archive a journal (Doctors only)</summary>
    [HttpPost("{id}/archive")]
    [Authorize(Roles = "Doctor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ArchiveJournal(int id)
    {
        var doctorId = await GetCurrentUserId();
        var journal = await _db.Journals.FindAsync(id);

        if (journal == null) return NotFound();
        if (journal.DoctorId != doctorId) return Forbid();

        journal.IsArchived = true;
        journal.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(doctorId, "ARCHIVE_JOURNAL", $"Journal#{id}", true);

        return NoContent();
    }

    private async Task<bool> HasConsent(int doctorId, int patientId) =>
        await _db.Consents.AnyAsync(c =>
            c.DoctorId == doctorId &&
            c.PatientId == patientId &&
            c.IsGranted &&
            c.RevokedAt == null);

    private JournalResponse MapToResponse(Journal j) => new(
        j.Id,
        j.PatientId,
        j.Patient.FullName,
        j.DoctorId,
        j.Doctor.FullName,
        _encryption.Decrypt(j.EncryptedDiagnosis),
        _encryption.Decrypt(j.EncryptedNotes),
        _encryption.Decrypt(j.EncryptedMedication),
        j.IsArchived,
        j.CreatedAt,
        j.UpdatedAt
    );
}
