using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Pitchfork.TypeParsing
{
    // Based somewhat on ECMA-335 (Sec. II.5.3), but we're a little stricter
    // about what values we allow. For example, we don't allow control chars
    // or escaped quotes or slashes.
    internal abstract class IdentifierRestrictor
    {
        protected abstract ReadOnlySpan<bool> GetAsciiCharsAllowMap();

        private int GetIndexOfFirstDisallowedChar(string name, bool allowNonAsciiChars)
        {
            ReadOnlySpan<bool> allowedAsciiCharsMap = GetAsciiCharsAllowMap();
            Debug.Assert(allowedAsciiCharsMap.Length == 128);

            for (int i = 0; i < name.Length; i++)
            {
                // Check for invalid UTF-16 sequences (we cannot process),
                // control / whitespace / separator characters, and disallowed categories.

                char c = name[i];
                if (c < (uint)allowedAsciiCharsMap.Length)
                {
                    // ASCII - fast track

                    if (!allowedAsciiCharsMap[c])
                    {
                        return i;
                    }
                }
                else
                {
                    // Not ASCII - go through the fallback map
                    // GetUnicodeCategory will return 'Surrogate' if identifier is
                    // malformed UTF-16, and IsAllowedUnicodeCategory will weed it out.

                    if (!allowNonAsciiChars
                        || char.IsControl(c)
                        || char.IsWhiteSpace(c)
                        || !IsUnicodeCategoryAllowed(char.GetUnicodeCategory(name, i)))
                    {
                        return i;
                    }

                    if (char.IsHighSurrogate(c)) { i++; } // already accounted for low surrogate above
                }
            }

            return -1; // all checks passed
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsUnicodeCategoryAllowed(UnicodeCategory unicodeCategory)
        {
            switch (unicodeCategory)
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark:
                case UnicodeCategory.EnclosingMark:
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.LetterNumber:
                case UnicodeCategory.OtherNumber:
                case UnicodeCategory.ConnectorPunctuation:
                case UnicodeCategory.DashPunctuation:
                case UnicodeCategory.OpenPunctuation:
                case UnicodeCategory.ClosePunctuation:
                case UnicodeCategory.InitialQuotePunctuation:
                case UnicodeCategory.FinalQuotePunctuation:
                case UnicodeCategory.OtherPunctuation:
                case UnicodeCategory.MathSymbol:
                case UnicodeCategory.CurrencySymbol:
                case UnicodeCategory.ModifierSymbol:
                case UnicodeCategory.OtherSymbol:
                    return true;

                default:
                    return false;
            }
        }

        public static void ThrowIfDisallowedAssemblyName([NotNull] string? name, ParseOptions options)
            => AssemblyNameRestrictor.Singleton.ThrowIfDisallowedIdentifier(name, options);

        private void ThrowIfDisallowedIdentifier([NotNull] string? name, ParseOptions options)
        {
            if (string.IsNullOrEmpty(name))
            {
                ThrowHelper.ThrowArgumentException_IdentifierMustNotBeNullOrEmpty();
            }

            int idxOfFirstDisallowedChar = GetIndexOfFirstDisallowedChar(name!, options.AllowNonAsciiIdentifiers);
            if (idxOfFirstDisallowedChar >= 0)
            {
                int disallowedCodePoint = name![idxOfFirstDisallowedChar]; // assume for now BMP char or unpaired surrogate

                // If this is actually a well-formed UTF-16 surrogate pair, extract the supplementary code point now.
                // If the code point is a BMP char, this additional work is unnecessary but harmless, so do it anyway.

#if NETCOREAPP3_0_OR_GREATER
                if (Rune.TryGetRuneAt(name, idxOfFirstDisallowedChar, out Rune disallowedRune))
                {
                    disallowedCodePoint = disallowedRune.Value;
                }
#else
                try
                {
                    disallowedCodePoint = char.ConvertToUtf32(name, idxOfFirstDisallowedChar);
                }
                catch (ArgumentException)
                {
                }
#endif

                ThrowHelper.ThrowArgumentException_IdentifierNotAllowedForbiddenCodePoint(name, (uint)disallowedCodePoint);
            }
        }

        public static void ThrowIfDisallowedTypeName([NotNull] string? name, ParseOptions options)
           => TypeNameRestrictor.Singleton.ThrowIfDisallowedIdentifier(name, options);

        private sealed class AssemblyNameRestrictor : IdentifierRestrictor
        {
            internal static readonly AssemblyNameRestrictor Singleton = new AssemblyNameRestrictor();

            private AssemblyNameRestrictor() { } // singleton ctor

            protected override ReadOnlySpan<bool> GetAsciiCharsAllowMap()
            {
                return new bool[128]
                {
                    false, // U+0000 (NUL)
                    false, // U+0001 (SOH)
                    false, // U+0002 (STX)
                    false, // U+0003 (ETX)
                    false, // U+0004 (EOT)
                    false, // U+0005 (ENQ)
                    false, // U+0006 (ACK)
                    false, // U+0007 (BEL)
                    false, // U+0008 (BS)
                    false, // U+0009 (TAB)
                    false, // U+000A (LF)
                    false, // U+000B (VT)
                    false, // U+000C (FF)
                    false, // U+000D (CR)
                    false, // U+000E (SO)
                    false, // U+000F (SI)
                    false, // U+0010 (DLE)
                    false, // U+0011 (DC1)
                    false, // U+0012 (DC2)
                    false, // U+0013 (DC3)
                    false, // U+0014 (DC4)
                    false, // U+0015 (NAK)
                    false, // U+0016 (SYN)
                    false, // U+0017 (ETB)
                    false, // U+0018 (CAN)
                    false, // U+0019 (EM)
                    false, // U+001A (SUB)
                    false, // U+001B (ESC)
                    false, // U+001C (FS)
                    false, // U+001D (GS)
                    false, // U+001E (RS)
                    false, // U+001F (US)
                    true, // U+0020 ' '
                    true, // U+0021 '!'
                    false, // U+0022 '"'
                    true, // U+0023 '#'
                    true, // U+0024 '$'
                    true, // U+0025 '%'
                    true, // U+0026 '&'
                    false, // U+0027 '''
                    true, // U+0028 '('
                    true, // U+0029 ')'
                    false, // U+002A '*'
                    true, // U+002B '+'
                    true, // U+002C ','
                    true, // U+002D '-'
                    true, // U+002E '.'
                    false, // U+002F '/'
                    true, // U+0030 '0'
                    true, // U+0031 '1'
                    true, // U+0032 '2'
                    true, // U+0033 '3'
                    true, // U+0034 '4'
                    true, // U+0035 '5'
                    true, // U+0036 '6'
                    true, // U+0037 '7'
                    true, // U+0038 '8'
                    true, // U+0039 '9'
                    false, // U+003A ':'
                    true, // U+003B ';'
                    true, // U+003C '<'
                    true, // U+003D '='
                    true, // U+003E '>'
                    false, // U+003F '?'
                    true, // U+0040 '@'
                    true, // U+0041 'A'
                    true, // U+0042 'B'
                    true, // U+0043 'C'
                    true, // U+0044 'D'
                    true, // U+0045 'E'
                    true, // U+0046 'F'
                    true, // U+0047 'G'
                    true, // U+0048 'H'
                    true, // U+0049 'I'
                    true, // U+004A 'J'
                    true, // U+004B 'K'
                    true, // U+004C 'L'
                    true, // U+004D 'M'
                    true, // U+004E 'N'
                    true, // U+004F 'O'
                    true, // U+0050 'P'
                    true, // U+0051 'Q'
                    true, // U+0052 'R'
                    true, // U+0053 'S'
                    true, // U+0054 'T'
                    true, // U+0055 'U'
                    true, // U+0056 'V'
                    true, // U+0057 'W'
                    true, // U+0058 'X'
                    true, // U+0059 'Y'
                    true, // U+005A 'Z'
                    false, // U+005B '['
                    false, // U+005C '\'
                    false, // U+005D ']'
                    true, // U+005E '^'
                    true, // U+005F '_'
                    true, // U+0060 '`'
                    true, // U+0061 'a'
                    true, // U+0062 'b'
                    true, // U+0063 'c'
                    true, // U+0064 'd'
                    true, // U+0065 'e'
                    true, // U+0066 'f'
                    true, // U+0067 'g'
                    true, // U+0068 'h'
                    true, // U+0069 'i'
                    true, // U+006A 'j'
                    true, // U+006B 'k'
                    true, // U+006C 'l'
                    true, // U+006D 'm'
                    true, // U+006E 'n'
                    true, // U+006F 'o'
                    true, // U+0070 'p'
                    true, // U+0071 'q'
                    true, // U+0072 'r'
                    true, // U+0073 's'
                    true, // U+0074 't'
                    true, // U+0075 'u'
                    true, // U+0076 'v'
                    true, // U+0077 'w'
                    true, // U+0078 'x'
                    true, // U+0079 'y'
                    true, // U+007A 'z'
                    true, // U+007B '{'
                    true, // U+007C '|'
                    true, // U+007D '}'
                    true, // U+007E '~'
                    false, // U+007F (DEL)
                };
            }
        }

        private sealed class TypeNameRestrictor : IdentifierRestrictor
        {
            internal static readonly TypeNameRestrictor Singleton = new TypeNameRestrictor();

            private TypeNameRestrictor() { } // singleton ctor

            protected override ReadOnlySpan<bool> GetAsciiCharsAllowMap()
            {
                return new bool[128]
                {
                    false, // U+0000 (NUL)
                    false, // U+0001 (SOH)
                    false, // U+0002 (STX)
                    false, // U+0003 (ETX)
                    false, // U+0004 (EOT)
                    false, // U+0005 (ENQ)
                    false, // U+0006 (ACK)
                    false, // U+0007 (BEL)
                    false, // U+0008 (BS)
                    false, // U+0009 (TAB)
                    false, // U+000A (LF)
                    false, // U+000B (VT)
                    false, // U+000C (FF)
                    false, // U+000D (CR)
                    false, // U+000E (SO)
                    false, // U+000F (SI)
                    false, // U+0010 (DLE)
                    false, // U+0011 (DC1)
                    false, // U+0012 (DC2)
                    false, // U+0013 (DC3)
                    false, // U+0014 (DC4)
                    false, // U+0015 (NAK)
                    false, // U+0016 (SYN)
                    false, // U+0017 (ETB)
                    false, // U+0018 (CAN)
                    false, // U+0019 (EM)
                    false, // U+001A (SUB)
                    false, // U+001B (ESC)
                    false, // U+001C (FS)
                    false, // U+001D (GS)
                    false, // U+001E (RS)
                    false, // U+001F (US)
                    false, // U+0020 ' '
                    true, // U+0021 '!'
                    false, // U+0022 '"'
                    true, // U+0023 '#'
                    true, // U+0024 '$'
                    true, // U+0025 '%'
                    false, // U+0026 '&'
                    false, // U+0027 '''
                    true, // U+0028 '('
                    true, // U+0029 ')'
                    false, // U+002A '*'
                    true, // U+002B '+'
                    false, // U+002C ','
                    true, // U+002D '-'
                    true, // U+002E '.'
                    false, // U+002F '/'
                    true, // U+0030 '0'
                    true, // U+0031 '1'
                    true, // U+0032 '2'
                    true, // U+0033 '3'
                    true, // U+0034 '4'
                    true, // U+0035 '5'
                    true, // U+0036 '6'
                    true, // U+0037 '7'
                    true, // U+0038 '8'
                    true, // U+0039 '9'
                    false, // U+003A ':'
                    false, // U+003B ';'
                    true, // U+003C '<'
                    true, // U+003D '='
                    true, // U+003E '>'
                    false, // U+003F '?'
                    true, // U+0040 '@'
                    true, // U+0041 'A'
                    true, // U+0042 'B'
                    true, // U+0043 'C'
                    true, // U+0044 'D'
                    true, // U+0045 'E'
                    true, // U+0046 'F'
                    true, // U+0047 'G'
                    true, // U+0048 'H'
                    true, // U+0049 'I'
                    true, // U+004A 'J'
                    true, // U+004B 'K'
                    true, // U+004C 'L'
                    true, // U+004D 'M'
                    true, // U+004E 'N'
                    true, // U+004F 'O'
                    true, // U+0050 'P'
                    true, // U+0051 'Q'
                    true, // U+0052 'R'
                    true, // U+0053 'S'
                    true, // U+0054 'T'
                    true, // U+0055 'U'
                    true, // U+0056 'V'
                    true, // U+0057 'W'
                    true, // U+0058 'X'
                    true, // U+0059 'Y'
                    true, // U+005A 'Z'
                    false, // U+005B '['
                    false, // U+005C '\'
                    false, // U+005D ']'
                    true, // U+005E '^'
                    true, // U+005F '_'
                    true, // U+0060 '`'
                    true, // U+0061 'a'
                    true, // U+0062 'b'
                    true, // U+0063 'c'
                    true, // U+0064 'd'
                    true, // U+0065 'e'
                    true, // U+0066 'f'
                    true, // U+0067 'g'
                    true, // U+0068 'h'
                    true, // U+0069 'i'
                    true, // U+006A 'j'
                    true, // U+006B 'k'
                    true, // U+006C 'l'
                    true, // U+006D 'm'
                    true, // U+006E 'n'
                    true, // U+006F 'o'
                    true, // U+0070 'p'
                    true, // U+0071 'q'
                    true, // U+0072 'r'
                    true, // U+0073 's'
                    true, // U+0074 't'
                    true, // U+0075 'u'
                    true, // U+0076 'v'
                    true, // U+0077 'w'
                    true, // U+0078 'x'
                    true, // U+0079 'y'
                    true, // U+007A 'z'
                    true, // U+007B '{'
                    true, // U+007C '|'
                    true, // U+007D '}'
                    true, // U+007E '~'
                    false, // U+007F (DEL)
                };
            }
        }
    }
}
