using System.Security.Cryptography;
using System.Text;

namespace PatientJournalSystem.Services;

/// <summary>
/// AES-256-CBC encryption for journal data at rest.
/// The key is loaded from environment variable or appsettings - never hardcoded.
/// In production: use a proper secrets vault (Azure Key Vault, HashiCorp Vault, etc.)
/// </summary>
public class EncryptionService
{
    private readonly byte[] _key;

    public EncryptionService(IConfiguration config)
    {
        var base64Key = config["Encryption:Key"]
            ?? throw new InvalidOperationException(
                "Encryption:Key is not configured. Set it as an environment variable: " +
                "Encryption__Key=<base64-encoded-32-bytes>");

        _key = Convert.FromBase64String(base64Key);

        if (_key.Length != 32)
            throw new InvalidOperationException("Encryption key must be exactly 32 bytes (256 bits).");
    }

    public string Encrypt(string plaintext)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV(); // Fresh random IV for every encryption

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        // Store as IV:CipherText (both base64) so we can decrypt later
        return $"{Convert.ToBase64String(aes.IV)}:{Convert.ToBase64String(cipherBytes)}";
    }

    public string Decrypt(string ciphertext)
    {
        var parts = ciphertext.Split(':');
        if (parts.Length != 2)
            throw new FormatException("Invalid ciphertext format.");

        var iv = Convert.FromBase64String(parts[0]);
        var cipherBytes = Convert.FromBase64String(parts[1]);

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
