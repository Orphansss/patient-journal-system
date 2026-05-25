using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatientJournalSystem.Data;

namespace PatientJournalSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
[Tags("Audit Log (NIS2)")]
public class AuditController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuditController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>View audit log - who accessed what and when (Admin only)</summary>
    /// <remarks>Required by NIS2 for traceability. Helps detect insider misuse.</remarks>
    [HttpGet]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetAuditLog([FromQuery] int? userId, [FromQuery] int page = 1)
    {
        var query = _db.AuditLogs
            .Include(a => a.User)
            .AsQueryable();

        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);

        var logs = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * 50)
            .Take(50)
            .Select(a => new
            {
                a.Id,
                a.UserId,
                UserName = a.User.FullName,
                a.Action,
                a.Resource,
                a.IpAddress,
                a.WasSuccessful,
                a.Timestamp
            })
            .ToListAsync();

        return Ok(logs);
    }
}
