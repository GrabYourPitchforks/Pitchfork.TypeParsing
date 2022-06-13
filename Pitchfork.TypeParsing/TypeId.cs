using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Pitchfork.TypeParsing.Resources;
using Pitchfork.TypeParsing.TypeInfo;

namespace Pitchfork.TypeParsing
{
    /// <summary>
    /// Represents the result of parsing a type string without actually loading
    /// a type into the runtime.
    /// </summary>
    public sealed partial class TypeId : IEquatable<TypeId>, IRandomizedHashCode
    {
        private string? _assemblyQualifiedName;

        // private ctor which sets members explicitly
        private TypeId(string name, ComplexTypeInfoBase? cti, int totalComplexity, AssemblyId? assembly)
        {
            Name = name;
            ComplexTypeInfo = cti;
            Assembly = assembly;
            TotalComplexity = totalComplexity;
        }

        /// <summary>
        /// The assembly-qualified name of the type; e.g., "System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089".
        /// </summary>
        /// <remarks>
        /// If <see cref="Assembly"/> is null, simply returns <see cref="Name"/>.
        /// </remarks>
        public string AssemblyQualifiedName
        {
            get
            {
                return (_assemblyQualifiedName ??= CreateAssemblyQualifiedName());

                [MethodImpl(MethodImplOptions.NoInlining)] // remove from hot path
                string CreateAssemblyQualifiedName() => (Assembly is not null) ? Name + ", " + Assembly : Name;
            }
        }

        /// <summary>
        /// The assembly which contains this type, or null if this <see cref="TypeId"/> was not
        /// created from a fully-qualified name.
        /// </summary>
        public AssemblyId? Assembly { get; }

        private ComplexTypeInfoBase? ComplexTypeInfo { get; }

        /// <summary>
        /// Returns true if this type represents any kind of array, regardless of the array's
        /// rank or its bounds.
        /// </summary>
        /// <remarks>
        /// If you want to know if this array represents a "normal" .NET array (e.g., "string[]"),
        /// query the <see cref="IsSzArrayType"/> property instead.
        /// </remarks>
        public bool IsArrayType => ComplexTypeInfo is ArrayTypeInfo;

        /// <summary>
        /// Returns true if this type represents a constructed generic type (e.g., "List&lt;int&gt;").
        /// </summary>
        /// <remarks>
        /// Returns false for open generic types (e.g., "Dictionary&lt;,&gt;").
        /// </remarks>
        public bool IsConstructedGenericType => ComplexTypeInfo is ConstructedGenericInfo;

        /// <summary>
        /// Returns true if this is a "plain" type; that is, not an array, not a pointer, and
        /// not a constructed generic type. Examples of elemental types are "System.Int32",
        /// "System.Uri", and "YourNamespace.YourClass".
        /// </summary>
        /// <remarks>
        /// <para>This property returning true doesn't mean that the type is a primitive like string
        /// or int; it just means that there's no underlying type (<see cref="GetUnderlyingType"/> returns null).</para>
        /// <para>This property will return true for generic type definitions (e.g., "Dictionary&lt;,&gt;").
        /// This is because determining whether a type truly is a generic type requires loading the type
        /// and performing a runtime check. See also <see cref="IsLikelyGenericTypeDefinition(out int)"/>.</para>
        /// </remarks>
        public bool IsElementalType => ComplexTypeInfo is null;

        /// <summary>
        /// Returns true if this is a managed pointer type (e.g., "ref int").
        /// Managed pointer types are sometimes called byref types.
        /// </summary>
        /// <remarks>
        /// Managed pointers are typically found in method signatures. For example, the
        /// second parameter in "void int.TryParse(string, out int)" has type "managed pointer to int".
        /// </remarks>
        public bool IsManagedPointerType => ComplexTypeInfo is PointerTypeInfo pti && pti.IsManagedPointer;

