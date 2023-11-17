using System;
using System.Text;

namespace Pitchfork.TypeParsing
{
#if !NETCOREAPP2_1_OR_GREATER
    internal static class StringBuilderExtensions
    {
        public unsafe static StringBuilder Append(this StringBuilder sb, ReadOnlySpan<char> value)
        {
            if (!value.IsEmpty)
            {
                fixed (char* pStr = value)
                {
                    sb.Append(pStr, value.Length);
                }
            }
            return sb;
        }
    }
#endif
}
