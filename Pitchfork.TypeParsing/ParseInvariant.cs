using System;

namespace Pitchfork.TypeParsing
{
    // Invariant parsers that don't allow leading or trailing whitespace,
    // trailing null chars (like int.Parse allows), and other nonsense.
    internal static class ParseInvariant
    {
        public static Version ParseVersion(ReadOnlySpan<char> value)
        {
            // Fail on anything that's not [0-9] or period.
            // This disallows constructs like "1.2<NUL>.3.4" or "1.2 .3.4".

            foreach (char ch in value)
            {
                if (!(('0' <= ch && ch <= '9') || ch == '.'))
                {
                    value = "INVALID".AsSpan(); // let Version.Parse handle it
                    break;
                }
            }

#if NETCOREAPP2_1_OR_GREATER
            return Version.Parse(value);
#else
            return new Version(value.ToString());
#endif
        }
    }
}
