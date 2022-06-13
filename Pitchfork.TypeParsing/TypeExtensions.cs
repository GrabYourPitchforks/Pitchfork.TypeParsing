using System;

namespace Pitchfork.TypeParsing
{
    internal static class TypeExtensions
    {
        public static bool IsSzArrayType(this Type type)
        {
#if NETCOREAPP2_0_OR_GREATER
            // framework built-in function
            return type.IsSZArray;
#else
            // bounce through a few layers of indirection to detect this
            return type.IsArray && type == type.GetElementType().MakeArrayType();
#endif
        }
    }
}
