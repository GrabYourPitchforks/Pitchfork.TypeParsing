using System.Collections.Generic;
using Xunit;

namespace Pitchfork.TypeParsing.Tests
{
    public class TypeIdExtensionsTests
    {
        // C# named primitive types
        public static IEnumerable<object[]> NamedPrimitiveTypes()
        {
            yield return new[] { "System.Boolean", "bool" };
            yield return new[] { "System.Byte", "byte" };
            yield return new[] { "System.SByte", "sbyte" };

            yield return new[] { "System.Char", "char" };
            yield return new[] { "System.Int16", "short" };
            yield return new[] { "System.UInt16", "ushort" };

            yield return new[] { "System.Int32", "int" };
            yield return new[] { "System.UInt32", "uint" };

            yield return new[] { "System.Int64", "long" };
            yield return new[] { "System.UInt64", "ulong" };

            yield return new[] { "System.IntPtr", "nint" };
            yield return new[] { "System.UIntPtr", "nuint" };

            yield return new[] { "System.Decimal", "decimal" };

            yield return new[] { "System.Object", "object" };
            yield return new[] { "System.String", "string" };
        }

        // .NET types that don't have C# keyword equivalents
        public static IEnumerable<object[]> TrickyFundamentalTypes()
        {
            // n.b. the friendly name strips the namespace from each of the types

            yield return new[] { "System.Enum", "Enum" }; // 'enum' does not correspond to Enum class
            yield return new[] { "System.ValueType", "ValueType" }; // 'struct' does not correspond to ValueType class
            yield return new[] { "System.Delegate", "Delegate" }; // 'delegate' does not correspond to Delegate class
        }

        // Special-casing for Nullable<T>
        // n.b. some constructs will be illegal (e.g., Nullable<Nullable<int>>), but we allow since it's not up to us to check constraints
        public static IEnumerable<object[]> NullableTypes()
        {
            yield return new[] { "System.Nullable", "Nullable" }; // not closed Nullable<T>, no special-casing
            yield return new[] { "System.Nullable`1", "Nullable`1" }; // not closed Nullable<T>, no special-casing
            yield return new[] { "System.Nullable`2", "Nullable`2" }; // not closed Nullable<T>, no special-casing
            yield return new[] { "System.Nullable`2[[System.Int32], [System.String]]", "Nullable<int, string>" }; // not closed Nullable<T>, no special-casing
            yield return new[] { "System.Nullable`1[[System.Int32]]", "int?" }; // closed Nullable<T>, replace with question mark
            yield return new[] { "System.Nullable`1[[System.String]]", "string?" }; // also a closed Nullable<T>, even though it's an illegal construct (violates runtime constraints)
            yield return new[] { "System.Nullable`1[[System.Nullable`1[[System.Int32]]]]", "int??" }; // closed Nullable<T>, even though it's an illegal construct (violates runtime constraints)
        }

        // Arrays, pointers, references
        public static IEnumerable<object[]> ArraysPointersAndRefs()
        {
            yield return new[] { "System.SomeType[]", "SomeType[]" }; // szarray
            yield return new[] { "System.Int32[*]", "int[*]" }; // mdarray, rank 1
            yield return new[] { "System.SomeOtherType[,,,,]", "SomeOtherType[,,,,]" }; // mdarray, rank 5

            yield return new[] { "System.SomeType*", "SomeType*" }; // unmanaged pointer
            yield return new[] { "System.SomeType&", "SomeType&" }; // managed pointer
            yield return new[] { "System.Int32**&", "int**&" }; // mixed pointers
        }

        // Generic type handling
        public static IEnumerable<object[]> GenericTypes()
        {
            yield return new[] { "System.SomeType`1", "SomeType`1" }; // open generic type
            yield return new[] { "System.Int32`1", "Int32`1" }; // open generic type (elemental type Int32`1 is not equivalent to elemental type Int32)
            yield return new[] { "System.SomeOtherType`5", "SomeOtherType`5" }; // open generic type

            yield return new[] { "System.Collections.Generic.List`1[[System.Int32]]", "List<int>" }; // closed generic type
            yield return new[] { "System.Collections.Generic.Dictionary`2[[System.String], [System.Collections.Generic.List`1[[System.Int32, mscorlib]]]]", "Dictionary<string, List<int>>" }; // closed generic type

            // For nested generic types, .NET syntax moves arg information to end, but friendly name keeps it infixed

            yield return new[] { "System.SomeGenericType`1+SomeNestedType", "SomeGenericType`1.SomeNestedType" }; // open types
            yield return new[] { "System.SomeGenericType`1+SomeNestedType`2", "SomeGenericType`1.SomeNestedType`2" }; // open types
            yield return new[] { "System.SomeGenericType`2+SomeNestedType`1[[System.Int16], [System.Int32], [System.Int64]]", "SomeGenericType<short, int>.SomeNestedType<long>" }; // closed types
            yield return new[] { "System.SomeGenericType`1+SomeNestedType`2+SomeOtherNestedType`3[[A], [B], [C], [D], [E], [F]]", "SomeGenericType<A>.SomeNestedType<B, C>.SomeOtherNestedType<D, E, F>" }; // closed types
        }

        [Theory]
        [MemberData(nameof(NamedPrimitiveTypes))]
        [MemberData(nameof(TrickyFundamentalTypes))]
        [InlineData("System.Foo.Int32", "Int32")] // wrong namespace: doesn't become 'int'
        [InlineData("System.Int32, SomeAssembly", "int")] // we don't care about assembly name
        [MemberData(nameof(NullableTypes))]
        [MemberData(nameof(ArraysPointersAndRefs))]
        [MemberData(nameof(GenericTypes))]
        [InlineData("System.Foo+Bar+Baz", "Foo.Bar.Baz")] // nested types
        public void GetFriendlyDisplayName_Corpus(string typeIdString, string expectedFriendlyName)
        {
            // Arrange 

            TypeId typeId = TypeId.ParseAssemblyQualifiedName(typeIdString);

            // Act & assert

            Assert.Equal(expectedFriendlyName, typeId.GetFriendlyDisplayName());
        }
    }
}