        /// <summary>
        /// Returns true if this type represents a single-dimensional, zero-indexed array (e.g., "int[]").
        /// </summary>
        public bool IsSzArrayType => ComplexTypeInfo is ArrayTypeInfo ati && ati.IsSzArray;

        /// <summary>
        /// Returns true if this type represents a variable-bound array; that is, an array of rank greater
        /// than 1 (e.g., "int[,]") or a single-dimensional array which isn't necessarily zero-indexed.
        /// </summary>
        public bool IsVariableBoundArrayType => ComplexTypeInfo is ArrayTypeInfo ati && !ati.IsSzArray;

        /// <summary>
        /// Returns true if this type represents an unmanaged pointer (e.g., "int*" or "void*").
        /// </summary>
        public bool IsUnmanagedPointerType => ComplexTypeInfo is PointerTypeInfo pti && !pti.IsManagedPointer;

        /// <summary>
        /// The name of this type, including namespace, but without the assembly name; e.g., "System.Int32".
        /// Nested types are represented with a '+'; e.g., "MyNamespace.MyType+NestedType".
        /// </summary>
        /// <remarks>
        /// <para>For constructed generic types, the type arguments will be listed using their fully qualified
        /// names. For example, given "List&lt;int&gt;", the <see cref="Name"/> property will return
        /// "System.Collections.Generic.List`1[[System.Int32, mscorlib, ...]]".</para>
        /// <para>For open generic types, the convention is to use a backtick ("`") followed by
        /// the arity of the generic type. For example, given "Dictionary&lt;,&gt;", the <see cref="Name"/>
        /// property will return "System.Collections.Generic.Dictionary`2". Given "Dictionary&lt;,&gt;.Enumerator",
        /// the <see cref="Name"/> property will return "System.Collections.Generic.Dictionary`2+Enumerator".
        /// See ECMA-335, Sec. I.10.7.2 (Type names and arity encoding) for more information.</para>
        /// </remarks>
        public string Name { get; }

        /// <summary>
        /// Represents the total amount of work that needs to be performed to fully inspect
        /// this instance, including any generic arguments or underlying types.
        /// </summary>
        /// <remarks>
        /// <para>There's not really a parallel concept to this in reflection. Think of it
        /// as the total number of <see cref="TypeId"/> instances that would be created if
        /// you were to totally deconstruct this instance and visit each intermediate <see cref="TypeId"/>
        /// that occurs as part of deconstruction.</para>
        /// <para>"int" and "Person" each have complexities of 1 because they're standalone types.</para>
        /// <para>"int[]" has a complexity of 2 because to fully inspect it involves inspecting the
        /// array type itself, <em>plus</em> unwrapping the underlying type ("int") and inspecting that.</para>
        /// <para>
        /// "Dictionary&lt;string, List&lt;int[][]&gt;&gt;" has complexity 8 because fully visiting it
        /// involves inspecting 8 <see cref="TypeId"/> instances total:
        /// <list type="bullet">
        /// <item>Dictionary&lt;string, List&lt;int[][]&gt;&gt; (the original type)</item>
        /// <item>Dictionary`2 (the generic type definition)</item>
        /// <item>string (a type argument of Dictionary)</item>
        /// <item>List&lt;int[][]&gt; (a type argument of Dictionary)</item>
        /// <item>List`1 (the generic type definition)</item>
        /// <item>int[][] (a type argument of List)</item>
        /// <item>int[] (the underlying type of int[][])</item>
        /// <item>int (the underlying type of int[])</item>
        /// </list>
        /// </para>
        /// </remarks>
        public int TotalComplexity { get; }

        /// <summary>
        /// Returns true iff two <see cref="TypeId"/> instances refer to the same type.
        /// </summary>
        public static bool operator ==(TypeId? a, TypeId? b) => (a is null) ? (b is null) : a.Equals(b);

        /// <summary>
        /// Returns true iff two <see cref="TypeId"/> instances refer to different types.
        /// </summary>
        public static bool operator !=(TypeId? a, TypeId? b) => !(a == b);

