using System;
using System.Reflection;

namespace Pitchfork.TypeParsing
{
    /// <summary>
    /// Contains internal helper methods for working with <see cref="TypeId"/> objects.
    /// </summary>
    public static class TypeIdExtensions
    {
        private static Func<TypeId, string> _debuggerDisplayNameFetcher = GetDebuggerDisplayNameFetcher();

        private static Func<TypeId, string> GetDebuggerDisplayNameFetcher()
        {
            MethodInfo? mi = typeof(TypeId).GetMethod("GetDebuggerDisplayString", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (mi is null || mi.ReturnType != typeof(string))
            {
                throw new PlatformNotSupportedException("TypeId is missing expected method GetDebuggerDisplayString() : string.");
            }

#if NET5_0_OR_GREATER
            return mi.CreateDelegate<Func<TypeId, string>>(target: null); // prefer generic for AOT & trimming 
#else
            return (Func<TypeId, string>)mi.CreateDelegate(typeof(Func<TypeId, string>), target: null);
#endif
        }

        /// <summary>
        /// Given a <see cref="TypeId"/>, returns a friendly C#-like representation of the type string.
        /// </summary>
        /// <param name="typeId">The <see cref="TypeId"/> from which to create a friendly display name.</param>
        /// <returns>The friendly display name of <paramref name="typeId"/>.</returns>
        /// <remarks>
        /// <para>
        /// A friendly display name is a minimal C#-like representation of the type string. For example,
        /// an input of "System.Nullable`1[[System.Int32]]" will become "int?", and an input of
        /// "System.Collections.Generic.Dictionary`2[[System.Int32, System.String]]" will become
        /// "Dictionary&lt;int, string&gt;".
        /// </para>
        /// <para>
        /// These are intended to be used <b>only for display purposes</b> and should never be used as
        /// part of a runtime operation. For exampler, callers mustn't attempt to perform comparisons
        /// against these values.
        /// </para>
        /// </remarks>
        public static string GetFriendlyDisplayName(this TypeId typeId) => _debuggerDisplayNameFetcher(typeId);
    }
}
