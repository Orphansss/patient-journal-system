namespace PatientJournalSystem.Models;

public class Journal
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int DoctorId { get; set; }

    // Stored encrypted with AES-256
    public string EncryptedDiagnosis { get; set; } = string.Empty;
    public string EncryptedNotes { get; set; } = string.Empty;
    public string EncryptedMedication { get; set; } = string.Empty;

    public bool IsArchived { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User Patient { get; set; } = null!;
    public User Doctor { get; set; } = null!;
}
