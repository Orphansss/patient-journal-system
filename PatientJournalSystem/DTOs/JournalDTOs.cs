namespace PatientJournalSystem.DTOs;

public record CreateJournalRequest(
    int PatientId,
    string Diagnosis,
    string Notes,
    string Medication
);

public record UpdateJournalRequest(
    string? Diagnosis,
    string? Notes,
    string? Medication
);

public record JournalResponse(
    int Id,
    int PatientId,
    string PatientName,
    int DoctorId,
    string DoctorName,
    string Diagnosis,
    string Notes,
    string Medication,
    bool IsArchived,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
