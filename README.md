# Patient Journal System

Eksamensprojekt for faget **Secure Software Development**.

Et webbaseret patientjournalsystem bygget med sikkerhed og privacy som centrale designprincipper fra starten (Secure by Design).

## Emner dækket

| Emne | Implementering |
|---|---|
| Trusselsmodellering | STRIDE-analyse + DFD (se `/docs`) |
| Secure by Design | Privacy by design, least privilege, fail-safe defaults |
| Adgangskontrol (RBAC) | Roller: Læge, Patient, Sekretær, Admin |
| Kryptering at-rest | AES-256-CBC på alle journalfelter i databasen |
| Kryptering in-transit | HTTPS/TLS |
| Autentifikation | JWT (lokalt) - Keycloak/OpenID Connect klar |
| Consent-håndtering | GDPR Artikel 9 - patient godkender adgang |
| Audit Log | NIS2 - hvem så hvad og hvornår |
| OWASP Top 10 | Evalueret løbende (se `/docs`) |
| Lovgivning | GDPR, NIS2, CRA |

## Kom i gang

### Krav
- [.NET 8 SDK](https://dotnet.microsoft.com/download)

### Start applikationen

```bash
cd PatientJournalSystem
dotnet restore
dotnet run
```

Swagger UI åbner automatisk på: `http://localhost:5000`

### Test-brugere (seedet automatisk)

| Rolle | Email | Password |
|---|---|---|
| Læge | doctor@klinik.dk | Doctor123! |
| Patient | patient@example.dk | Patient123! |
| Sekretær | secretary@klinik.dk | Secretary123! |
| Admin | admin@klinik.dk | Admin123! |

### Workflow i Swagger UI

1. Kald `POST /api/auth/login` med en af test-brugerne
2. Kopiér `token` fra svaret
3. Klik **Authorize** øverst i Swagger UI
4. Indtast: `Bearer <dit token>`
5. Test endpoints - adgangskontrol håndhæves automatisk

## Nøglehåndtering (vigtigt!)

AES-krypteringsnøglen må **aldrig** ligge i kildekoden eller committes til Git.

**Lokalt (development):**
Nøglen i `appsettings.Development.json` er kun til lokal udvikling.

Generér en ny nøgle:
```bash
./generate-dev-key.sh
```

**Produktion:**
Sæt nøglen som environment variable:
```bash
export Encryption__Key="<base64-encoded-32-bytes>"
export Jwt__Key="<min-32-chars>"
dotnet run
```

I en rigtig produktion: brug Azure Key Vault, HashiCorp Vault eller lignende.

## Projektstruktur

```
PatientJournalSystem/
├── Controllers/
│   ├── AuthController.cs       # Login, register
│   ├── JournalController.cs    # CRUD med RBAC + kryptering
│   ├── ConsentController.cs    # GDPR samtykke-håndtering
│   └── AuditController.cs      # NIS2 audit log (Admin only)
├── Models/
│   ├── User.cs                 # Bruger med roller
│   ├── Journal.cs              # Journal med krypterede felter
│   ├── DoctorPatientConsent.cs # GDPR samtykke
│   └── AuditLog.cs             # Audit trail
├── Services/
│   ├── EncryptionService.cs    # AES-256-CBC kryptering
│   ├── TokenService.cs         # JWT udstedelse
│   └── AuditService.cs         # Audit logging
├── Data/
│   ├── AppDbContext.cs          # Entity Framework / SQLite
│   └── DatabaseSeeder.cs       # Test-data
└── DTOs/                        # Request/Response objekter
```

## Næste skridt

- [ ] Udskift lokal JWT med Keycloak (OpenID Connect)
- [ ] STRIDE-diagram i `/docs`
- [ ] DFD over systemet
- [ ] Pentest (OWASP ZAP)
- [ ] Supply chain: `dotnet list package --vulnerable`