        internal static TypeId CreateElementalTypeWithoutValidation(string name, AssemblyId? assembly)
            => new TypeId(name, cti: null, totalComplexity: 1, assembly);

        /// <summary>
        /// Creates a <see cref="TypeId"/> instances wrapped around an existing runtime <see cref="Type"/>.
        /// </summary>
        /// <param name="followTypeForwards">If true, will follow any <see cref="TypeForwardedFromAttribute"/> markers on
        /// <paramref name="type"/> and on any generic arguments.</param>
        /// <exception cref="ArgumentException"><paramref name="type"/> is not supported; for example, it represents a COM type.</exception>
        public static TypeId CreateFromExisting(Type type, bool followTypeForwards = false)
        {
            try
            {
                return Worker(type, followTypeForwards);
            }
            catch (Exception ex)
            {
                // wrap in a friendlier error message
                throw new ArgumentException(
                    paramName: nameof(type),
                    message: string.Format(CultureInfo.InvariantCulture, SR.TypeId_ExistingTypeNotAllowed, type),
                    innerException: ex);
            }

            static TypeId Worker(Type type, bool followTypeForwards)
            {
                // First, some quick validation checks.

                if (type.IsGenericParameter)
                {
                    ThrowNotSupportedException(type, SR.TypeId_ExistingTypeNotAllowed_NoGenericArgs);
                }
                else if (type.IsCOMObject)
                {
                    ThrowNotSupportedException(type, SR.TypeId_ExistingTypeNotAllowed_NoComTypes);
                }
                else if (false)
                {
                    // TODO: How do we detect function pointers?
                    ThrowNotSupportedException(type, SR.TypeId_ExistingTypeNotAllowed_NoFunctionPointers);
                }

                // Unwrap and apply decorators as needed.

                if (type.IsArray)
                {
                    if (type.IsSzArrayType())
                    {
                        return Worker(type.GetElementType()!, followTypeForwards).MakeSzArrayType();
                    }
                    else
                    {
                        return Worker(type.GetElementType()!, followTypeForwards).MakeVariableBoundArrayType(type.GetArrayRank());
                    }
                }
                else if (type.IsConstructedGenericType)
                {
                    return Worker(type.GetGenericTypeDefinition(), followTypeForwards)
                        .MakeGenericType(Array.ConvertAll(type.GetGenericArguments(), (type) => Worker(type, followTypeForwards)));
                }
                else if (type.IsByRef)
                {
                    return Worker(type.GetElementType()!, followTypeForwards).MakeManagedPointerType();
                }
                else if (type.IsPointer)
                {
                    return Worker(type.GetElementType()!, followTypeForwards).MakeUnmanagedPointerType();
                }
                else
                {
                    Debug.Assert(!type.HasElementType, "Expected this to be an elemental type.");

                    IdentifierRestrictor.ThrowIfDisallowedTypeName(type.Name, ParseOptions.FromExistingIdentifier);
                    return new TypeId(
                        name: type.FullName,
                        cti: null,
                        totalComplexity: 1,
                        assembly: GetAssemblyForType(type, followTypeForwards));
                }

                [DoesNotReturn]
                [StackTraceHidden]
                static void ThrowNotSupportedException(Type type, string formatString)
                {
                    throw new NotSupportedException(
                        message: string.Format(CultureInfo.CurrentCulture, formatString, type));
                }

                static AssemblyId GetAssemblyForType(Type type, bool followTypeForwards)
                    => (followTypeForwards && (type.GetCustomAttribute(typeof(TypeForwardedFromAttribute)) is TypeForwardedFromAttribute attr))
                    ? AssemblyId.Parse(attr.AssemblyFullName)
                    : AssemblyId.CreateFromExisting(type.Assembly);
            }
        }

