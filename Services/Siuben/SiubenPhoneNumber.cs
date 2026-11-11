using System.Text.RegularExpressions;

namespace Exatech_Indotel_API.Services.Siuben;

public static class SiubenPhoneNumber
{
    public static string Normalize(string? mobile, string? phone)
    {
        var value = string.IsNullOrWhiteSpace(mobile) ? phone ?? string.Empty : mobile;
        var digits = Regex.Replace(value, @"[\s()+-]", string.Empty);
        return digits.Length == 11 && digits.StartsWith('1') ? digits[1..] : digits;
    }

    public static bool IsValid(string? mobile, string? phone) =>
        Regex.IsMatch(Normalize(mobile, phone), "^[0-9]{10}$");
}
