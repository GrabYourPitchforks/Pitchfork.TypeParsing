using System;

namespace Pitchfork.TypeParsing
{
    internal static class SpanUtil
    {
        // If 'span' starts with the char 'value', strips the first char and any spaces which
        // follow it from the span, then returns true. Otherwise returns false without changing
        // the provided ref.
        public static bool TryStripFirstCharAndTrailingSpaces(ref ReadOnlySpan<char> span, char value)
        {
            if (span.StartsWith(value))
            {
                span = span.Slice(1).TrimStartSpacesOnly();
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
