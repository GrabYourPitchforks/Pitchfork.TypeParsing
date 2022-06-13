using System;
using System.Buffers.Binary;
using System.Globalization;
using Pitchfork.TypeParsing.Resources;

namespace Pitchfork.TypeParsing
{
    /// <summary>
    /// Represents the public key token portion of an assembly name.
    /// </summary>
    public sealed class PublicKeyToken : IEquatable<PublicKeyToken>, IRandomizedHashCode
    {
        private readonly byte[] _tokenBytes;
        private readonly string _tokenString;

        public PublicKeyToken(string tokenString)
        {
            _tokenString = tokenString?.ToLowerInvariant()!; // null check will take place in TryHexStringToBytes
            _tokenBytes = new byte[8];

            if (!HexUtil.TryHexStringToBytes(_tokenString.AsSpan(), _tokenBytes))
            {
                throw new ArgumentException(
                   paramName: nameof(tokenString),
                   message: SR.PublicKeyToken_CtorStringInvalid);
            }
        }

        public PublicKeyToken(ReadOnlySpan<char> tokenString)
            : this(tokenString.ToString())
        {
        }

        public PublicKeyToken(byte[] tokenBytes)
            : this(tokenBytes.AsSpan())
        {
        }

        public PublicKeyToken(ReadOnlySpan<byte> tokenBytes)
        {
            if (tokenBytes.Length != 8)
            {
                throw new ArgumentException(
                    paramName: nameof(tokenBytes),
                    message: SR.PublicKeyToken_CtorArrayInvalid);
            }

            _tokenBytes = tokenBytes.ToArray();
            _tokenString = BinaryPrimitives.ReadUInt64BigEndian(_tokenBytes).ToString("x16", CultureInfo.InvariantCulture);
        }

        public ReadOnlySpan<byte> TokenBytes => _tokenBytes;

        public string TokenString => _tokenString;

        public static bool operator ==(PublicKeyToken? a, PublicKeyToken? b) => (a is null) ? (b is null) : a.Equals(b);

        public static bool operator !=(PublicKeyToken? a, PublicKeyToken? b) => !(a == b);

        public override bool Equals(object? obj) => Equals(obj as PublicKeyToken);

        public bool Equals(PublicKeyToken? other) => other is not null && TokenBytes.SequenceEqual(other.TokenBytes);

        public override int GetHashCode()
        {
            // We can't call long.GetHashCode since it's too easy to produce
            // a collision. Instead, we'll use our specialized randomized hash
            // code construct.

            RandomizedHashCode hashCode = new RandomizedHashCode(RandomizedHashCode.Caller.PublicKeyToken);
            hashCode.Add(BinaryPrimitives.ReadUInt64LittleEndian(TokenBytes));
            return hashCode.ToHashCode();
        }

        public override string ToString() => TokenString;
    }
}