        /// <summary>
        /// Loads the actual <see cref="Type"/> represented by this <see cref="TypeId"/>
        /// into the runtime and returns it. The caller should only use this method after
        /// ensuring that the current <see cref="TypeId"/> instance represents an allowable
        /// type to be loaded.
        /// </summary>
        /// <param name="throwOnError">Specifies whether the method should return null or throw
        /// an exception if the type is not found. See <see cref="Type.GetType(string, bool)"/>.</param>
        public Type? DangerousGetRuntimeType(bool throwOnError = true)
        {
            if (Assembly is not null)
            {
                // Load from assembly
                Assembly assembly = System.Reflection.Assembly.Load(Assembly.ToAssemblyName());
                return assembly.GetType(Name, throwOnError);
            }
            else
            {
                // Load from ambient context (likely mscorlib or similar)
                return Type.GetType(Name, throwOnError);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [StackTraceHidden]
        private void EnsureNotManagedPointerType()
        {
            if (IsManagedPointerType)
            {
                Throw();
            }

            [DoesNotReturn]
            [StackTraceHidden]
            void Throw()
            {
                throw new InvalidOperationException(SR.TypeId_OperationNotAllowedOnByRefTypes);
            }
        }

        /// <summary>
        /// Returns true iff two <see cref="TypeId"/> instances refer to the same type.
        /// </summary>
        public override bool Equals(object? obj) => Equals(obj as TypeId);

        /// <summary>
        /// Returns true iff two <see cref="TypeId"/> instances refer to the same type.
        /// </summary>
        public bool Equals(TypeId? other)
        {
            // fast-track reference equality
            if (ReferenceEquals(this, other)) { return true; }

            return other is not null
                && this.Name == other.Name
                && this.Assembly == other.Assembly
                && Equals(this.ComplexTypeInfo, other.ComplexTypeInfo);
        }

        [StackTraceHidden]
        private ArrayTypeInfo GetArrayInfoOrThrow() =>
            (ComplexTypeInfo as ArrayTypeInfo)
            ?? throw new InvalidOperationException(SR.TypeId_InstanceIsNotAnArrayType);

        /// <summary>
        /// If this <see cref="TypeId"/> represents an array type, returns its rank (number of dimensions).
        /// </summary>
        /// <remarks>
        /// For example, given "int[]", returns 1. Given "int[,,]", returns 3.
        /// </remarks>
        /// <exception cref="InvalidOperationException"><see cref="IsArrayType"/> is false.</exception>
        public int GetArrayRank() => GetArrayInfoOrThrow().Rank;

        [StackTraceHidden]
        private ConstructedGenericInfo GetConstructedGenericInfoOrThrow() =>
            (ComplexTypeInfo as ConstructedGenericInfo)
            ?? throw new InvalidOperationException(SR.TypeId_InstanceIsNotAConstructedGenericType);
        /// <summary>
        /// If this <see cref="TypeId"/> represents a constructed generic type, returns an array
        /// of all the generic arguments.
        /// </summary>
        /// <remarks>
        /// <para>For example, given "Dictionary&lt;string, int&gt;", returns a 2-element array containing
        /// string and int.</para>
        /// <para>The caller controls the returned array and may mutate it freely.</para>
        /// </remarks>
        /// <exception cref="InvalidOperationException"><see cref="IsConstructedGenericType"/> is false.</exception>
        public TypeId[] GetGenericParameters() => GetConstructedGenericInfoOrThrow().GenericArguments.ToArray();

        /// <summary>
        /// If this <see cref="TypeId"/> represents a constructed generic type, returns the number of
        /// generic arguments. For a constructed generic type (see <see cref="IsConstructedGenericType"/>),
        /// this will be equal to the length of the array returned by <see cref="GetGenericParameters"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="IsConstructedGenericType"/> is false.</exception>
        public int GetGenericParameterCount() => GetConstructedGenericInfoOrThrow().GenericArguments.Length;

        /// <summary>
        /// If this <see cref="TypeId"/> represents a constructed generic type, returns the number of
        /// generic arguments. For a constructed generic type (see <see cref="IsConstructedGenericType"/>),
        /// this will be equal to the length of the array returned by <see cref="GetGenericParameters"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="IsConstructedGenericType"/> is false.</exception>
        public TypeId GetGenericTypeDefinition() => GetConstructedGenericInfoOrThrow().ElementalType;

        /// <summary>
        /// Gets a hash code for this instance.
        /// </summary>
        /// <remarks>The hash code is randomized per application instance. Callers should not persist the hash code.</remarks>
        public override int GetHashCode()
        {
            RandomizedHashCode hashCode = new RandomizedHashCode(RandomizedHashCode.Caller.TypeId);
            hashCode.Add(Name);
            hashCode.Add(ComplexTypeInfo);
            hashCode.Add(Assembly);
            return hashCode.ToHashCode();
        }

        /// <summary>
        /// If this type is not an elemental type (see <see cref="IsElementalType"/>), gets
        /// the underlying type. If this type is an elemental type, returns null.
        /// </summary>
        /// <remarks>
        /// For example, given "int[][]", unwraps the outermost array and returns "int[]".
        /// Given "Dictionary&lt;string, int&gt;", returns the generic type definition "Dictionary&lt;,&gt;".
        /// </remarks>
        public TypeId? GetUnderlyingType() => ComplexTypeInfo?.ElementalType;

        /// <summary>
        /// Determines whether this type likely represents a generic type definition (e.g., "List&lt;&gt;").
        /// </summary>
        /// <param name="genericParameterCount">When the method returns, if this is likely a generic type
        /// definition, contains the arity of the generic type. Otherwise contains 0.</param>
        /// <remarks>
        /// <para>In general, it is not possible to tell from the type string alone whether a type represents a
        /// generic type definition. The only way to determine this with 100% accuracy is to load the type
        /// into the runtime.</para>
        /// <para>This check uses a heuristic to determine whether a type string looks like the name
        /// of a generic type definition. This heuristic should be reliable enough for general use, but
        /// nevertheless it remains a heuristic and may miss edge cases. See See ECMA-335,
        /// Sec. I.10.7.2 (Type names and arity encoding) for more information.</para>
        /// </remarks>
        public bool IsLikelyGenericTypeDefinition(out int genericParameterCount)
        {
            if (!IsElementalType)
            {
                goto Fail; // only elemental types can be generic type definitions
            }

            ReadOnlySpan<char> remaining = Name.AsSpan();
            ulong totalArity = 0;

            int idxOfBacktick;
            while ((idxOfBacktick = remaining.IndexOf('`')) >= 0)
            {
                if (idxOfBacktick == 0)
                {
                    // saw an empty string before the backtick
                    // technically allowed, but violates CLS per ECMA-335, Sec. I.10.7.2, Rule (1)
                    goto Fail;
                }

                remaining = remaining.Slice(idxOfBacktick + 1);

                ulong thisLevelArity = 0;

                while (!remaining.IsEmpty)
                {
                    char nextChar = remaining[0];
                    remaining = remaining.Slice(1);

                    if (nextChar == '+')
                    {
                        break;
                    }
                    else if ('0' <= nextChar && nextChar <= '9')
                    {
                        thisLevelArity *= 10;
                        thisLevelArity += (uint)(nextChar - '0');

                        // No leading zeroes and no integer overflow
                        if (thisLevelArity == 0 || thisLevelArity > int.MaxValue)
                        {
                            goto Fail;
                        }
                    }
                    else
                    {
                        // Saw "`unknown"
                        goto Fail;
                    }
                }

                // Any level that has "`0" or "`invalid" causes us to exit immediately

                if (thisLevelArity == 0)
                {
                    goto Fail;
                }

                totalArity += thisLevelArity;
                if (totalArity > int.MaxValue)
                {
                    goto Fail; // avoid integer overflow
                }
            }

            Debug.Assert(0 <= totalArity && totalArity <= int.MaxValue);
            if (totalArity >= 1) // it's possible we read no markers at all
            {
                genericParameterCount = (int)totalArity;
                return true;
            }

        Fail:
            genericParameterCount = default;
            return false;
        }

        /// <summary>
        /// Given a <see cref="TypeId"/> which represents a generic type definition, creates
        /// a constructed generic type using the provided type arguments.
        /// </summary>
        /// <remarks>
        /// This method uses a heuristic to judge whether the current <see cref="TypeId"/> likely
        /// represents a generic type definition and what its arity would be. This heuristic
        /// may be conservative. If the heursitic fails, this method throws <see cref="InvalidOperationException"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">This <see cref="TypeId"/> likely doesn't
        /// represent a generic type definition of the correct arity.</exception>
        public TypeId MakeGenericType(params Type[] types)
            => MakeGenericType(Array.ConvertAll(types, (type) => CreateFromExisting(type)));

        /// <summary>
        /// Given a <see cref="TypeId"/> which represents a generic type definition, creates
        /// a constructed generic type using the provided type arguments.
        /// </summary>
        /// <remarks>
        /// This method uses a heuristic to judge whether the current <see cref="TypeId"/> likely
        /// represents a generic type definition and what its arity would be. This heuristic
        /// may be conservative. If the heursitic fails, this method throws <see cref="InvalidOperationException"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">This <see cref="TypeId"/> likely doesn't
        /// represent a generic type definition of the correct arity.</exception>
        public TypeId MakeGenericType(params TypeId[] types)
        {
            if (types is null || types.Length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(types));
            }

            if (!IsElementalType)
            {
                throw new InvalidOperationException(SR.TypeId_CannotMakeGenericsFromNonElementalTypes);
            }

            // Ensure the suffix "`xyz" exists.
            // This disallows things like "`02", etc.

            if (!IsLikelyGenericTypeDefinition(out int deducedArity) || types.Length != deducedArity)
            {
                throw new InvalidOperationException(
                    message: string.Format(CultureInfo.CurrentCulture, SR.TypeId_TypeNameDoesNotHaveCorrectAritySuffix, types.Length, Name));
            }

            // New total complexity will be the sum of the cumulative args' complexity + 2:
            // - one for the generic type definition "MyGeneric`x"
            // - one for the constructed type definition "MyGeneric`x[[...]]"
            // - and the cumulative complexity of all the arguments

            Debug.Assert(TotalComplexity == 1, "Any elemental type should have complexity 1.");
            int newTotalComplexity = 2;

            StringBuilder newDisplayName = new StringBuilder();
            newDisplayName.Append(Name);
            newDisplayName.Append('[');

            foreach (TypeId typeId in types)
            {
                newTotalComplexity = checked(newTotalComplexity + typeId.TotalComplexity);
                newDisplayName.Append('[');
                newDisplayName.Append(typeId.AssemblyQualifiedName);
                newDisplayName.Append(']');
                newDisplayName.Append(',');
            }
            newDisplayName[newDisplayName.Length - 1] = ']'; // replace ',' with ']'

            return new TypeId(
                name: newDisplayName.ToString(),
                cti: new ConstructedGenericInfo(types, this),
                totalComplexity: newTotalComplexity,
                assembly: Assembly);
        }

        /// <summary>
        /// Creates a <see cref="TypeId"/> that represents a managed pointer
        /// to the current <see cref="TypeId"/>.
        /// </summary>
        /// <remarks>For example, if the current type represents "int", returns a type representing "ref int".</remarks>
        public TypeId MakeManagedPointerType()
        {
            EnsureNotManagedPointerType();

            return new TypeId(
                name: Name + "&",
                cti: new PointerTypeInfo(isManagedPointer: true, elementalType: this),
                totalComplexity: checked(TotalComplexity + 1),
                assembly: Assembly);
        }

        /// <summary>
        /// Creates a <see cref="TypeId"/> that represents a single-dimensional, zero-indexed array
        /// whose element type is the current <see cref="TypeId"/>.
        /// </summary>
        /// <remarks>For example, if the current type represents "string", returns a type representing "string[]".</remarks>
        public TypeId MakeSzArrayType()
        {
            EnsureNotManagedPointerType();

            return new TypeId(
                name: Name + "[]",
                cti: new ArrayTypeInfo(isSzArray: true, rank: 1, elementalType: this),
                totalComplexity: checked(TotalComplexity + 1),
                assembly: Assembly);
        }

        /// <summary>
        /// Creates a <see cref="TypeId"/> that represents an unmanaged pointer
        /// to the current <see cref="TypeId"/>.
        /// </summary>
        /// <remarks>For example, if the current type represents "int", returns a type representing "int*".</remarks>
        public TypeId MakeUnmanagedPointerType()
        {
            EnsureNotManagedPointerType();

            return new TypeId(
                name: Name + "*",
                cti: new PointerTypeInfo(isManagedPointer: false, elementalType: this),
                totalComplexity: checked(TotalComplexity + 1),
                assembly: Assembly);
        }

        /// <summary>
        /// Creates a <see cref="TypeId"/> that represents a variable-bound array
        /// whose element type is the current <see cref="TypeId"/>.
        /// </summary>
        /// <remarks>For example, if the current type represents "int", then given rank 3, returns a type representing "int[,,]".</remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="rank"/> is not between 1 and 32, inclusive.</exception>
        public TypeId MakeVariableBoundArrayType(int rank)
        {
            EnsureNotManagedPointerType();

            // Ctor below performs validation on the rank argument
            ArrayTypeInfo ati = new ArrayTypeInfo(isSzArray: false, rank: rank, elementalType: this);
            return new TypeId(
                name: $"{Name}[{((rank == 1) ? "*" : new string(',', rank - 1))}]",
                cti: ati,
                totalComplexity: checked(TotalComplexity + 1),
                assembly: Assembly);
        }

        /// <summary>
        /// Parses the provided type name and creates a new <see cref="TypeId"/> instance wrapping
        /// it and the provided assembly. <paramref name="typeName"/> must not be an assembly-qualified name.
        /// </summary>
        /// <remarks>
        /// Use <see cref="ParseAssemblyQualifiedName"/> if you want to allow <paramref name="typeName"/>
        /// to contain an assembly-qualified name.
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="typeName"/> is malformed, disallowed, or represents an assembly-qualified name.</exception>
        public static TypeId Parse(ReadOnlySpan<char> typeName, AssemblyId? assembly, ParseOptions? parseOptions = null)
           => ParseCommon(typeName, allowFullyQualifiedName: false, parseOptions).WithAssembly(assembly);

        /// <summary>
        /// Parses the provided type name and creates a new <see cref="TypeId"/> instance wrapping
        /// it and the provided assembly. <paramref name="typeName"/> must not be an assembly-qualified name.
        /// </summary>
        /// <remarks>
        /// Use <see cref="ParseAssemblyQualifiedName"/> if you want to allow <paramref name="typeName"/>
        /// to contain an assembly-qualified name.
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="typeName"/> is malformed, disallowed, or represents an assembly-qualified name.</exception>
        public static TypeId Parse(string typeName, AssemblyId? assembly, ParseOptions? parseOptions = null)
            => Parse(typeName.AsSpan(), assembly, parseOptions);

        /// <summary>
        /// Parses the provided type name and creates a new <see cref="TypeId"/> instance wrapping
        /// it. If <paramref name="typeName"/> represents an assembly-qualified name, the assembly
        /// information is extracted from the string.
        /// </summary>
        /// <remarks>
        /// Use <see cref="Parse"/> if you want to forbid <paramref name="typeName"/>
        /// from containing an assembly-qualified name.
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="typeName"/> is malformed or disallowed.</exception>
        public static TypeId ParseAssemblyQualifiedName(ReadOnlySpan<char> assemblyQualifiedTypeName, ParseOptions? parseOptions = null)
            => ParseCommon(assemblyQualifiedTypeName, allowFullyQualifiedName: true, parseOptions);

        /// <summary>
        /// Parses the provided type name and creates a new <see cref="TypeId"/> instance wrapping
        /// it. If <paramref name="typeName"/> represents an assembly-qualified name, the assembly
        /// information is extracted from the string.
        /// </summary>
        /// <remarks>
        /// Use <see cref="Parse"/> if you want to forbid <paramref name="typeName"/>
        /// from containing an assembly-qualified name.
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="typeName"/> is malformed or disallowed.</exception>
        public static TypeId ParseAssemblyQualifiedName(string assemblyQualifiedTypeName, ParseOptions? parseOptions = null)
            => ParseAssemblyQualifiedName(assemblyQualifiedTypeName.AsSpan(), parseOptions);

        private static TypeId ParseCommon(ReadOnlySpan<char> typeName, bool allowFullyQualifiedName, ParseOptions? parseOptions)
            => TypeIdParser.Parse(typeName, allowFullyQualifiedName, parseOptions ?? ParseOptions.GlobalDefaults);

        /// <summary>
        /// Returns <see cref="AssemblyQualifiedName"/>.
        /// </summary>
        public override string ToString() => AssemblyQualifiedName;

        /// <summary>
        /// Visits the current <see cref="TypeId"/> with <paramref name="visitor"/>,
        /// returning the result of <see cref="TypeIdVisitor.VisitType(TypeId)"/>.
        /// </summary>
        public TypeId Visit(TypeIdVisitor visitor) => visitor.VisitType(this);

        /// <summary>
        /// Returns a new <see cref="TypeId"/> instance with the same <see cref="Name"/>
        /// as this instance but with a different value for <see cref="Assembly"/>.
        /// </summary>
        /// <remarks>
        /// If this type represents a constructed generic type and you want to change the
        /// <see cref="Assembly"/> properties of the generic type arguments, consider
        /// implementing a <see cref="TypeIdVisitor"/> and calling <see cref="Visit"/>.
        /// </remarks>
        public TypeId WithAssembly(AssemblyId? assembly)
        {
            if (this.Assembly == assembly)
            {
                return this; // fast-track no change
            }
            else
            {
                // Non-elemental types get their assembly information from the
                // basic elemental type. So we need to drill down to the elemental
                // type and reconstruct it, then reapply all the decorators.

                TypeIdDecoratorStack decoratorStack = default;

                TypeId baseType;
                for (baseType = this; !baseType.IsElementalType; baseType = baseType.GetUnderlyingType()!)
                {
                    if (baseType.IsSzArrayType)
                    {
                        decoratorStack.PushMakeSzArrayType();
                    }
                    else if (baseType.IsVariableBoundArrayType)
                    {
                        decoratorStack.PushMakeVariableBoundArrayType(baseType.GetArrayRank());
                    }
                    else if (baseType.IsConstructedGenericType)
                    {
                        decoratorStack.PushMakeClosedGenericType(baseType.GetGenericParameters());
                    }
                    else if (baseType.IsManagedPointerType)
                    {
                        decoratorStack.PushMakeManagedPointerType();
                    }
                    else if (baseType.IsUnmanagedPointerType)
                    {
                        decoratorStack.PushMakeUnmanagedPointerType();
                    }
                    else
                    {
                        Debug.Fail("We missed a case.");
                        throw new NotSupportedException();
                    }
                }

                Debug.Assert(baseType.ComplexTypeInfo is null);
                Debug.Assert(baseType.TotalComplexity == 1);
                TypeId retVal = new TypeId(baseType.Name, baseType.ComplexTypeInfo, baseType.TotalComplexity, assembly /* new assembly */);

                // Reapply decorators

                retVal = decoratorStack.PopAllDecoratorsOnto(retVal);
                Debug.Assert(this.TotalComplexity == retVal.TotalComplexity, "We missed a decorator?");
                return retVal;
            }
        }
    }
}
