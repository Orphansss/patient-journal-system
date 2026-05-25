namespace PatientJournalSystem.Models;

public enum UserRole
{
    Patient,
    Doctor,
    Secretary,
    Admin
}

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Journal> JournalsAsPatient { get; set; } = new List<Journal>();
    public ICollection<Journal> JournalsAsDoctor { get; set; } = new List<Journal>();
    public ICollection<DoctorPatientConsent> ConsentsGiven { get; set; } = new List<DoctorPatientConsent>();
    public ICollection<DoctorPatientConsent> ConsentsReceived { get; set; } = new List<DoctorPatientConsent>();
}
