using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Pitchfork.TypeParsing
{
    internal static class HexUtil
    {
        public static bool TryHexStringToBytes(ReadOnlySpan<char> source, Span<byte> destination)
        {
            if (source.Length != destination.Length * 2)
            {
                return false; // source and destination not sized appropriately
            }

            for (int i = 0; i < destination.Length; i++)
            {
                int combinedValue = (ParseHexChar(source[2 * i]) << 4) | ParseHexChar(source[2 * i + 1]);
                if (combinedValue < 0)
                {
                    // Found a bad hex value (-1 when shifted will keep high bit set), bail out now.
                    return false;
                }
                Debug.Assert(combinedValue <= byte.MaxValue);
                destination[i] = (byte)combinedValue;
            }

            return true; // success all the way across!
        }

        // Returns 0 .. 15, or -1 on error.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ParseHexChar(char value)
        {
            uint valueAsInt = value;

            // ASCII decimal digit?
            if (MiscUtil.IsBetweenInclusive(valueAsInt, '0', '9')) { return (int)(valueAsInt - '0'); }

            // A..F a..f?
            if (MiscUtil.IsBetweenInclusive(valueAsInt | 0x20u, 'a', 'f')) { return (int)((valueAsInt | 0x20u) - 'a' + 10); }

            // Error
            return -1;
        }
    }
}
