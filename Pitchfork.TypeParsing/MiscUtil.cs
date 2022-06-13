using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Pitchfork.TypeParsing
{
    internal class MiscUtil
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBetweenInclusive(uint value, uint lowerBound, uint upperBound)
        {
            Debug.Assert(lowerBound <= upperBound);
            return (value - lowerBound) <= (upperBound - lowerBound);
        }
    }
}
