namespace PatientJournalSystem.Models;

// GDPR Article 9: Explicit consent for processing sensitive health data
public class DoctorPatientConsent
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public bool IsGranted { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }

    // Navigation
    public User Patient { get; set; } = null!;
    public User Doctor { get; set; } = null!;
}
