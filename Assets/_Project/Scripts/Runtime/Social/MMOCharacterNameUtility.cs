using System;
using System.Text;

namespace RPGClone.Social
{
    public static class MMOCharacterNameUtility
    {
        public const int MinimumLength = 3;
        public const int MaximumLength = 16;

        public static bool TryValidate(string rawName, out string displayName, out string normalizedName, out string error)
        {
            displayName = NormalizeDisplayName(rawName);
            normalizedName = NormalizeLookupName(displayName);

            if (string.IsNullOrWhiteSpace(rawName))
            {
                error = "Enter a character name.";
                return false;
            }

            if (rawName.Trim() != rawName || rawName.Contains('\t') || rawName.Contains('\n') || rawName.Contains('\r'))
            {
                error = "Names cannot start or end with whitespace.";
                return false;
            }

            if (displayName.Length < MinimumLength || displayName.Length > MaximumLength)
            {
                error = $"Names must be {MinimumLength}-{MaximumLength} letters.";
                return false;
            }

            for (int i = 0; i < displayName.Length; i++)
            {
                if (!IsAsciiLetter(displayName[i]))
                {
                    error = "Use letters only. No spaces, numbers, or punctuation.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public static string NormalizeDisplayName(string rawName)
        {
            string trimmed = string.IsNullOrWhiteSpace(rawName) ? string.Empty : rawName.Trim();
            if (trimmed.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new(trimmed.Length);
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                builder.Append(i == 0 ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
            }

            return builder.ToString();
        }

        public static string NormalizeLookupName(string displayName)
        {
            return string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim().ToLowerInvariant();
        }

        public static string CreateFallbackName(string seed, string characterId)
        {
            string raw = string.IsNullOrWhiteSpace(seed) ? "Adventurer" : seed;
            StringBuilder letters = new();
            for (int i = 0; i < raw.Length && letters.Length < MaximumLength; i++)
            {
                if (IsAsciiLetter(raw[i]))
                {
                    letters.Append(raw[i]);
                }
            }

            if (letters.Length < MinimumLength)
            {
                letters.Clear();
                letters.Append("Adventurer");
            }

            string suffix = CreateLettersFromId(characterId);
            int maxPrefix = Math.Max(MinimumLength, MaximumLength - suffix.Length);
            string prefix = letters.ToString();
            if (prefix.Length > maxPrefix)
            {
                prefix = prefix.Substring(0, maxPrefix);
            }

            string candidate = prefix + suffix;
            return NormalizeDisplayName(candidate.Length <= MaximumLength ? candidate : candidate.Substring(0, MaximumLength));
        }

        private static string CreateLettersFromId(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return "A";
            }

            int value = 0;
            for (int i = 0; i < characterId.Length; i++)
            {
                value = (value + characterId[i]) % (26 * 26);
            }

            char first = (char)('A' + value / 26);
            char second = (char)('A' + value % 26);
            return new string(new[] { first, second });
        }

        private static bool IsAsciiLetter(char value)
        {
            return (value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z');
        }
    }
}
