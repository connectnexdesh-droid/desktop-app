using System.Globalization;
using System.Security.Cryptography;

namespace QuickDock.Services;

public static class TotpService
{
    public static string GenerateFromSecret(string input, int digits = 6, int periodSeconds = 30)
    {
        if (digits is < 6 or > 8)
            throw new ArgumentOutOfRangeException(nameof(digits));

        if (periodSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(periodSeconds));

        var secret = ExtractSecret(input);
        var key = DecodeBase32(secret);
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / periodSeconds;

        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(counterBytes);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes);

        var offset = hash[^1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var modulus = (int)Math.Pow(10, digits);
        var code = binaryCode % modulus;
        return code.ToString(new string('0', digits), CultureInfo.InvariantCulture);
    }

    private static string ExtractSecret(string input)
    {
        var value = input.Trim();

        if (value.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
        {
            var queryStart = value.IndexOf('?');
            if (queryStart < 0)
                throw new FormatException("The otpauth URI does not contain a secret.");

            var query = value[(queryStart + 1)..];
            foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                if (pair.Length == 2 &&
                    pair[0].Equals("secret", StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(pair[1]);
                }
            }

            throw new FormatException("No secret parameter was found in the otpauth URI.");
        }

        return value;
    }

    private static byte[] DecodeBase32(string secret)
    {
        var normalized = new string(secret
            .Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '=')
            .Select(char.ToUpperInvariant)
            .ToArray());

        if (normalized.Length == 0)
            throw new FormatException("Clipboard is empty. Copy a Base32 2FA secret first.");

        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var character in normalized)
        {
            var value = alphabet.IndexOf(character);
            if (value < 0)
                throw new FormatException("Clipboard does not contain a valid Base32 2FA secret.");

            buffer = (buffer << 5) | value;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)((buffer >> bitsLeft) & 0xFF));
                buffer &= (1 << bitsLeft) - 1;
            }
        }

        if (output.Count < 10)
            throw new FormatException("The 2FA secret is too short to be valid.");

        return output.ToArray();
    }
}
