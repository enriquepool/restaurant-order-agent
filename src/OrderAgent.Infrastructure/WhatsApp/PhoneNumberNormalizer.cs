namespace OrderAgent.Infrastructure.WhatsApp;

public static class PhoneNumberNormalizer
{
    /// <summary>
    /// Los números de celular de México llegan en el webhook como 521 + 10 dígitos, pero la API
    /// los espera como 52 + 10 dígitos (así aparecen en la lista de destinatarios permitidos).
    /// Sin este ajuste Meta responde con el error 131030 "Recipient phone number not in allowed list".
    /// </summary>
    public static string ForSending(string waId)
    {
        var digits = new string(waId.Where(char.IsAsciiDigit).ToArray());

        if (digits.Length == 13 && digits.StartsWith("521", StringComparison.Ordinal))
            return "52" + digits[3..];

        return digits;
    }
}
