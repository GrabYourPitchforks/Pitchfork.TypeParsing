using System;
using System.Diagnostics.CodeAnalysis;

namespace Pitchfork.TypeParsing
{
    internal static class StringExtensions
    {
        public static bool ContainsControlCharacters([NotNullWhen(true)] this string? value) => ContainsControlCharacters(value.AsSpan());

        public static bool ContainsControlCharacters(this ReadOnlySpan<char> value)
        {
            // Don't need to worry about surrogates - control chars are always BMP.
            foreach (char ch in value)
            {
                if (char.IsControl(ch)) { return true; }
            }
            return false;
        }

        public static ReadOnlySpan<char> StripSurroundingQuotes(this ReadOnlySpan<char> value)
        {
            char ch;
            if (value.Length >= 2 && ((ch = value[0]) is '\'' or '\"') && value[value.Length - 1] == ch)
            {
                return value[1..^1]; // strip surrounding quotes
            }
            else
            {
                return value; // nothing to strip
            }
        }

        public static string TrimSpacesOnly(this string value)
        {
#if NETCOREAPP2_1_OR_GREATER
            return value.Trim(' ');
#else
            var trimmed = value.AsSpan().TrimSpacesOnly();
            return (trimmed.Length == value.Length) ? value : trimmed.ToString();
#endif
        }

        public static ReadOnlySpan<char> TrimEndSpacesOnly(this ReadOnlySpan<char> value)
        {
            return value.TrimEnd(' ');
        }

        public static ReadOnlySpan<char> TrimStartSpacesOnly(this ReadOnlySpan<char> value)
        {
            return value.TrimStart(' ');
        }

        public static ReadOnlySpan<char> TrimSpacesOnly(this ReadOnlySpan<char> value)
        {
            return value.Trim(' ');
        }

        public static SplitResult<char> SplitForbidEmptyTrailer(this ReadOnlySpan<char> value, char splitChar)
        {
            int idxOfSplitChar = value.IndexOf(splitChar);
            if (idxOfSplitChar < 0 || idxOfSplitChar == value.Length - 1)
            {
                return new SplitResult<char>(value, default);
            }
            else
            {
                return new SplitResult<char>(value.Slice(0, idxOfSplitChar), value.Slice(idxOfSplitChar + 1));
            }
        }

        public static bool StartsWith(this ReadOnlySpan<char> span, char value) => !span.IsEmpty && span[0] == value;
    }
}
