using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Pitchfork.TypeParsing.Tests
{
    public class TypeIdTests
    {
        public static IEnumerable<object[]> CreateByRefType(Type type, object[] extraData)
        {
            yield return new object[] { type.MakeByRefType() }.Concat(extraData).ToArray();
        }

        public static IEnumerable<object[]> CreateVariableBoundArrayWithRank(Type type, int rank, object[] extraData)
        {
            Type arrayType = type.MakeArrayType(rank);
            yield return new object[] { arrayType }.Concat(extraData).ToArray();
        }

        public static IEnumerable<object[]> GetAdditionalConstructedTypeData()
        {
            yield return new object[] { typeof(Dictionary<List<int[]>[,], List<int?[][][,]>>[]), 16 };

            // "Dictionary<List<int[]>[,], List<int?[][][,]>>[]" breaks down to complexity 16 like so:
            //
            // 01: Dictionary<List<int[]>[,], List<int?[][][,]>>[]
            // 02: `- Dictionary<List<int[]>[,], List<int?[][][,]>>
            // 03:    +- Dictionary`2
            // 04:    +- List<int[]>[,]
            // 05:    |  `- List<int[]>
            // 06:    |     +- List`1
            // 07:    |     `- int[]
            // 08:    |        `- int
            // 09:    `- List<int?[][][,]>
            // 10:       +- List`1
            // 11:       `- int?[][][,]
            // 12:          `- int?[][]
            // 13:             `- int?[]
            // 14:                `- int?
            // 15:                   +- Nullable`1
            // 16:                   `- int

            yield return new object[] { typeof(int[]).MakePointerType().MakeByRefType(), 4 }; // int[]*&
            yield return new object[] { typeof(long).MakeArrayType(31), 2 }; // long[,,,,,,,...]
            yield return new object[] { typeof(long).Assembly.GetType("System.Int64[*]"), 2 }; // long[*]
        }

        [Theory]
        [InlineData(typeof(AssemblyId), 1)]
        [InlineData(typeof(AssemblyIdTests), 1)]
        [InlineData(typeof(object), 1)]
        [InlineData(typeof(Assert), 1)] // xunit
        [InlineData(typeof(int[]), 2)]
        [InlineData(typeof(int[,][]), 3)]
        [InlineData(typeof(Nullable<>), 1)] // open generic type treated as elemental
        [MemberData(nameof(GetAdditionalConstructedTypeData))]
        public void CreateFromExistingType_RoundTrips_Success(Type type, int expectedComplexity)
        {
            // Type -> TypeId

            TypeId typeId = TypeId.CreateFromExisting(type);
            Assert.Equal(type.FullName, typeId.Name);
            Assert.Equal(type.AssemblyQualifiedName, typeId.AssemblyQualifiedName);
            Assert.Equal(expectedComplexity, typeId.TotalComplexity);
            Assert.Equal(AssemblyId.CreateFromExisting(type.Assembly), typeId.Assembly);

            // TypeId -> Type

            Type roundTrippedType = typeId.DangerousGetRuntimeType();
            Assert.Equal(type, roundTrippedType);
        }

        [Theory]
        [InlineData(typeof(int), "System.Int32")]
        [InlineData(typeof(int[]), "System.Int32[]")]
        [InlineData(typeof(int[,,,]), "System.Int32[,,,]")]
        [InlineData(typeof(int*), "System.Int32*")]
        [MemberData(nameof(CreateVariableBoundArrayWithRank), typeof(int), 1, new object[] { "System.Int32[*]" })]
        public void CreateFromExistingType_AndParseAQNFromTypeString_Success(Type type, string expectedTypeName)
        {
            // Test 1 - try creating from the Type instance

            TypeId typeId1 = TypeId.CreateFromExisting(type);
            Assert.Equal(expectedTypeName, typeId1.Name);
            Assert.Equal(AssemblyId.CreateFromExisting(type.Assembly), typeId1.Assembly);

            // Test 2 - try creating from the parsed type name

            TypeId typeId2 = TypeId.ParseAssemblyQualifiedName(type.AssemblyQualifiedName);
            Assert.Equal(expectedTypeName, typeId2.Name);
            Assert.Equal(AssemblyId.CreateFromExisting(type.Assembly), typeId2.Assembly);
        }

        [Theory]
        [InlineData(typeof(int[]), "System.Int32[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
        [InlineData(typeof(HashSet<long>), "System.Collections.Generic.HashSet`1[[System.Int64, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]], System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
        [InlineData(typeof(int*), "System.Int32*, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
        [InlineData(typeof(int[][,,]), "System.Int32[,,][], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")] // szarray(of mdarray): C# declaration order is reversed from Reflection
        [MemberData(nameof(CreateByRefType), typeof(int[]), new object[] { "System.Int32[]&, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" })]
        public void CreateFromExisting_FollowTypeForwards(Type type, string expectedAQN)
        {
            TypeId typeId = TypeId.CreateFromExisting(type, followTypeForwards: true);
            Assert.Equal(expectedAQN, typeId.AssemblyQualifiedName);
        }

        public static IEnumerable<object[]> CreateFromExisting_NegativeTestData()
        {
            yield return new[] { typeof(List<>).GetGenericArguments()[0] }; // generic (unbound) type argument

#if NET6_0_OR_GREATER
            if (OperatingSystem.IsWindows())
#endif
            {
                yield return new[] { Type.GetTypeFromCLSID(Guid.Empty) }; // dummy COM object type
            }
        }

        [Theory]
        [MemberData(nameof(CreateFromExisting_NegativeTestData), DisableDiscoveryEnumeration = true)] // discovery of COM types is unreliable
        public void CreateFromExisting_WithUnsupportedTypes_Fails(Type type)
        {
            Assert.Throws<ArgumentException>("type", () => TypeId.CreateFromExisting(type));
        }

        [Theory]
        [InlineData(
            "System.Int32",
            "System.Int32")]
        [InlineData(
            "System.SomeGenericType`1[[System.SomethingElse, mscorlib]]", // AQNs within the generic marker are ok
            "System.SomeGenericType`1[[System.SomethingElse, mscorlib, Culture=neutral, PublicKeyToken=null]]")]
        [InlineData(
            "System.SomeGenericType`2[[System.SomethingElse, mscorlib],[System.NestedGeneric`1[[System.NonQualifiedName]][][], mscorlib]]",
            "System.SomeGenericType`2[[System.SomethingElse, mscorlib, Culture=neutral, PublicKeyToken=null],[System.NestedGeneric`1[[System.NonQualifiedName]][][], mscorlib, Culture=neutral, PublicKeyToken=null]]")]
        public void Parse_NoAssemblyQualifiedNames_Success(string typeName, string expectedAssemblyQualifiedName)
        {
            TypeId typeId1 = TypeId.Parse(typeName, assembly: null);
            Assert.Equal(expectedAssemblyQualifiedName, typeId1.AssemblyQualifiedName);
        }

        [Theory]
        [InlineData(null, null, true)] // null == null
        [InlineData("SomeType", null, false)] // null != not null
        [InlineData("SomeType", "SomeType", true)] // exact name match, no trailing data
        [InlineData("SomeType", "sometype", false)] // names are case-sensitive
        [InlineData("SomeType", "SomeType, SomeAssembly", false)] // null assembly vs. non-null assembly
        [InlineData("SomeType, SomeAssembly", "SomeType, SomeAssembly", true)] // same assembly
        [InlineData("SomeType, SomeAssembly", "SomeType, SomeAssembly, Version=1.2.3.4", false)] // assembly version mismatch
        [InlineData("SomeType&, SomeAssembly", "SomeType*, SomeAssembly", false)] // managed pointer vs unmanaged pointer
        [InlineData("SomeType&, SomeAssembly", "SomeType&, SomeAssembly", true)] // both pointers managed = ok
        [InlineData("SomeType[], SomeAssembly", "SomeType[*], SomeAssembly", false)] // szarray vs variable-bound array
        [InlineData("SomeType[], SomeAssembly", "SomeType[,], SomeAssembly", false)] // szarray vs variable-bound array
        [InlineData("SomeType[,], SomeAssembly", "SomeType[,], SomeAssembly", true)] // both arrays are the same
        [InlineData("SomeType[], SomeAssembly", "SomeType[], SomeAssembly", true)] // both arrays are the same
        [InlineData("SomeType`1[[SomeInnerType]]", "SomeType`1[[SomeInnerType]]", true)] // generic, same
        [InlineData("SomeType`1[[SomeInnerType]]", "SomeType`1", false)] // open vs. closed generic type
        [InlineData("SomeType`1[[SomeInnerType]]", "SomeType`1[[SomeInnerType[]]]", false)] // generic args differ
        public void EqualityTests(string typeAssemblyQualifiedName1, string typeAssemblyQualifiedName2, bool expectedResult)
        {
            TypeId typeId1 = (typeAssemblyQualifiedName1 is not null) ? TypeId.ParseAssemblyQualifiedName(typeAssemblyQualifiedName1) : null;
            TypeId typeId2 = (typeAssemblyQualifiedName2 is not null) ? TypeId.ParseAssemblyQualifiedName(typeAssemblyQualifiedName2) : null;

            if (typeId1 is not null)
            {
                Assert.Equal(expectedResult, typeId1.Equals(typeId2));
                Assert.Equal(expectedResult, typeId1.Equals((object)typeId2));
            }
            if (typeId2 is not null)
            {
                Assert.Equal(expectedResult, typeId2.Equals(typeId1));
                Assert.Equal(expectedResult, typeId2.Equals((object)typeId1));
            }

            Assert.Equal(expectedResult, typeId1 == typeId2);
            Assert.Equal(expectedResult, typeId2 == typeId1);

            Assert.NotEqual(expectedResult, typeId1 != typeId2);
            Assert.NotEqual(expectedResult, typeId2 != typeId1);

            if (expectedResult && typeId1 is not null && typeId2 is not null)
            {
                Assert.Equal(typeId1.GetHashCode(), typeId2.GetHashCode());
            }
        }

        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(int[]))]
        [MemberData(nameof(CreateByRefType), typeof(int), new object[0])]
        public void DangerousGetRuntimeType_WithAssemblyName_SuccessCases(Type expectedType)
        {
            TypeId typeId = TypeId.CreateFromExisting(expectedType);
            Type roundTrippedType = typeId.DangerousGetRuntimeType();
            Assert.Equal(expectedType, roundTrippedType);
        }

        [Fact]
        public void DangerousGetRuntimeType_HonorsThrowOnErrorFlag()
        {
            TypeId nonExistingWithAssembly = TypeId.Parse("System.DoesNotExist", assembly: AssemblyId.CreateFromExisting(typeof(object).Assembly));
            TypeId nonExistingWithoutAssembly = TypeId.Parse("System.DoesNotExist", assembly: null);

            // Test 1 - does not throw

            Assert.Null(nonExistingWithAssembly.DangerousGetRuntimeType(throwOnError: false));
            Assert.Null(nonExistingWithoutAssembly.DangerousGetRuntimeType(throwOnError: false));

            // Test 2 - throws

            Assert.Throws<TypeLoadException>(() => Assert.Null(nonExistingWithAssembly.DangerousGetRuntimeType(throwOnError: true)));
            Assert.Throws<TypeLoadException>(() => Assert.Null(nonExistingWithoutAssembly.DangerousGetRuntimeType(throwOnError: true)));
        }

        [Theory]
        [InlineData("System.Int32", typeof(int))]
        [InlineData("System.Object", typeof(object))]
        [InlineData("System.Object[]", typeof(object[]))]
        public void DangerousGetRuntimeType_WithoutAssemblyName_SuccessCases(string typeName, Type expectedType)
        {
            TypeId typeId = TypeId.Parse(typeName, assembly: null);
            Type roundTrippedType = typeId.DangerousGetRuntimeType();
            Assert.Equal(expectedType, roundTrippedType);
        }

        [Theory]
        [InlineData("System.Int32, mscorlib")]
        [InlineData("System.SomeGenericType`1[[System.SomethingElse, mscorlib]], mscorlib")]
        public void Parse_RejectsAssemblyQualifiedNames(string typeName)
        {
            Assert.Throws<ArgumentException>(() => TypeId.Parse(typeName, assembly: null));
        }

        [Theory]
        [InlineData("System.SomeGeneric`2[[System.Something]]")]
        public void Parse_RejectsNamesWithMismatchedGenericArity(string typeName)
        {
            Assert.Throws<InvalidOperationException>(() => TypeId.Parse(typeName, assembly: null));
        }

        [Theory]
        [InlineData("System.Int32", 1)] // base type only
        [InlineData("System.Int32[]", 2)] // base type + 1 decoratoe
        [InlineData("System.Int32[][*][]&", 5)] // base type + 4 decorators
        [InlineData("GenericType`1[[InnerType]]", 2)] // arg0 has depth 1; +1 for generic
        [InlineData("GenericType`1[[InnerType]][]", 3)] // arg0 has depth 1; +1 each for generic and final szarray
        [InlineData("GenericType`1[[InnerType**]][]", 5)] // arg0 has depth 3; +1 each for generic and final szarray
        [InlineData("GenericType`2[[InnerType**],[OtherInnerType[,,,]]][]", 5)] // arg0 has depth 3; arg1 has depth 2; +1 each for generic and final szarray
        [InlineData("GenericType`2[[InnerType**],[OtherInnerType[,,,]**]][]", 6)] // arg0 has depth 3; arg1 has depth 4; +1 each for generic and final szarray
        [InlineData("GenericType`2[[InnerType**],[OtherInnerType`1[[SubNested***]]]][]", 7)] // arg0 has depth 3; arg1 has depth 5; +1 each for generic and final szarray
        public void Parse_HonorsMaxRecursiveDepth(string typeName, int expectedMaxDepth)
        {
            // Ensure the expected depth is sufficient to parse the type name; no exceptions.

            TypeId.Parse(typeName, assembly: null, new ParseOptions() { MaxRecursiveDepth = expectedMaxDepth });
            TypeId.ParseAssemblyQualifiedName(typeName, new ParseOptions() { MaxRecursiveDepth = expectedMaxDepth });

            // Now bump the depth down by one and assert failure.

            if (expectedMaxDepth > 1)
            {
                Assert.Throws<InvalidOperationException>(() => TypeId.Parse(typeName, assembly: null, new ParseOptions() { MaxRecursiveDepth = expectedMaxDepth - 1 }));
                Assert.Throws<InvalidOperationException>(() => TypeId.ParseAssemblyQualifiedName(typeName, new ParseOptions() { MaxRecursiveDepth = expectedMaxDepth - 1 }));
            }
        }

        [Theory]
        [InlineData("Foo ")] // can't end with a space
        [InlineData("Foo *")] // can't have spaces betwen type name and decorator
        [InlineData("Foo &")]
        [InlineData("Foo []")]
        [InlineData("Foo`1 [[Nested]]")] // can't have spaces before generic marker
        [InlineData("Foo`1[[Nested ]]")] // can't have spaces within nested type name
        [InlineData("Foo`1[[Nested *]]")] // checking recursive decorator logic
        [InlineData("Foo`1[[]]")] // can't have constructed generic types with empty type names
        [InlineData("Foo[a]")] // bad array specifier
        [InlineData("Foo[*,]")]
        [InlineData("Foo[,+]")]
        [InlineData("Foo`1[[Nested],]")] // bad generic specifier
        [InlineData("Foo`1[[Nested]")] // generic specifier terminates early
        [InlineData("Foo`1[[Nested")] // generic specifier terminates early
        [InlineData("Foo`1[[Nested\0]]")] // embedded nulls
        public void Parse_NegativeTestCases_Fails(string typeName)
        {
            Assert.Throws<ArgumentException>(() => TypeId.Parse(typeName, assembly: null));
            Assert.Throws<ArgumentException>(() => TypeId.ParseAssemblyQualifiedName(typeName + ", SomeAssembly"));
        }

        public static IEnumerable<object[]> Parse_PositiveTestCaseData()
        {
            TypeId simpleTypeAWithAsm = TypeId.ParseAssemblyQualifiedName("SimpleTypeA, AssemblyA");
            TypeId simpleTypeBWithAsm = TypeId.ParseAssemblyQualifiedName("SimpleTypeB, AssemblyB");
            TypeId generic1ArityWithAsm = TypeId.ParseAssemblyQualifiedName("Generic`1, AssemblyGeneric1");

            TypeId simpleTypeAWithoutAsm = TypeId.ParseAssemblyQualifiedName("SimpleTypeA");
            TypeId simpleTypeBWithoutAsm = TypeId.ParseAssemblyQualifiedName("SimpleTypeB");
            TypeId generic1ArityWithoutAsm = TypeId.ParseAssemblyQualifiedName("Generic`1");
            TypeId generic2ArityWithoutAsm = TypeId.ParseAssemblyQualifiedName("Generic`2");

            yield return new object[]
            {
                "SimpleTypeA",
                simpleTypeAWithoutAsm,
                1
            };

            yield return new object[]
            {
                "   SimpleTypeB", // spaces are trimmed as long as they're not immediately after the type name
                simpleTypeBWithoutAsm,
                1
            };

            yield return new object[]
            {
                "SimpleTypeA* []", // spaces between decorators are ok
                simpleTypeAWithoutAsm.MakeUnmanagedPointerType().MakeSzArrayType(),
                3 //                  2                          1
                  // alternatively: (01) SimpleTypeA*[]
                  //                (02) SimpleTypeA*
                  //                (03) SimpleTypeA
            };

            yield return new object[]
            {
                "SimpleTypeB[] [,, ,, ,,]  [  *  ] &", // spaces between decorators are ok
                simpleTypeBWithoutAsm.MakeSzArrayType().MakeVariableBoundArrayType(7).MakeVariableBoundArrayType(1).MakeManagedPointerType(),
                5 //                  4                 3                             2                             1
                  // alternatively: (01) SimpleTypeB[][,,,,,,][*]&
                  //                (02) SimpleTypeB[][,,,,,,][*]
                  //                (03) SimpleTypeB[][,,,,,,]
                  //                (04) SimpleTypeB[]
                  //                (05) SimpleTypeB
            };

            yield return new object[]
            {
                "Generic`2[ [ SimpleTypeA, AssemblyA ] , [ SimpleTypeB, AssemblyB ] ] &", // closed generic type with spaces between brackets ok
                generic2ArityWithoutAsm.MakeGenericType(simpleTypeAWithAsm, simpleTypeBWithAsm).MakeManagedPointerType(),
                5 //                    4               3                   2                   1
                  // alternatively: (01) Generic`2[[SimpleTypeA],[SimpleTypeB]]&
                  //                (02) Generic`2[[SimpleTypeA],[SimpleTypeB]]
                  //                (03) +- Generic`2
                  //                (04) +- SimpleTypeA
                  //                (05) `- SimpleTypeB
            };

            yield return new object[]
            {
                "Generic`2[] [] [] []", // open generic type with spaces between decorators ok
                generic2ArityWithoutAsm.MakeSzArrayType().MakeSzArrayType().MakeSzArrayType().MakeSzArrayType(),
                5 //                    4                 3                 2                 1
                  // alternatively: (01) Generic`2[][][][]
                  //                (02) Generic`2[][][]
                  //                (03) Generic`2[][]
                  //                (04) Generic`2[]
                  //                (05) Generic`2
            };

            yield return new object[]
            {
                "Generic`1[ [Generic`1[ [ Generic`1[*] *, AssemblyGeneric1 ] ][], AssemblyGeneric1 ] ]", // nested generics; some open, some closed
                generic1ArityWithoutAsm.MakeGenericType(generic1ArityWithAsm.MakeGenericType(generic1ArityWithAsm.MakeVariableBoundArrayType(1).MakeUnmanagedPointerType()).MakeSzArrayType()),
                8 //                    7               6                    5               4                    3                             2                           1
                  // alternatively: (01) Generic`1[[Generic`1[[Generic`1[*]*]][]]]
                  //                (02) +- Generic`1
                  //                (03) `- Generic`1[[Generic`1[*]*]][]
                  //                (04)    Generic`1[[Generic`1[*]*]]
                  //                (05)    +- Generic`1
                  //                (06)    `- Generic`1[*]*
                  //                (07)       Generic`1[*]
                  //                (08)       Generic`1
            };
        }

        [Theory]
        [MemberData(nameof(Parse_PositiveTestCaseData), DisableDiscoveryEnumeration = true)] // TestIds aren't serializable
        public void Parse_PositiveTestCases_Success(string typeName, TypeId expectedTypeId, int expectedComplexity)
        {
            var asm = AssemblyId.Parse("SomeAssembly");

            Assert.Equal(expectedTypeId, TypeId.Parse(typeName, assembly: null));
            Assert.Equal(expectedTypeId, TypeId.ParseAssemblyQualifiedName(typeName));
            Assert.Equal(expectedTypeId.WithAssembly(asm), TypeId.ParseAssemblyQualifiedName(typeName + ", " + asm.Name));
            Assert.Equal(expectedComplexity, expectedTypeId.TotalComplexity);
        }

        [Theory]
        [MemberData(nameof(CommonData.SampleDisallowedTypeNamesAsWrappedStrings), MemberType = typeof(CommonData))]
        public void Parse_WithDisallowedName_Throws(WrappedString typeName)
        {
            string unwrapped = typeName.Unwrap();
            Assert.Throws<ArgumentException>(() => TypeId.Parse(unwrapped, assembly: null));
        }

        [Fact]
        public void Parse_RespectsNonAsciiCharsAllowedSetting()
        {
            // With non-ASCII disallowed

            Assert.Throws<ArgumentException>(() => TypeId.Parse("Helloéthere", assembly: null, new ParseOptions() { AllowNonAsciiIdentifiers = false }));
            Assert.Throws<ArgumentException>(() => TypeId.ParseAssemblyQualifiedName("Helloéthere", new ParseOptions() { AllowNonAsciiIdentifiers = false }));

            // With non-ASCII allowed

            TypeId typeId = TypeId.Parse("Helloéthere", assembly: null, new ParseOptions() { AllowNonAsciiIdentifiers = true });
            Assert.Equal("Helloéthere", typeId.Name);

            typeId = TypeId.ParseAssemblyQualifiedName("Helloéthere", new ParseOptions() { AllowNonAsciiIdentifiers = true });
            Assert.Equal("Helloéthere", typeId.Name);

            // But certain classes (like newlines) are still disallowed, even with non-ASCII allowed

            Assert.Throws<ArgumentException>(() => TypeId.Parse("Hello\u2028there", assembly: null, new ParseOptions() { AllowNonAsciiIdentifiers = false }));
            Assert.Throws<ArgumentException>(() => TypeId.ParseAssemblyQualifiedName("Hello\u2028there", new ParseOptions() { AllowNonAsciiIdentifiers = false }));
        }

        [Theory]
        [InlineData("MyType`1", 1)]
        [InlineData("MyType`2+SomeNestedType`3", 5)]
        [InlineData("A`1+B`3+C`4", 8)]
        public void MakeGenericType_WithCorrectArityMarkers_Success(string baseTypeName, int arityToPassIn)
        {
            TypeId typeId = TypeId.Parse(baseTypeName, assembly: null);
            TypeId[] genericArgs = MakeSequentialTypeIdNames(arityToPassIn).ToArray();
            TypeId constructedGenericType = typeId.MakeGenericType(genericArgs);

            string expectedConstructedGenericTypeName = baseTypeName + "[[" + string.Join("],[", genericArgs.Select(arg => arg.Name)) + "]]";
            Assert.Equal(expectedConstructedGenericTypeName, constructedGenericType.Name);
        }

        [Theory]
        [InlineData(typeof(List<>), new Type[] { typeof(int) }, typeof(List<int>))]
        [InlineData(typeof(Func<,,>), new Type[] { typeof(string), typeof(int), typeof(object) }, typeof(Func<string, int, object>))]
        public void MakeGenericType_FromTypes_WithCorrectArityMarkers_Success(Type openGenericType, Type[] genericArgs, Type expectedConstructedType)
        {
            TypeId openGenericTypeId = TypeId.CreateFromExisting(openGenericType);
            Assert.True(openGenericTypeId.IsElementalType);
            Assert.False(openGenericTypeId.IsConstructedGenericType);
            Assert.True(openGenericTypeId.IsLikelyGenericTypeDefinition(out _));

            TypeId actualConstructedTypeId = openGenericTypeId.MakeGenericType(genericArgs);
            Assert.True(actualConstructedTypeId.IsConstructedGenericType);
            Assert.False(actualConstructedTypeId.IsLikelyGenericTypeDefinition(out _));

            Assert.Equal(TypeId.CreateFromExisting(expectedConstructedType), actualConstructedTypeId);
            Assert.Equal(genericArgs.Length, actualConstructedTypeId.GetGenericParameterCount());
            Assert.Equal(genericArgs.Select(type => TypeId.CreateFromExisting(type)), actualConstructedTypeId.GetGenericParameters());
            Assert.Same(openGenericTypeId, actualConstructedTypeId.GetUnderlyingType());
            Assert.Same(openGenericTypeId, actualConstructedTypeId.GetGenericTypeDefinition());
        }

        [Theory]
        [InlineData(typeof(List<>), new Type[] { typeof(int), typeof(object) })]
        [InlineData(typeof(Func<,,>), new Type[] { typeof(string), typeof(int), typeof(object), typeof(object) })]
        public void MakeGenericType_FromTypes_WithIncorrectArityMarkers_Throws(Type openGenericType, Type[] genericArgs)
        {
            TypeId openGenericTypeId = TypeId.CreateFromExisting(openGenericType);
            Assert.Throws<InvalidOperationException>(() => openGenericTypeId.MakeGenericType(genericArgs));
        }

        [Fact]
        public void MakeGenericType_WithEmptyArray_Fails()
        {
            TypeId elementalType = TypeId.Parse("SomeElementalType", assembly: null);

            Assert.Throws<ArgumentOutOfRangeException>("types", () => elementalType.MakeGenericType(new Type[0]));
            Assert.Throws<ArgumentOutOfRangeException>("types", () => elementalType.MakeGenericType(new TypeId[0]));
        }

        [Theory]
        [InlineData("`1", 1)] // we need something before the first backtick
        [InlineData("a`1+`2", 3)] // we need something before the second backtick
        [InlineData("MyType`0", 1)]
        [InlineData("MyType`", 1)] // backtick by itself has no meaning
        [InlineData("MyType`+Sub`1", 1)] // backtick by itself has no meaning
        [InlineData("MyType`hello", 1)] // backtick by itself has no meaning
        [InlineData("MyType`1", 2)]
        [InlineData("MyType`01", 1)] // leading zeroes are forbidden
        [InlineData("MyType`2a", 2)] // a is not a valid terminator for arity
        [InlineData("MyType`2`3", 2)] // ` is not a valid terminator for arity
        [InlineData("MyType`2`3", 3)]
        [InlineData("MyType`2`3", 5)]
        [InlineData("MyType`2+`3", 4)]
        [InlineData("MyType`2+`3", 6)]
        [InlineData("MyType`2147483647", 2)] // int.MaxValue
        [InlineData("MyType`2147483647+SomeNestedType`2147483647+SomeOtherNestedType`3", 1)] // int.MaxValue + int.MaxValue + 3 = 1 after overflow
        [InlineData("MyType`4294967295", 2)] // uint.MaxValue
        [InlineData("MyType", 1)] // no markers at all
        public void MakeGenericType_WithIncorrectArityMarkers_Fails(string baseTypeName, int arityToPassIn)
        {
            TypeId typeId = TypeId.Parse(baseTypeName, assembly: null);
            TypeId[] genericArgs = MakeSequentialTypeIdNames(arityToPassIn).ToArray();
            Assert.Throws<InvalidOperationException>(() => typeId.MakeGenericType(genericArgs));
        }

        [Theory]
        [InlineData(typeof(int[]), 3)]
        [InlineData(typeof(List<int>[]), 5)]
        [InlineData(typeof(List<>), 2)]
        [InlineData(typeof(void*), 3)]
        public void MakeManagedPointerType_WhenValid_Success(Type baseType, int expectedNewTotalComplexity)
        {
            TypeId baseTypeId = TypeId.CreateFromExisting(baseType);
            TypeId newTypeId = baseTypeId.MakeManagedPointerType();

            Assert.True(newTypeId.IsManagedPointerType);
            Assert.False(newTypeId.IsUnmanagedPointerType);
            Assert.False(newTypeId.IsElementalType);
            Assert.Equal(baseTypeId, newTypeId.GetUnderlyingType());
            Assert.Equal(baseTypeId.Name + "&", newTypeId.Name);
            Assert.Equal(expectedNewTotalComplexity, newTypeId.TotalComplexity);
            Assert.Same(baseTypeId.Assembly, newTypeId.Assembly);
            Assert.Same(baseTypeId, newTypeId.GetUnderlyingType());
        }

        [Fact]
        public void MakeManagedPointerType_WhenDisallowed_ThrowsInvalidOperationException()
        {
            TypeId baseType = TypeId.CreateFromExisting(typeof(int).MakeByRefType());
            Assert.True(baseType.IsManagedPointerType);

            Assert.Throws<InvalidOperationException>(() => baseType.MakeManagedPointerType());
            Assert.Throws<InvalidOperationException>(() => baseType.MakeUnmanagedPointerType());
            Assert.Throws<InvalidOperationException>(() => baseType.MakeSzArrayType());
            Assert.Throws<InvalidOperationException>(() => baseType.MakeVariableBoundArrayType(1));
            Assert.Throws<InvalidOperationException>(() => baseType.MakeVariableBoundArrayType(2));

            baseType = TypeId.CreateFromExisting(typeof(List<>).MakeByRefType());
            Assert.True(baseType.IsManagedPointerType);

            Assert.Throws<InvalidOperationException>(() => baseType.MakeGenericType(typeof(int)));
        }

        [Theory]
        [InlineData(typeof(int[]), 3)]
        [InlineData(typeof(List<int>[]), 5)]
        [InlineData(typeof(List<>), 2)]
        [InlineData(typeof(void*), 3)]
        public void MakeUnmanagedPointerType_WhenValid_Success(Type baseType, int expectedNewTotalComplexity)
        {
            TypeId baseTypeId = TypeId.CreateFromExisting(baseType);
            TypeId newTypeId = baseTypeId.MakeUnmanagedPointerType();

            Assert.False(newTypeId.IsManagedPointerType);
            Assert.True(newTypeId.IsUnmanagedPointerType);
            Assert.False(newTypeId.IsElementalType);
            Assert.Equal(baseTypeId, newTypeId.GetUnderlyingType());
            Assert.Equal(baseTypeId.Name + "*", newTypeId.Name);
            Assert.Equal(expectedNewTotalComplexity, newTypeId.TotalComplexity);
            Assert.Same(baseTypeId.Assembly, newTypeId.Assembly);
            Assert.Same(baseTypeId, newTypeId.GetUnderlyingType());
        }

        [Theory]
        [InlineData(typeof(int[]), 3)]
        [InlineData(typeof(List<int>[]), 5)]
        [InlineData(typeof(List<>), 2)]
        [InlineData(typeof(void*), 3)]
        public void MakeArrayType_WhenValid_Success(Type baseType, int expectedNewTotalComplexity)
        {
            TypeId baseTypeId = TypeId.CreateFromExisting(baseType);

            // Test 1: SZARRAY

            TypeId newTypeId = baseTypeId.MakeSzArrayType();
            Assert.True(newTypeId.IsArrayType);
            Assert.True(newTypeId.IsSzArrayType);
            Assert.False(newTypeId.IsVariableBoundArrayType);
            Assert.Equal(1, newTypeId.GetArrayRank());
            Assert.False(newTypeId.IsElementalType);
            Assert.Equal(baseTypeId, newTypeId.GetUnderlyingType());
            Assert.Equal(baseTypeId.Name + "[]", newTypeId.Name);
            Assert.Equal(expectedNewTotalComplexity, newTypeId.TotalComplexity);
            Assert.Same(baseTypeId.Assembly, newTypeId.Assembly);

            // Test 2: MDARRAY, rank 1

            newTypeId = baseTypeId.MakeVariableBoundArrayType(1);
            Assert.True(newTypeId.IsArrayType);
            Assert.False(newTypeId.IsSzArrayType);
            Assert.True(newTypeId.IsVariableBoundArrayType);
            Assert.Equal(1, newTypeId.GetArrayRank());
            Assert.False(newTypeId.IsElementalType);
            Assert.Equal(baseTypeId, newTypeId.GetUnderlyingType());
            Assert.Equal(baseTypeId.Name + "[*]", newTypeId.Name);
            Assert.Equal(expectedNewTotalComplexity, newTypeId.TotalComplexity);
            Assert.Same(baseTypeId.Assembly, newTypeId.Assembly);

            // Test 2: MDARRAY, rank 3

            newTypeId = baseTypeId.MakeVariableBoundArrayType(3);
            Assert.True(newTypeId.IsArrayType);
            Assert.False(newTypeId.IsSzArrayType);
            Assert.True(newTypeId.IsVariableBoundArrayType);
            Assert.Equal(3, newTypeId.GetArrayRank());
            Assert.False(newTypeId.IsElementalType);
            Assert.Equal(baseTypeId, newTypeId.GetUnderlyingType());
            Assert.Equal(baseTypeId.Name + "[,,]", newTypeId.Name);
            Assert.Equal(expectedNewTotalComplexity, newTypeId.TotalComplexity);
            Assert.Same(baseTypeId.Assembly, newTypeId.Assembly);
        }

        [Theory]
        [InlineData("System.Int32")]
        [InlineData("System.Int32**")]
        [InlineData("System.Int32&")]
        [InlineData("System.Int32[]*")] // decorator after array type
        [InlineData("SomeGeneric`1[[SomeType[]]]")] // array type within generic arg, but not outer type
        public void GetArrayRank_ForNonArrayType_Throws(string typeName)
        {
            TypeId typeId = TypeId.ParseAssemblyQualifiedName(typeName);
            Assert.Throws<InvalidOperationException>(() => typeId.GetArrayRank());
        }

        [Theory]
        [InlineData("System.Int32")] // not generic
        [InlineData("SomeGeneric`1[[SomeType]][]")] // decorator after generic marker
        [InlineData("SomeGeneric`1")] // open (not constructed) generic type
        public void GetGenericTypeDefinition_ForNonConstructedGenericType_Throws(string typeName)
        {
            TypeId typeId = TypeId.ParseAssemblyQualifiedName(typeName);
            Assert.False(typeId.IsConstructedGenericType);
            Assert.Throws<InvalidOperationException>(() => typeId.GetGenericTypeDefinition());
        }

        [Theory]
        [InlineData("MyType", -1)]
        [InlineData("MyType`0", -1)] // can't have zero arity
        [InlineData("MyType`01", -1)] // leading zero disallowed
        [InlineData("MyType`1", 1)]
        [InlineData("MyType`123", 123)]
        [InlineData("MyType`123+Nested`456", 579)]
        [InlineData("MyType`a", -1)]
        [InlineData("MyType`+", -1)]
        [InlineData("MyType+Nested`2", 2)]
        [InlineData("MyType`", -1)]
        [InlineData("MyType```3", -1)]
        [InlineData("MyType`1[[SomeType]]", -1)] // constructed generic type
        [InlineData("MyType`3*&", -1)] // decorated types disallowed
        [InlineData("MyType`3+NonGenericNested*&", -1)] // decorated types disallowed
        [InlineData("MyType`2147483647+Nested", 2147483647)] // int.MaxValue
        [InlineData("MyType`2147483647+Nested`1", -1)] // overflow
        [InlineData("MyType`3+Nested`-1", -1)] // negatives disallowed
        public void IsLikelyGenericTypeDefinition_Tests(string typeName, int expectedArity)
        {
            TypeId typeId = TypeId.Parse(typeName, assembly: null);

            if (expectedArity == -1)
            {
                Assert.False(typeId.IsLikelyGenericTypeDefinition(out int actualArity));
                Assert.Equal(0, actualArity);
            }
            else
            {
                Assert.True(typeId.IsLikelyGenericTypeDefinition(out int actualArity));
                Assert.Equal(expectedArity, actualArity);
            }
        }

        [Fact]
        public void GetUnderlyingType_ForElementalType_ReturnsNull()
        {
            TypeId typeId = TypeId.CreateFromExisting(typeof(int));
            Assert.True(typeId.IsElementalType);
            Assert.Null(typeId.GetUnderlyingType());
        }

        [Theory]
        [InlineData(typeof(int*), typeof(int))]
        [InlineData(typeof(int[,,][]), typeof(int[]))] // C# jagged array constructs are in reverse order
        [InlineData(typeof(List<int>), typeof(List<>))]
        [InlineData(typeof(List<int>[]), typeof(List<int>))]
        [MemberData(nameof(CreateVariableBoundArrayWithRank), typeof(int[]), 1, new object[] { typeof(int[]) })]
        [MemberData(nameof(CreateByRefType), typeof(int[]), new object[] { typeof(int[]) })]
        public void GetUnderlyingType_ForNonElementalType_ReturnsUnderlyingType(Type typeToTest, Type expectedUnderlyingType)
        {
            TypeId typeIdToTest = TypeId.CreateFromExisting(typeToTest);
            TypeId expectedUnderlyingTypeId = TypeId.CreateFromExisting(expectedUnderlyingType);

            Assert.False(typeIdToTest.IsElementalType);
            Assert.Equal(expectedUnderlyingTypeId, typeIdToTest.GetUnderlyingType());
        }

        private static IEnumerable<TypeId> MakeSequentialTypeIdNames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return TypeId.Parse($"SomeType{i + 1}", assembly: null);
            }
        }

        [Theory]
        [InlineData("System.Int32", "mscorlib", true)]
        [InlineData("System.Int32, mscorlib", "mscorlib", false)]
        [InlineData("SomeType, SomeAssembly, Version=1.2.3.4", "SomeAssembly", true)]
        [InlineData("SomeType, SomeAssembly, Version=1.2.3.4", "SomeAssembly, Version = 1.2.3.4 ", false)]
        [InlineData("SomeType[]*[], SomeAssembly, Version=1.2.3.4", "SomeAssembly, Version=1.2.3.4", false)]
        [InlineData("SomeType[]*[], SomeAssembly, Version=1.2.3.4", null, true)]
        [InlineData("Generic`1[[InnerType, InnerAssembly]], SomeAssembly", "SomeAssembly", false)]
        [InlineData("Generic`1[[InnerType, InnerAssembly]], SomeAssembly", null, true)] // n.b. InnerAssembly won't change
        public void WithAssembly_Tests(string typeName, string newAssemblyName, bool expectedChange)
        {
            TypeId typeId = TypeId.ParseAssemblyQualifiedName(typeName);
            AssemblyId newAsmId = (string.IsNullOrEmpty(newAssemblyName)) ? null : AssemblyId.Parse(newAssemblyName);

            TypeId newTypeId = typeId.WithAssembly(newAsmId);
            Assert.Equal(newAsmId, newTypeId.Assembly);

            if (!expectedChange)
            {
                Assert.Same(typeId, newTypeId); // don't return a new instance if no change needed
            }
        }
    }
}
