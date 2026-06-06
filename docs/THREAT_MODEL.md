# Trusselsmodel - Patient Journal System

## Systemet i kort

Et webbaseret patientjournalsystem til en lægeklinik. Sundhedsdata er en særlig kategori under GDPR (Artikel 9) og systemet er i scope for NIS2 (sundhedssektoren er direkte nævnt).

## Roller og adgang

| Rolle | Kan gøre |
|---|---|
| Patient | Se egne journaler, administrere samtykke |
| Læge | Se/oprette/opdatere journaler for egne patienter (med samtykke) |
| Sekretær | Administrative opgaver - ingen adgang til journalindhold |
| Admin | Drift, brugerhåndtering, audit log - ingen adgang til journalindhold |

## STRIDE-analyse

### S - Spoofing (Identitetsforfalskning)
- **Trussel:** En angriber udgiver sig for at være en legitim bruger
- **Modforanstaltning:** Keycloak account locking, JWT-tokens med signatur, rate limiting på login

### T - Tampering (Manipulation)
- **Trussel:** Manipulation af journaldata undervejs eller i databasen
- **Modforanstaltning:** AES-256 kryptering at-rest, HTTPS/TLS in-transit, EF Core parameteriserede queries (SQL injection)

### R - Repudiation (Afvisning)
- **Trussel:** En bruger benægter at have tilgået en journal
- **Modforanstaltning:** Audit log med timestamp, bruger-ID, IP-adresse og handling

### I - Information Disclosure (Informationslæk)
- **Trussel:** Uautoriseret adgang til journaldata
- **Modforanstaltning:** RBAC - kun ejere og autoriserede læger kan se journaler. Krypterede felter i databasen

### D - Denial of Service
- **Trussel:** Overbelastning af systemet
- **Modforanstaltning:** Rate limiting (tilføjes), input validation

### E - Elevation of Privilege (Rettighedsforøgelse)
- **Trussel:** En patient forsøger at tilgå en anden patients data
- **Modforanstaltning:** Rollebaseret adgangskontrol + consent-tjek i hvert endpoint

## OWASP Top 10 tjekliste

| # | Risiko | Status | Implementering |
|---|---|---|---|
| A01 | Broken Access Control | Implementeret | RBAC + consent-tjek i JournalController |
| A02 | Cryptographic Failures | Implementeret | AES-256 at-rest, HTTPS in-transit |
| A03 | Injection | Implementeret | EF Core parameteriserede queries |
| A04 | Insecure Design | Implementeret | Threat model, secure by design fra start |
| A05 | Security Misconfiguration | Delvist | Swagger kun i dev, secrets i env vars |
| A06 | Vulnerable Components | Tjekkes | `dotnet list package --vulnerable` |
| A07 | Auth/Session Failures | Implementeret | Keycloak med OIDC, account locking mod brute force |
| A08 | Software/Data Integrity | Delvist | Pakker via NuGet (signerede) |
| A09 | Logging/Monitoring Failures | Implementeret | AuditLog tabel, NIS2-krav |
| A10 | SSRF | N/A | Ingen udgående HTTP-kald |
