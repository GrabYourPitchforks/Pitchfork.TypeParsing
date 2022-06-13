using System;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using Xunit.Abstractions;

namespace Pitchfork.TypeParsing.Tests
{
    [Serializable]
    public sealed class WrappedString : IXunitSerializable
    {
        private string _innerWrappedString;

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Should be called only by reflection.", error: true)]
        public WrappedString()
        {
        }

        public WrappedString(string stringToWrap)
        {
            _innerWrappedString = WrapString(stringToWrap); ;
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            _innerWrappedString = info.GetValue<string>("_innerWrappedString");
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue("_innerWrappedString", _innerWrappedString, typeof(string));
        }

        public override string ToString() => _innerWrappedString;

        public string Unwrap() => UnwrapString(_innerWrappedString);

        private static string UnwrapString(string stringToUnwrap)
        {
            if (string.IsNullOrEmpty(stringToUnwrap))
            {
                return stringToUnwrap;
            }

            // n.b. We're going char-by-char because we want to allow invalid UTF-16.
            StringBuilder sb = new StringBuilder();

            ReadOnlySpan<char> remaining = stringToUnwrap.AsSpan();
            int idxOfNextOpenBrace;
            while ((idxOfNextOpenBrace = remaining.IndexOf('[')) >= 0)
            {
                sb = sb.Append(remaining.Slice(0, idxOfNextOpenBrace));
                sb.Append((char)ParseUInt16(remaining.Slice(idxOfNextOpenBrace + 1, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
                remaining = remaining.Slice(idxOfNextOpenBrace + 6);
            }
            sb.Append(remaining);

            return sb.ToString();
        }

        private static string WrapString(string stringToWrap)
        {
            if (string.IsNullOrEmpty(stringToWrap))
            {
                return stringToWrap;
            }

            // n.b. We're going char-by-char because we want to allow invalid UTF-16.
            StringBuilder sb = new StringBuilder();
            foreach (char ch in stringToWrap)
            {
                bool escapeThisChar = (ch < 0x20 || ch >= 0x7F); // escape control chars and non-ASCII
                if (char.IsLetterOrDigit(ch))
                {
                    escapeThisChar = false; // but allow letters and digits, even non-ASCII
                }

                if (ch is '[' or ']')
                {
                    escapeThisChar = true; // must escape []
                }

                if (escapeThisChar)
                {
                    sb.Append($"[{(int)ch:X4}]");
                }
                else
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }

        private static ushort ParseUInt16(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider)
        {
#if NETCOREAPP2_1_OR_GREATER
            return ushort.Parse(s, style, provider);
#else
            return ushort.Parse(s.ToString(), style, provider);
#endif
        }
    }

#if !NETCOREAPP2_1_OR_GREATER
    internal static class StringBuilderExtensions
    {
        public static StringBuilder Append(this StringBuilder @this, ReadOnlySpan<char> value) => @this.Append(value.ToString());
    }
#endif
}
