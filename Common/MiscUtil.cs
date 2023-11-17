using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Pitchfork.Common
{
    internal class MiscUtil
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertBufferLittleEndianToMachineEndian(Span<int> buffer)
        {
            if (!BitConverter.IsLittleEndian)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = BinaryPrimitives.ReverseEndianness(buffer[i]);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBetweenInclusive(int value, int lowerBound, int upperBound)
        {
            Debug.Assert(lowerBound <= upperBound);
            return (uint)(value - lowerBound) <= (uint)(upperBound - lowerBound);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBetweenInclusive(uint value, uint lowerBound, uint upperBound)
        {
            Debug.Assert(lowerBound <= upperBound);
            return (value - lowerBound) <= (upperBound - lowerBound);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBetweenInclusive(ulong value, ulong lowerBound, ulong upperBound)
        {
            Debug.Assert(lowerBound <= upperBound);
            return (value - lowerBound) <= (upperBound - lowerBound);
        }
    }
}
