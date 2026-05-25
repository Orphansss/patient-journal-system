namespace PatientJournalSystem.Models;

public class AuditLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Action { get; set; } = string.Empty;   // e.g. "READ_JOURNAL", "UPDATE_JOURNAL"
    public string Resource { get; set; } = string.Empty;  // e.g. "Journal#42"
    public string IpAddress { get; set; } = string.Empty;
    public bool WasSuccessful { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
