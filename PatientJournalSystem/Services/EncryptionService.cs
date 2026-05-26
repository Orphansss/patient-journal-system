using System.Security.Cryptography;
using System.Text;

namespace PatientJournalSystem.Services;

/// <summary>
/// AES-256-GCM encryption for journal data at rest.
/// GCM giver både fortrolighed OG integritet via et auth tag -
/// hvis data manipuleres direkte i databasen, fejler dekryptering.
/// Nøglen læses fra User Secrets eller miljøvariabel - aldrig hardkodet.
/// </summary>
public class EncryptionService
{
    // AES-GCM kræver præcis 12 bytes nonce (96 bit) - det er standarden for GCM
    private const int NonceSize = 12;

    // Auth tag er 16 bytes (128 bit) - maksimal størrelse, giver stærkest integritetsbeskyttelse
    private const int TagSize = 16;

    private readonly byte[] _key;

    public EncryptionService(IConfiguration config)
    {
        var base64Key = config["Encryption:Key"]
            ?? throw new InvalidOperationException(
                "Encryption:Key er ikke konfigureret. " +
                "Kør: dotnet user-secrets set \"Encryption:Key\" \"<base64-32-bytes>\"");

        _key = Convert.FromBase64String(base64Key);

        // AES-256 kræver præcis 32 bytes - validér ved opstart så fejlen er tydelig
        if (_key.Length != 32)
            throw new InvalidOperationException(
                $"Encryption:Key skal være præcis 32 bytes (256 bit). Fik {_key.Length} bytes.");
    }

    public string Encrypt(string plaintext)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

        // Kryptografisk sikker tilfældig nonce via CSPRNG - aldrig den usikre Random-klasse.
        // Genbrug af nonce med samme nøgle er katastrofalt i GCM og
        // kompromitterer både fortrolighed og integritet
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Gem som nonce:tag:ciphertext - alle tre dele skal bruges ved dekryptering
        return $"{Convert.ToBase64String(nonce)}:" +
               $"{Convert.ToBase64String(tag)}:" +
               $"{Convert.ToBase64String(ciphertext)}";
    }

    public string Decrypt(string encryptedData)
    {
        var parts = encryptedData.Split(':');

        // GCM-formatet har tre dele - CBC havde to (IV + ciphertext)
        if (parts.Length != 3)
            throw new FormatException(
                "Ugyldigt krypteringsformat. Forventer nonce:tag:ciphertext.");

        var nonce = Convert.FromBase64String(parts[0]);
        var tag = Convert.FromBase64String(parts[1]);
        var ciphertext = Convert.FromBase64String(parts[2]);

        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(_key, TagSize);

        // Hvis tag ikke matcher, er data manipuleret - AesGcm kaster AuthenticationTagMismatchException
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}