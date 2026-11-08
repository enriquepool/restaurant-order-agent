using System.Security.Cryptography;
using System.Text;

namespace OrderAgent.Infrastructure.WhatsApp;

/// <summary>
/// Valida el encabezado X-Hub-Signature-256 que Meta agrega a cada webhook:
/// HMAC-SHA256 del cuerpo crudo usando el App Secret. Evita que terceros envíen mensajes falsos.
/// </summary>
public static class WhatsAppSignatureValidator
{
    private const string Prefix = "sha256=";

    public static bool IsValid(ReadOnlySpan<byte> body, string? signatureHeader, string appSecret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrEmpty(appSecret))
            return false;
        if (!signatureHeader.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        byte[] received;
        try
        {
            received = Convert.FromHexString(signatureHeader.AsSpan(Prefix.Length));
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), body);
        return CryptographicOperations.FixedTimeEquals(expected, received);
    }

    public static string Sign(ReadOnlySpan<byte> body, string appSecret) =>
        Prefix + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), body));
}
