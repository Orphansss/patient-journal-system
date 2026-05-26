using PatientJournalSystem.Models;
using PatientJournalSystem.Services;

namespace PatientJournalSystem.Data;

public static class DatabaseSeeder
{
    public static void Seed(AppDbContext context, EncryptionService encryption)
    {
        if (context.Users.Any()) return;

        // Ingen passwords her - credentials lever i Keycloak, ikke i vores database.
        // Disse brugere skal også eksistere i Keycloak med samme email og rolle.
        var doctor = new User
        {
            FullName = "Dr. Anders Nielsen",
            Email = "doctor@klinik.dk",
            Role = UserRole.Doctor
        };
        var patient = new User
        {
            FullName = "Peter Larsen",
            Email = "patient@example.dk",
            Role = UserRole.Patient
        };
        var secretary = new User
        {
            FullName = "Mette Sørensen",
            Email = "secretary@klinik.dk",
            Role = UserRole.Secretary
        };
        var admin = new User
        {
            FullName = "System Admin",
            Email = "admin@klinik.dk",
            Role = UserRole.Admin
        };

        context.Users.AddRange(doctor, patient, secretary, admin);
        context.SaveChanges();

        // Patient giver lægen aktivt samtykke - krav i GDPR artikel 9
        context.Consents.Add(new DoctorPatientConsent
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            IsGranted = true
        });

        // Seed-journal krypteres med AES-256-GCM - databasen indeholder aldrig klartekst
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