using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Pitchfork.TypeParsing
{
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplayString) + "()}")]
    public sealed partial class TypeId
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        private string GetDebuggerDisplayString()
        {
            StringBuilder builder = new StringBuilder();
            DebuggerDisplayHelper.AppendFriendlyDebugNameToBuilder(this, builder);
            return builder.ToString();
        }

        private static class DebuggerDisplayHelper
        {
            private static Dictionary<string, string> _reflectionTypeNamesToFriendlyNames = new()
            {
                ["System.Boolean"] = "bool",
                ["System.Byte"] = "byte",
                ["System.SByte"] = "sbyte",
                ["System.Char"] = "char",
                ["System.Int16"] = "short",
                ["System.UInt16"] = "ushort",
                ["System.Int32"] = "int",
                ["System.UInt32"] = "uint",
                ["System.Int64"] = "long",
                ["System.UInt64"] = "ulong",
                ["System.Single"] = "float",
                ["System.Double"] = "double",
                ["System.Decimal"] = "decimal",
                ["System.IntPtr"] = "nint",
                ["System.UIntPtr"] = "nuint",
                ["System.Object"] = "object",
                ["System.String"] = "string",
            };

            internal static void AppendFriendlyDebugNameToBuilder(TypeId typeId, StringBuilder builder)
            {
                if (typeId.IsElementalType)
                {
                    string typeName = typeId.Name;
                    // First, look for C# keywords.
                    if (_reflectionTypeNamesToFriendlyNames.TryGetValue(typeName, out string friendlyName))
                    {
                        builder.Append(friendlyName);
                    }
                    else
                    {
                        if (!typeId.IsLikelyGenericTypeDefinition(out _))
                        {
                            // This isn't an open generic type; strip off the namespace and write only the type name.
                            int lastIdxOfNamespaceSeparator = typeName.LastIndexOf('.');
                            if (lastIdxOfNamespaceSeparator < 0) { lastIdxOfNamespaceSeparator = -1; }
                            string substr = typeName.Substring(lastIdxOfNamespaceSeparator + 1);
                            builder.Append(substr.Replace('+', '.')); // replace nested type markers if they exist
                        }
                        else
                        {
                            // Open generic type (List<> or List<>.Enumerator); fill in missing brackets.

                            (var beforeArityMarker, var afterArityMarker) = typeName.AsSpan().SplitForbidEmptyTrailer('`');
                            int lastIdxOfNamespaceSeparator = beforeArityMarker.LastIndexOf('.');
                            if (lastIdxOfNamespaceSeparator < 0) { lastIdxOfNamespaceSeparator = -1; }
                            builder.Append(beforeArityMarker.Slice(lastIdxOfNamespaceSeparator + 1));
                            afterArityMarker = afterArityMarker.ToString().Replace('+', '.').AsSpan(); // replace nested type markers if they exist
                            builder.Append('<');
                            do
                            {
                                int arity = ExtractNextGenericArity(ref afterArityMarker);
                                builder.Append(',', arity - 1);
                                builder.Append('>');
                                (beforeArityMarker, afterArityMarker) = afterArityMarker.SplitForbidEmptyTrailer('`');
                                builder.Append(beforeArityMarker);
                            } while (!afterArityMarker.IsEmpty);
                        }
                    }
                }
                else if (typeId.IsArrayType)
                {
                    AppendFriendlyDebugNameToBuilder(typeId.GetUnderlyingType()!, builder);
                    if (typeId.IsSzArrayType)
                    {
                        builder.Append("[]");
                    }
                    else if (typeId.GetArrayRank() == 1)
                    {
                        builder.Append("[*]");
                    }
                    else
                    {
                        builder.Append('[');
                        builder.Append(new string(',', typeId.GetArrayRank() - 1));
                        builder.Append(']');
                    }
                }
                else if (typeId.IsManagedPointerType)
                {
                    AppendFriendlyDebugNameToBuilder(typeId.GetUnderlyingType()!, builder);
                    builder.Append('&');
                }
                else if (typeId.IsUnmanagedPointerType)
                {
                    AppendFriendlyDebugNameToBuilder(typeId.GetUnderlyingType()!, builder);
                    builder.Append('*');
                }
                else if (typeId.IsConstructedGenericType)
                {
                    var cgi = typeId.GetConstructedGenericInfoOrThrow(); // no copy
                    string elementalTypeName = cgi.ElementalType.Name;
                    if (elementalTypeName == "System.Nullable`1" && cgi.GenericArguments.Length == 1)
                    {
                        // special-case Nullable<T> -> T?
                        AppendFriendlyDebugNameToBuilder(cgi.GenericArguments[0], builder);
                        builder.Append('?');
                    }
                    else if (cgi.ElementalType.IsLikelyGenericTypeDefinition(out int totalArity) && totalArity == cgi.GenericArguments.Length)
                    {
                        // See if there are nested generics, like List<T>.Enumerator
                        (var beforeArityMarker, var afterArityMarker) = elementalTypeName.AsSpan().SplitForbidEmptyTrailer('`');
                        int lastIdxOfNamespaceSeparator = beforeArityMarker.LastIndexOf('.');
                        if (lastIdxOfNamespaceSeparator < 0) { lastIdxOfNamespaceSeparator = -1; }
                        builder.Append(beforeArityMarker.Slice(lastIdxOfNamespaceSeparator + 1));
                        afterArityMarker = afterArityMarker.ToString().Replace('+', '.').AsSpan(); // replace nested type markers if they exist
                        builder.Append('<');
                        ReadOnlySpan<TypeId> remainingGenericArguments = cgi.GenericArguments;
                        do
                        {
                            int thisLevelArity = ExtractNextGenericArity(ref afterArityMarker);
                            foreach (TypeId genericArgument in remainingGenericArguments.Slice(0, thisLevelArity))
                            {
                                AppendFriendlyDebugNameToBuilder(genericArgument, builder);
                                builder.Append(", ");
                            }
                            remainingGenericArguments = remainingGenericArguments.Slice(thisLevelArity);
                            builder.Length -= 2; // remove last ", "
                            builder.Append('>');
                            (beforeArityMarker, afterArityMarker) = afterArityMarker.SplitForbidEmptyTrailer('`');
                            builder.Append(beforeArityMarker);
                        } while (!afterArityMarker.IsEmpty);
                        Debug.Assert(remainingGenericArguments.IsEmpty);
                    }
                    else
                    {
                        // This isn't a generic pattern we recognize - dump the raw name
                        builder.Append(typeId.Name);
                    }
                }
                else
                {
                    // This isn't a pattern we recognize - dump the raw name
                    builder.Append(typeId.Name);
                }

                static int ExtractNextGenericArity(ref ReadOnlySpan<char> remaining)
                {
                    // Assumes the input has already been validated.
                    int arity = 0;
                    while (!remaining.IsEmpty)
                    {
                        int thisChar = remaining[0];
                        if (!('0' <= thisChar && thisChar <= '9'))
                        {
                            break;
                        }
                        arity = (arity * 10) + thisChar - '0';
                        remaining = remaining.Slice(1);
                    }
                    return arity;
                }
            }
        }
    }
}
