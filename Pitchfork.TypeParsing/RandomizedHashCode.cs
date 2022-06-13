using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Pitchfork.TypeParsing
{
    // Provides helper functions for computing a randomized hash code
    // from types which might not provide built-in hash code randomization.
    // For example, long.GetHashCode results in trivial collisions if the
    // high and low components are identical. This type works around that issue.
    // Also contains logic to put headers in front of variable-length data
    // to disallow collisions caused by adjacent buffers.
    internal struct RandomizedHashCode
    {
        private static readonly int _binaryBufferHeader = GetRandomBytes();
        private static readonly int _stringHeader = GetRandomBytes();
        private static readonly int _versionHeader = GetRandomBytes();

#if DEBUG
        private bool _wasProperCtorCalled;
#endif

        private HashCode _worker;

        [Obsolete("Should never be called.", error: true)]
        public RandomizedHashCode()
        {
            Debug.Fail("This should never be called.");
            throw new NotImplementedException();
        }

        public RandomizedHashCode(Caller caller)
        {
#if DEBUG
            _wasProperCtorCalled = true;
#endif

            _worker = new HashCode();
            _worker.Add((int)caller);
        }

        public void Add(bool value) => _worker.Add(value.GetHashCode());

        public void Add(int value) => _worker.Add(value);

        // Null and empty are equivalent here, but should be ok
        public void Add(string? value) => Add(value.AsSpan());

        public void Add(ReadOnlySpan<byte> value)
        {
            _worker.Add(_binaryBufferHeader);

#if NET6_0_OR_GREATER
            // .NET Core already has randomized hash code APIs
            _worker.AddBytes(value);
#else
            // Extract 32-bit values from the buffer one at a time, feeding
            // each 32-bit value into the hash code calculation routine.

            while (value.Length >= sizeof(int))
            {
                _worker.Add(Unsafe.ReadUnaligned<int>(ref MemoryMarshal.GetReference(value)));
                value = value.Slice(sizeof(int));
            }

            // If there's leftover data, feed it through now.

            if (!value.IsEmpty)
            {
                int leftoverData = 0;
                for (int i = 0; i < value.Length; i++)
                {
                    leftoverData = (leftoverData << 8) | value[i];
                }
                _worker.Add(value[0]);
            }
#endif
        }

        public void Add(ReadOnlySpan<char> value)
        {
            _worker.Add(_stringHeader);

#if NETCOREAPP3_0_OR_GREATER
            // .NET Core already has randomized hash code APIs
            _worker.Add(string.GetHashCode(value));
#else
            // If running downlevel, we can't rely on string.GetHashCode being randomized.
            // Instead, we'll extract 32-bit values from the string one at a time, feeding
            // each 32-bit value into the hash code calculation routine.
            Debug.Assert(sizeof(int) / sizeof(char) == 2, "Relies on 2 chars fitting into an int.");

            while (value.Length >= 2)
            {
                _worker.Add(Unsafe.ReadUnaligned<int>(ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value))));
                value = value.Slice(2);
            }

            // If there's leftover data, it's just a single character. Feed it through now.

            if (!value.IsEmpty)
            {
                _worker.Add(value[0]);
            }
#endif
        }

        public void Add(ulong value)
        {
            _worker.Add((int)value);
            _worker.Add((int)(value >> 32));
        }

        public void Add(Version? value)
        {
            _worker.Add(_versionHeader);
            if (value is null)
            {
                _worker.Add(-1);
            }
            else
            {
                _worker.Add(value.Major);
                _worker.Add(value.Minor);
                _worker.Add(value.Build);
                _worker.Add(value.Revision);
            }
        }

        public void Add<T>(T? value) where T : IRandomizedHashCode
        {
            _worker.Add((value is not null) ? value.GetHashCode() : 0);
        }

        [Obsolete("Should never be called.", error: true)]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
        public override int GetHashCode()
#pragma warning restore CS0809
        {
            Debug.Fail("This should never be called.");
            throw new NotImplementedException();
        }

        private static int GetRandomBytes()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] data = new byte[sizeof(int)];
                rng.GetBytes(data);
                return BitConverter.ToInt32(data, 0);
            }
        }

        public int ToHashCode()
        {
#if DEBUG
            Debug.Assert(_wasProperCtorCalled, "Incorrect ctor was called - no initial seed specified.");
#endif

            return _worker.ToHashCode();
        }

        public enum Caller
        {
            ArrayTypeInfo,
            AssemblyId,
            ConstructedGenericInfo,
            PointerTypeInfo,
            PublicKeyToken,
            TypeId,
        }
    }
}
