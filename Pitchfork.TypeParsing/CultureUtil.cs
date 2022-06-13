#if NET5_0_OR_GREATER
#define HAS_GETCULTUREINFO_PREDEFINEDONLY_OVERLOAD
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

namespace Pitchfork.TypeParsing
{
    // Contains helper methods for working with culture strings and comparing them.
    // Since CultureInfo creates new cultures on-demand and adds them to a static
    // cache, this presents opportunity for DoS attacks due to resource exhaustion.
    // The utilities here help us ensure that we're only creating cultures which
    // correspond to actual understood cultures, which provides an upper bound on
    // the amount of static resources we'll consume.
    internal static class CultureUtil
    {
#if !HAS_GETCULTUREINFO_PREDEFINEDONLY_OVERLOAD
        private static HashSet<string> _predefinedCultureNames = GetPredefinedCultureNames();

        private static HashSet<string> GetPredefinedCultureNames()
        {
            // CultureInfo.GetCultureData converts A-Z to a-z, leaving all other
            // characters alone. We'll mimic that same logic here.

            return new HashSet<string>(
                CultureInfo.GetCultures(CultureTypes.AllCultures)
                .Select(ci => AnsiToLower(ci.Name)));
        }
#endif

        // Like CultureInfo, only maps [A-Z] -> [a-z].
        // All non-ASCII characters are left alone.
        [return: NotNullIfNotNull("input")]
        public static string? AnsiToLower(string? input)
        {
            if (input is null)
            {
                return null;
            }

            int idx;
            for (idx = 0; idx < input.Length; idx++)
            {
                if (MiscUtil.IsBetweenInclusive(input[idx], 'A', 'Z'))
                {
                    break;
                }
            }

            if (idx == input.Length)
            {
                return input; // no characters to change.
            }

#if NETCOREAPP2_1_OR_GREATER
            return string.Create(input.Length, (input, idx), static (buffer, state) =>
            {
                ReadOnlySpan<char> input = state.input.AsSpan();
                int idx = state.idx;

                input.CopyTo(buffer);
                for (; idx < buffer.Length; idx++)
                {
                    char c = input[idx];
                    buffer[idx] = MiscUtil.IsBetweenInclusive(c, 'A', 'Z') ? (char)(c | 0x20) : c;
                }
            });
#else
            char[] chars = input.ToCharArray();
            for (; idx < chars.Length; idx++)
            {
                char c = chars[idx];
                if (MiscUtil.IsBetweenInclusive(c, 'A', 'Z'))
                {
                    chars[idx] = (char)(c | 0x20);
                }
            }
            return new string(chars);
#endif
        }

        public static bool AreCulturesEqual(string? cultureNameA, string? cultureNameB)
        {
            if (ReferenceEquals(cultureNameA, cultureNameB)) { return true; }
            if (cultureNameA is null || cultureNameB is null) { return false; }
            return AreCulturesEqual(cultureNameA.AsSpan(), cultureNameB.AsSpan());
        }

        public static bool AreCulturesEqual(ReadOnlySpan<char> cultureNameA, ReadOnlySpan<char> cultureNameB)
        {
            if (cultureNameA.Length != cultureNameB.Length) { return false; }
            for (int i = 0; i < cultureNameA.Length; i++)
            {
                uint chA = cultureNameA[i];
                uint chB = cultureNameB[i];

                if (MiscUtil.IsBetweenInclusive(chA, 'A', 'Z'))
                {
                    chA |= 0x20;
                    chB |= 0x20; // if outside A-Za-z range, will become garbage
                }

                if (chA != chB) { return false; }
            }

            return true;
        }

        public static CultureInfo GetPredefinedCultureInfo(string cultureName)
        {
            // GetCultureInfo eventually calls down to code which can't
            // properly handle control characters, so forbid them.

            if (!cultureName.ContainsControlCharacters())
            {
#if HAS_GETCULTUREINFO_PREDEFINEDONLY_OVERLOAD
                return CultureInfo.GetCultureInfo(cultureName, predefinedOnly: true);
#else
                if (_predefinedCultureNames.Contains(AnsiToLower(cultureName)))
                {
                    return CultureInfo.GetCultureInfo(cultureName);
                }
#endif
            }

            throw new CultureNotFoundException(
                invalidCultureName: cultureName,
                message: new CultureNotFoundException().Message,
                innerException: null);
        }
    }
}
