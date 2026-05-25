using PatientJournalSystem.Models;
using PatientJournalSystem.Services;

namespace PatientJournalSystem.Data;

public static class DatabaseSeeder
{
    public static void Seed(AppDbContext context, EncryptionService encryption)
    {
        if (context.Users.Any()) return;

        var doctor = new User
        {
            FullName = "Dr. Anders Nielsen",
            Email = "doctor@klinik.dk",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Doctor123!"),
            Role = UserRole.Doctor
        };
        var patient = new User
        {
            FullName = "Peter Larsen",
            Email = "patient@example.dk",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Patient123!"),
            Role = UserRole.Patient
        };
        var secretary = new User
        {
            FullName = "Mette Sørensen",
            Email = "secretary@klinik.dk",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Secretary123!"),
            Role = UserRole.Secretary
        };
        var admin = new User
        {
            FullName = "System Admin",
            Email = "admin@klinik.dk",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            Role = UserRole.Admin
        };

        context.Users.AddRange(doctor, patient, secretary, admin);
        context.SaveChanges();

        // Consent: patient grants doctor access
        context.Consents.Add(new DoctorPatientConsent
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            IsGranted = true
        });

        // Seed a journal with encrypted data
        context.Journals.Add(new Journal
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            EncryptedDiagnosis = encryption.Encrypt("Type 2 Diabetes - mild"),
            EncryptedNotes = encryption.Encrypt("Patient referred for dietary counseling. Follow-up in 3 months."),
            EncryptedMedication = encryption.Encrypt("Metformin 500mg twice daily")
        });

        context.SaveChanges();
    }
}
