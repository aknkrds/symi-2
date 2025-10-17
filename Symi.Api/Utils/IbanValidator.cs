using System.Text;

namespace Symi.Api.Utils;

public static class IbanValidator
{
    // ISO 13616 IBAN check
    public static bool IsValid(string iban)
    {
        if (string.IsNullOrWhiteSpace(iban)) return false;
        var sanitized = new string(iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (sanitized.Length < 15 || sanitized.Length > 34) return false;
        if (!char.IsLetter(sanitized[0]) || !char.IsLetter(sanitized[1])) return false;
        if (!char.IsDigit(sanitized[2]) || !char.IsDigit(sanitized[3])) return false;

        // Move first 4 chars to end
        var rearranged = sanitized[4..] + sanitized[..4];
        // Convert letters to numbers A=10..Z=35
        var sb = new StringBuilder();
        foreach (var ch in rearranged)
        {
            if (char.IsLetter(ch)) sb.Append((int)ch - 55); // 'A' => 10
            else sb.Append(ch);
        }
        // Mod 97
        int chunkSize = 9;
        int remainder = 0;
        for (int i = 0; i < sb.Length; i += chunkSize)
        {
            var part = remainder.ToString() + sb.ToString(i, Math.Min(chunkSize, sb.Length - i));
            remainder = Mod97(part);
        }
        return remainder == 1;
    }

    private static int Mod97(string digits)
    {
        int res = 0;
        foreach (var ch in digits)
        {
            res = (res * 10 + (ch - '0')) % 97;
        }
        return res;
    }
}