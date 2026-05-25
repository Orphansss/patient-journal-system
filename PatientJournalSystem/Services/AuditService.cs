using PatientJournalSystem.Data;
using PatientJournalSystem.Models;

namespace PatientJournalSystem.Services;

/// <summary>
/// Audit log: records who did what, when, and from which IP.
/// Required by NIS2 for traceability of access to sensitive health data.
/// </summary>
public class AuditService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task LogAsync(int userId, string action, string resource, bool success)
    {
        var ip = _http.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            Resource = resource,
            IpAddress = ip,
            WasSuccessful = success,
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }
}
