using System;
using System.Text;

namespace Pitchfork.TypeParsing
{
#if !NETCOREAPP2_1_OR_GREATER
    internal static class StringBuilderExtensions
    {
        public static StringBuilder Append(this StringBuilder sb, ReadOnlySpan<char> value)
            => sb.Append(value.ToString());
    }
#endif
}
