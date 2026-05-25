namespace PatientJournalSystem.DTOs;

public record LoginRequest(string Email, string Password);

public record LoginResponse(string Token, string Role, string FullName);

public record RegisterRequest(
    string FullName,
    string Email,
    string Password,
    string Role  // "Patient", "Doctor", "Secretary", "Admin"
);
