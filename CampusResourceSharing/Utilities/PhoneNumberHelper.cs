using System.Text.RegularExpressions;

namespace CampusResourceSharing.Utilities
{
    public static class PhoneNumberHelper
    {
        /// <summary>
        /// Validates and normalizes an input mobile number into a canonical 10-digit format.
        /// Handles standard Indian formats such as 9876543210, +91 9876543210, +919876543210, 09876543210, 919876543210.
        /// Strips whitespace, hyphens, dots, and parentheses.
        /// </summary>
        /// <param name="input">The raw mobile number string.</param>
        /// <param name="normalized">The canonical 10-digit mobile number if successful.</param>
        /// <returns>True if the mobile number is valid and normalized; otherwise false.</returns>
        public static bool TryNormalize(string? input, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var raw = input.Trim();
            // Remove whitespace, dashes, dots, parentheses
            var cleaned = Regex.Replace(raw, @"[\s\-\.\(\)]+", "");

            // Handle country code +91 or +
            if (cleaned.StartsWith("+91"))
            {
                cleaned = cleaned.Substring(3);
            }
            else if (cleaned.StartsWith("+"))
            {
                cleaned = cleaned.Substring(1);
            }

            // Handle 12 digits starting with 91 (e.g., 919876543210)
            if (cleaned.Length == 12 && cleaned.StartsWith("91"))
            {
                cleaned = cleaned.Substring(2);
            }
            // Handle 11 digits starting with trunk prefix 0 (e.g., 09876543210)
            else if (cleaned.Length == 11 && cleaned.StartsWith("0"))
            {
                cleaned = cleaned.Substring(1);
            }

            // Canonical representation: exactly 10 digits
            if (Regex.IsMatch(cleaned, @"^[0-9]{10}$"))
            {
                normalized = cleaned;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the normalized 10-digit number if valid; otherwise returns the trimmed raw string.
        /// </summary>
        public static string Normalize(string? input)
        {
            if (TryNormalize(input, out var normalized))
            {
                return normalized;
            }
            return input?.Trim() ?? string.Empty;
        }
    }
}
