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
            return builder.Replace('+', '.').ToString(); // fix up nested type syntax before returning
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
                    if (_reflectionTypeNamesToFriendlyNames.TryGetValue(typeName, out string? friendlyName))
                    {
                        builder.Append(friendlyName);
                    }
                    else
                    {
                        // Strip off the namespace and write only the type name. For open generic types this will
                        // output "Foo`2" instead of "Foo<,>". This is desirable behavior because it prevents malicious
                        // inputs like "Foo`500000000" from causing us to allocate huge strings and exhaust memory.
                        // Closed generic types (e.g., "Foo`2[[A],[B]]") are handled later.

                        var nameWithoutNamespace = typeName.AsSpan().GetEverythingAfterLast('.');
                        builder.Append(nameWithoutNamespace);
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
                    // Unlike open generic types, closed generic types require the caller to pass full
                    // generic argument information. This means that we won't perform O(n) work unless
                    // our caller passed input of O(n) length. This mitigates algorithmic complexity
                    // attacks and allows us to populate full generic type information in the response.

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
                        builder.Append(beforeArityMarker.GetEverythingAfterLast('.'));
                        ReadOnlySpan<TypeId> remainingGenericArguments = cgi.GenericArguments;
                        do
                        {
                            int thisLevelArity = ExtractNextGenericArity(ref afterArityMarker);
                            builder.Append('<');
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
